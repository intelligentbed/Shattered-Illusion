using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Core.OverrideSystem
{
    /// <summary>
    /// Finds every NPCBehaviorOverride subclass and registers it by NPC type.
    /// </summary>
    public static class NPCBehaviorOverrideLoader
    {
        private static NPCBehaviorOverrideContainer?[]? _overridesByNPCType;

        private const BindingFlags HookLookupFlags =
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly;

        public static void Load()
        {
            _overridesByNPCType =
                new NPCBehaviorOverrideContainer?[NPCLoader.NPCCount];

            var overrideTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type =>
                    !type.IsAbstract &&
                    typeof(NPCBehaviorOverride).IsAssignableFrom(type));

            foreach (Type type in overrideTypes)
            {
                var instance = Activator.CreateInstance(type) as NPCBehaviorOverride;

                if (instance is null)
                {
                    throw new InvalidOperationException(
                        $"Failed to create NPCBehaviorOverride instance for {type.FullName}.");
                }

                bool hasPreAI =
                    OverridesMethod(type, nameof(NPCBehaviorOverride.PreAI));

                bool hasFindFrame =
                    OverridesMethod(type, nameof(NPCBehaviorOverride.FindFrame));

                bool hasPreDraw =
                    OverridesMethod(type, nameof(NPCBehaviorOverride.PreDraw));

                bool hasCheckDead =
                    OverridesMethod(type, nameof(NPCBehaviorOverride.CheckDead));

                bool hasModifyNPCLoot =
                    OverridesMethod(type, nameof(NPCBehaviorOverride.ModifyNPCLoot));

                int npcType = instance.NPCOverrideType;

                if (npcType < 0 || npcType >= _overridesByNPCType.Length)
                {
                    throw new IndexOutOfRangeException(
                        $"{type.Name}.NPCOverrideType ({npcType}) " +
                        $"is not a valid NPC type index.");
                }

                if (_overridesByNPCType[npcType] is not null)
                {
                    NPCBehaviorOverrideContainer existing =
                        _overridesByNPCType[npcType]!;

                    throw new InvalidOperationException(
                        $"Multiple NPCBehaviorOverride classes are registered " +
                        $"for NPC type {npcType}. " +
                        $"Existing override: " +
                        $"{existing.BehaviorOverride.GetType().FullName}. " +
                        $"Conflicting override: {type.FullName}.");
                }

                var container = new NPCBehaviorOverrideContainer(
                    instance,
                    hasPreAI,
                    hasFindFrame,
                    hasPreDraw,
                    hasCheckDead,
                    hasModifyNPCLoot);

                instance.Load();

                _overridesByNPCType[npcType] = container;
            }
        }

        public static void Unload()
        {
            _overridesByNPCType = null;
        }

        private static bool OverridesMethod(Type type, string methodName)
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                HookLookupFlags);

            return method is not null &&
                   method.DeclaringType != typeof(NPCBehaviorOverride);
        }

        public static bool TryGet(
            int npcType,
            out NPCBehaviorOverrideContainer container)
        {
            container = null!;

            NPCBehaviorOverrideContainer?[]? overrides =
                _overridesByNPCType;

            if (overrides is null ||
                npcType < 0 ||
                npcType >= overrides.Length)
            {
                return false;
            }

            NPCBehaviorOverrideContainer? found =
                overrides[npcType];

            if (found is null)
            {
                return false;
            }

            container = found;
            return true;
        }

        public static bool Registered(int npcType)
        {
            return TryGet(npcType, out _);
        }

        public static bool Registered<T>() where T : ModNPC
        {
            return Registered(ModContent.NPCType<T>());
        }
    }

    /// <summary>
    /// Dispatches GlobalNPC hooks to the registered NPC behavior override.
    ///
    /// This class intentionally uses one shared behavior instance per NPC type.
    /// Individual NPC state must therefore be stored on the NPC itself or in
    /// per-entity data.
    /// </summary>
    public class NPCBehaviorOverrideGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override void SetDefaults(NPC npc)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container))
            {
                container.BehaviorOverride.SetDefaults(npc);
            }
        }

        public override bool PreAI(NPC npc)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container) &&
                container.HasPreAI)
            {
                return container.BehaviorOverride.PreAI(npc);
            }

            return true;
        }

        public override void SendExtraAI(
            NPC npc,
            BitWriter bitWriter,
            BinaryWriter binaryWriter)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container))
            {
                container.BehaviorOverride.SendExtraData(
                    npc,
                    binaryWriter);
            }
        }

        public override void ReceiveExtraAI(
            NPC npc,
            BitReader bitReader,
            BinaryReader binaryReader)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container))
            {
                container.BehaviorOverride.ReceiveExtraData(
                    npc,
                    binaryReader);
            }
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container) &&
                container.HasFindFrame)
            {
                container.BehaviorOverride.FindFrame(
                    npc,
                    frameHeight);
            }
        }

        public override bool PreDraw(
            NPC npc,
            SpriteBatch spriteBatch,
            Vector2 screenPos,
            Color drawColor)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container) &&
                container.HasPreDraw)
            {
                return container.BehaviorOverride.PreDraw(
                    npc,
                    spriteBatch,
                    screenPos,
                    drawColor);
            }

            return true;
        }

        public override bool CheckDead(NPC npc)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container) &&
                container.HasCheckDead)
            {
                return container.BehaviorOverride.CheckDead(npc);
            }

            return true;
        }

        public override void ModifyNPCLoot(
            NPC npc,
            NPCLoot npcLoot)
        {
            if (NPCBehaviorOverrideLoader.TryGet(npc.type, out var container))
            {
                container.BehaviorOverride.ModifyNPCLoot(
                    npc,
                    npcLoot);
            }
        }
    }
}