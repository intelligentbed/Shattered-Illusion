using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Visuals;
using ShatteredIllusion.Content.Buffs.StatBuffs;
using ShatteredIllusion.Content.Particles;
using ShatteredIllusion.Core.OverrideSystem;
using ShatteredIllusion.Core.Packets;
using ShatteredIllusionKeybinds;
using ParticleLibrary.Core.V3.Particles;
using ParticleLibrary.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using SystemVector2 = System.Numerics.Vector2;

namespace ShatteredIllusion.Common.Players.ParrySystem
{
    // ok since i know people will be confused on what iparryable means 
    // IT MEANS THAT THE BOSS IS PARRYABLE BUT NOT THE ATTACK YOU GOTTA DO THAT YOURSELF 
    // AND YOU HAVE HAVE TO MAKE AN EFFECT FOR THE PARRYABLE ATTACKS 
    public interface IParryable
    {
        bool IsParryable { get; }
        void OnParried(Player player);

        // what the tutorial hint flashes off of, only override if it differs from IsParryable
        bool IsTutorialParryable(NPC npc) => IsParryable;
    }

    public enum ParryVisualEffect : byte
    {
        Started,
        Succeeded,
        MeterFull,
        SteeledActivated,
        MycelialExplosion,
        Failed
    }

    public class ParryPlayer : ModPlayer
    {
        private static readonly SoundStyle ParryFullSound = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/ParryBarFull");
        private static readonly SoundStyle SteeledUseSound = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/dry-fart");
        private static readonly SoundStyle FailedParry = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/FailedParry");
        private static readonly SoundStyle SuccessfulParry = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/SuccessfulParry");

        public int CooldownTimer;
        public int parrySlowTimer;
        private bool parrySucceededThisWindow;

        public const int MaxCooldown = 120;
        public bool IsParrying => parrySlowTimer > 0;

        public const int ParryWindowFrames = 15;
        private const int ParryImmuneFrames = 60;
        private const float ParryKnockback = 4f;
        private const float ParryStartSlowdown = 0.2f;
        private const float ParryRunSlowdown = 0.15f;

        // server gets the success packet a bit after its own window closes
        private const int ServerParryGrace = 6;
        private const float ServerParryRange = 600f;
        private int parryGraceTimer;

        public int SturdinessMeter;
        public const int MaxSturdinessMeter = 100;
        public const int SturdinessMeterGainPerParry = 20;

        public const int FocusedBuffDuration = 600;
        public const int ParryHealAmount = 20;

        // The curve for the buff duration so above 60 = good below cant use 
        public const float SteeledCurveThreshold = 0.6f;

        private const float SteeledMinScale = 0.05f;
        private int steeledHealPerParry;

        private int steeledDrainStartValue;
        private int steeledDrainDuration;
        private int steeledDrainTimer;

        public float DisplaySturdinessMeter
        {
            get
            {
                if (steeledDrainTimer <= 0 || steeledDrainDuration <= 0)
                    return SturdinessMeter;

                float progress = 1f - (steeledDrainTimer / (float)steeledDrainDuration);
                return MathHelper.Lerp(steeledDrainStartValue, 0f, progress);
            }
        }

        public bool MycelialSetActive;

        private const float MycelialExplosionRadius = 200f;
        private const int MycelialExplosionDamage = 15;
        private const int MycelialPoisonedDuration = 180;

        // feel stuff
        private const int ImpactHoldFrames = 4;
        private const float ImpactHoldDamping = 0.5f;
        private const int ShockwaveTravelFrames = 10;
        private const int ShieldDustCount = 56;

        private Vector3 flashColor;
        private int flashTimer;
        private int flashDuration;
        private int impactHoldTimer;
        private int windowVisualTimer;

        public override void ResetEffects()
        {
            MycelialSetActive = false;
        }

        public override void PostUpdate()
        {
            if (CooldownTimer > 0)
            {
                CooldownTimer--;

                if (CooldownTimer == 0 && Player.whoAmI == Main.myPlayer && !Main.dedServ)
                    SpawnRing(Player.Center, DustID.Silver, 26f, 10, -1.6f, alpha: 100, scale: 1.1f);
            }

            if (windowVisualTimer > 0)
            {
                windowVisualTimer--;

                if (!Main.dedServ)
                    SpawnWindowRing();
            }

            if (parrySlowTimer > 0)
            {
                parrySlowTimer--;

                if (parrySlowTimer <= 0 && !parrySucceededThisWindow)
                {
                    if (Main.netMode == NetmodeID.Server)
                        parryGraceTimer = ServerParryGrace;
                    else if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        // owner plays it right away, the server skips them
                        if (Player.whoAmI == Main.myPlayer)
                            PlayVisualEffect(ParryVisualEffect.Failed, -1, 1f);
                    }
                    else
                        TriggerVisualEffect(ParryVisualEffect.Failed);
                }
            }
            else if (parryGraceTimer > 0)
            {
                parryGraceTimer--;

                if (parryGraceTimer <= 0 && !parrySucceededThisWindow)
                    TriggerVisualEffect(ParryVisualEffect.Failed, skipOwner: true);
            }

            if (steeledDrainTimer > 0)
                steeledDrainTimer--;

            if (flashTimer > 0)
            {
                flashTimer--;

                if (!Main.dedServ)
                {
                    float fade = flashTimer / (float)flashDuration;
                    Lighting.AddLight(Player.Center, flashColor * (fade * fade));
                }
            }
        }

        public override void PreUpdateMovement()
        {
            if (impactHoldTimer > 0)
            {
                impactHoldTimer--;
                Player.velocity *= ImpactHoldDamping;
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (KeybindSystem.ParryKeybind.JustPressed)
            {
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    // predict the window locally, otherwise ping eats it
                    if (BeginParryWindow())
                    {
                        PlayVisualEffect(ParryVisualEffect.Started, -1, 1f);
                        SendRequest(MessageType.RequestParry);
                    }
                }
                else
                    TryBeginParry();
            }

            if (KeybindSystem.SturdinessMeterUseKeybind.JustPressed)
            {
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    SendRequest(MessageType.RequestActivateSteeled);
                else
                    TryActivateSteeledBuff();
            }
        }

        internal static void HandleParryRequest(int whoAmI)
        {
            if (!IsValidRequestingPlayer(whoAmI))
                return;

            Main.player[whoAmI].GetModPlayer<ParryPlayer>().TryBeginParry();
        }

        internal static void HandleActivateSteeledRequest(int whoAmI)
        {
            if (!IsValidRequestingPlayer(whoAmI))
                return;

            Main.player[whoAmI].GetModPlayer<ParryPlayer>().TryActivateSteeledBuff();
        }

        // client tells the server it ate a parryable hit, server hands out the rewards
        internal static void HandleParrySuccessRequest(int whoAmI, BinaryReader reader)
        {
            int attackerIndex = reader.ReadInt16();

            if (!IsValidRequestingPlayer(whoAmI))
                return;

            Main.player[whoAmI].GetModPlayer<ParryPlayer>().TryApplyClientParry(attackerIndex);
        }

        private static bool IsValidRequestingPlayer(int whoAmI)
        {
            return whoAmI >= 0
                && whoAmI < Main.maxPlayers
                && Main.player[whoAmI].active
                && !Main.player[whoAmI].dead;
        }

        private static bool TryGetParryable(NPC attacker, out IParryable boss)
        {
            boss = attacker.ModNPC as IParryable;

            if (boss is null &&
                NPCBehaviorOverrideLoader.TryGet(attacker.type, out var parryOverrideContainer))
            {
                boss = parryOverrideContainer.BehaviorOverride as IParryable;
            }

            return boss is not null;
        }

        private static float DistanceToHitbox(NPC npc, Vector2 point)
        {
            return Vector2.Distance(npc.Hitbox.ClosestPointInRect(point), point);
        }

        private bool BeginParryWindow()
        {
            if (Player.dead || CooldownTimer > 0)
                return false;

            parrySlowTimer = ParryWindowFrames;
            CooldownTimer = MaxCooldown;
            parrySucceededThisWindow = false;
            parryGraceTimer = 0;
            Player.velocity.X *= ParryStartSlowdown;
            return true;
        }

        private bool TryBeginParry()
        {
            if (!BeginParryWindow())
                return false;

            SyncStateToOwner();
            TriggerVisualEffect(ParryVisualEffect.Started, skipOwner: true);
            return true;
        }

        private bool TryActivateSteeledBuff()
        {
            float fraction = SturdinessMeter / (float)MaxSturdinessMeter;

            if (fraction < SteeledCurveThreshold)
                return false;

            float normalized = (fraction - SteeledCurveThreshold) / (1f - SteeledCurveThreshold);
            float scale = (float)Math.Pow(normalized, 3);
            scale = Math.Max(scale, SteeledMinScale);

            int duration = (int)(FocusedBuffDuration * scale);

            steeledHealPerParry = (int)(ParryHealAmount * scale);

            Player.AddBuff(ModContent.BuffType<SteeledBuff>(), duration);

            steeledDrainStartValue = SturdinessMeter;
            steeledDrainDuration = duration;
            steeledDrainTimer = duration;

            SturdinessMeter = 0;

            SyncStateToOwner();
            TriggerVisualEffect(ParryVisualEffect.SteeledActivated, scale: scale);
            return true;
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (!IsParrying)
                return base.FreeDodge(info);

            if (info.DamageSource.SourceNPCIndex < 0 ||
                info.DamageSource.SourceNPCIndex >= Main.maxNPCs)
                return base.FreeDodge(info);

            NPC attacker = Main.npc[info.DamageSource.SourceNPCIndex];

            if (!TryGetParryable(attacker, out IParryable boss) || !boss.IsParryable)
                return base.FreeDodge(info);

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // contact hits resolve on the owner, so the server only knows if we tell it
                parrySlowTimer = 0;
                parrySucceededThisWindow = true;
                Player.SetImmuneTimeForAllTypes(ParryImmuneFrames);

                // life is owner authoritative so the heal has to happen here
                ApplySteeledHeal();

                SendParrySuccess(attacker.whoAmI);
                PlayVisualEffect(ParryVisualEffect.Succeeded, attacker.whoAmI, 1f);
                return true;
            }

            ApplyParrySuccess(attacker, boss, ownerPredicted: false);
            return true;
        }

        private void TryApplyClientParry(int attackerIndex)
        {
            if (parrySucceededThisWindow || (parrySlowTimer <= 0 && parryGraceTimer <= 0))
                return;

            if (attackerIndex < 0 || attackerIndex >= Main.maxNPCs)
                return;

            NPC attacker = Main.npc[attackerIndex];

            // not rechecking IsParryable, the window can close in the time the packet takes to get here
            if (!attacker.active || !TryGetParryable(attacker, out IParryable boss))
                return;

            if (DistanceToHitbox(attacker, Player.Center) > ServerParryRange)
                return;

            ApplyParrySuccess(attacker, boss, ownerPredicted: true);
        }

        private void ApplyParrySuccess(NPC attacker, IParryable boss, bool ownerPredicted)
        {
            boss.OnParried(Player);
            Player.SetImmuneTimeForAllTypes(ParryImmuneFrames);

            Vector2 toAttacker = attacker.Center - Player.Center;
            if (toAttacker != Vector2.Zero)
            {
                attacker.velocity += Vector2.Normalize(toAttacker) * ParryKnockback;
                attacker.netUpdate = true;
            }

            bool wasFull = SturdinessMeter >= MaxSturdinessMeter;
            parrySucceededThisWindow = true;
            SturdinessMeter = Math.Min(SturdinessMeter + SturdinessMeterGainPerParry, MaxSturdinessMeter);

            if (!ownerPredicted)
                ApplySteeledHeal();

            bool triggerMycelialExplosion = MycelialSetActive;
            if (triggerMycelialExplosion)
                ResolveMycelialExplosionDamage();

            parrySlowTimer = 0;
            parryGraceTimer = 0;
            SyncStateToOwner();
            TriggerVisualEffect(ParryVisualEffect.Succeeded, attacker.whoAmI, skipOwner: ownerPredicted);

            if (!wasFull && SturdinessMeter >= MaxSturdinessMeter)
                TriggerVisualEffect(ParryVisualEffect.MeterFull);

            if (triggerMycelialExplosion)
                TriggerVisualEffect(ParryVisualEffect.MycelialExplosion);
        }

        private void ApplySteeledHeal()
        {
            if (steeledHealPerParry <= 0 || !Player.HasBuff(ModContent.BuffType<SteeledBuff>()))
                return;

            Player.statLife = Math.Min(Player.statLife + steeledHealPerParry, Player.statLifeMax2);
            Player.HealEffect(steeledHealPerParry);
        }

        private void ResolveMycelialExplosionDamage()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 center = Player.Center;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;

                if (DistanceToHitbox(npc, center) > MycelialExplosionRadius)
                    continue;

                NPC.HitInfo hit = new NPC.HitInfo
                {
                    Damage = MycelialExplosionDamage,
                    HitDirection = Math.Sign(npc.Center.X - center.X),
                    Crit = false,
                };

                npc.StrikeNPC(hit);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendStrikeNPC(npc, hit);

                npc.AddBuff(BuffID.Poisoned, MycelialPoisonedDuration);
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server)
                SendState(toWho);
        }

        private void SyncStateToOwner()
        {
            if (Main.netMode == NetmodeID.Server)
                SendState(Player.whoAmI);
        }

        private void SendState(int toWho)
        {
            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)MessageType.SyncParryState);
            packet.Write((byte)Player.whoAmI);
            packet.Write(CooldownTimer);
            packet.Write(parrySlowTimer);
            packet.Write(parrySucceededThisWindow);
            packet.Write(SturdinessMeter);
            packet.Write(steeledHealPerParry);
            packet.Write(steeledDrainStartValue);
            packet.Write(steeledDrainDuration);
            packet.Write(steeledDrainTimer);
            packet.Send(toWho);
        }

        internal static void ReceiveSyncedState(BinaryReader reader)
        {
            int playerIndex = reader.ReadByte();
            int cooldownTimer = reader.ReadInt32();
            int parryTimer = reader.ReadInt32();
            bool parrySucceeded = reader.ReadBoolean();
            int sturdinessMeter = reader.ReadInt32();
            int healPerParry = reader.ReadInt32();
            int drainStart = reader.ReadInt32();
            int drainDuration = reader.ReadInt32();
            int drainTimer = reader.ReadInt32();

            if (playerIndex != Main.myPlayer || playerIndex >= Main.maxPlayers)
                return;

            ParryPlayer parryPlayer = Main.player[playerIndex].GetModPlayer<ParryPlayer>();
            parryPlayer.CooldownTimer = cooldownTimer;

            // we already predicted the window, the server's copy is a ping behind so it can only shorten ours
            parryPlayer.parrySlowTimer = Math.Min(parryPlayer.parrySlowTimer, parryTimer);
            parryPlayer.parrySucceededThisWindow = parrySucceeded || parryPlayer.parrySucceededThisWindow;
            parryPlayer.SturdinessMeter = sturdinessMeter;
            parryPlayer.steeledHealPerParry = healPerParry;
            parryPlayer.steeledDrainStartValue = drainStart;
            parryPlayer.steeledDrainDuration = drainDuration;
            parryPlayer.steeledDrainTimer = drainTimer;
        }

        private static void SendRequest(MessageType messageType)
        {
            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)messageType);
            packet.Send();
        }

        private static void SendParrySuccess(int attackerIndex)
        {
            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)MessageType.ParrySucceeded);
            packet.Write((short)attackerIndex);
            packet.Send();
        }

        private void TriggerVisualEffect(ParryVisualEffect effect, int attackerIndex = -1, float scale = 1f, bool skipOwner = false)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                PlayVisualEffect(effect, attackerIndex, scale);
                return;
            }

            if (Main.netMode != NetmodeID.Server)
                return;

            // owner already played it locally when they predicted it
            ModPacket packet = ModContent.GetInstance<ShatteredIllusion>().GetPacket();
            packet.Write((byte)MessageType.ParryVisualEffect);
            packet.Write((byte)effect);
            packet.Write((byte)Player.whoAmI);
            packet.Write((short)attackerIndex);
            packet.Write(scale);
            packet.Send(-1, skipOwner ? Player.whoAmI : -1);
        }

        internal static void ReceiveVisualEffect(BinaryReader reader)
        {
            ParryVisualEffect effect = (ParryVisualEffect)reader.ReadByte();
            int playerIndex = reader.ReadByte();
            int attackerIndex = reader.ReadInt16();
            float scale = reader.ReadSingle();

            if (playerIndex < 0 || playerIndex >= Main.maxPlayers || !Main.player[playerIndex].active)
                return;

            Main.player[playerIndex].GetModPlayer<ParryPlayer>().PlayVisualEffect(effect, attackerIndex, scale);
        }

        private void PlayVisualEffect(ParryVisualEffect effect, int attackerIndex, float scale)
        {
            switch (effect)
            {
                case ParryVisualEffect.Started:
                    SpawnDustExplosion(DustID.Silver, 25, 6f);
                    SpawnRing(Player.Center, DustID.Silver, 30f, 14, 3.5f, alpha: 80, scale: 1.3f);
                    SpawnParrySparks(Player.Center, count: 8, speedMin: 2f, speedMax: 4f, color: new Color(215, 225, 255, 0), sparkScale: 6f, duration: 16);
                    Flash(new Vector3(0.45f, 0.5f, 0.7f), 8);
                    windowVisualTimer = ParryWindowFrames;
                    break;

                case ParryVisualEffect.Succeeded:
                    PlayParrySuccessEffects(attackerIndex);
                    break;

                case ParryVisualEffect.MeterFull:
                    PlaySturdinessMeterFullEffects();
                    break;

                case ParryVisualEffect.SteeledActivated:
                    PlaySteeledActivationEffects(scale);
                    break;

                case ParryVisualEffect.MycelialExplosion:
                    PlayMycelialExplosionEffects();
                    break;

                case ParryVisualEffect.Failed:
                    SoundEngine.PlaySound(FailedParry, Player.position);
                    windowVisualTimer = 0;
                    SpawnFizzle();
                    break;
            }
        }

        private void Flash(Vector3 color, int frames)
        {
            flashColor = color;
            flashDuration = frames;
            flashTimer = frames;
        }

        private void Shake(Vector2 direction, float strength, int frames)
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            Cinema.Shake(Player.Center, direction.SafeNormalize(Vector2.UnitY), strength, 7f, frames);
        }

        private void PlayParrySuccessEffects(int attackerIndex)
        {
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.5f, Volume = 0.7f }, Player.position);
            SoundEngine.PlaySound(SuccessfulParry, Player.position);
            windowVisualTimer = 0;

            Vector2 contact = Player.Center;
            Vector2 hitDirection = new Vector2(Player.direction, 0f);

            if (attackerIndex >= 0 && attackerIndex < Main.maxNPCs && Main.npc[attackerIndex].active)
            {
                NPC attacker = Main.npc[attackerIndex];

                // meet in the middle of the two hitboxes instead of inside a giant boss
                Vector2 onPlayer = Player.Hitbox.ClosestPointInRect(attacker.Center);
                Vector2 onAttacker = attacker.Hitbox.ClosestPointInRect(Player.Center);
                contact = Vector2.Lerp(onPlayer, onAttacker, 0.5f);
                hitDirection = (attacker.Center - Player.Center).SafeNormalize(hitDirection);
            }

            Flash(new Vector3(1.3f, 1.05f, 0.35f), 14);
            Lighting.AddLight(contact, 1.3f, 1.05f, 0.35f);

            SpawnDustExplosion(DustID.Gold, 22, 6f, contact);
            SpawnRing(contact, DustID.Gold, 8f, 14, 5f, alpha: 40, scale: 1.5f);
            SpawnRing(Player.Center, DustID.GoldFlame, 30f, 16, 3.5f, alpha: 70, scale: 1.3f);
            SpawnShockwaveRing(contact, DustID.GoldFlame, 70f, 0.2f, alpha: 60, scale: 1.4f);

            // cone of sparks flung back at the player's side, plus a small even pop
            SpawnParrySparks(contact, count: 9, speedMin: 5f, speedMax: 9f, color: new Color(255, 250, 225, 0), sparkScale: 9f, duration: 20, direction: -hitDirection, spread: 0.8f);
            SpawnParrySparks(contact, count: 7, speedMin: 2f, speedMax: 5f, color: new Color(255, 210, 110, 0), sparkScale: 7f, duration: 18);

            Shake(hitDirection, 6f, 9);

            if (Player.whoAmI == Main.myPlayer)
            {
                impactHoldTimer = ImpactHoldFrames;
                Cinema.ZoomPunch(0.015f);
                Cinema.Flash(new Color(255, 230, 160), 0.07f);
            }
        }

        private void PlaySturdinessMeterFullEffects()
        {
            SoundEngine.PlaySound(ParryFullSound, Player.position);
            SpawnDustExplosion(DustID.GoldFlame, 36, 9f);
            Flash(new Vector3(1.4f, 1.1f, 0.4f), 22);
            SpawnRing(Player.Center, DustID.GoldFlame, 15f, 20, 7f, alpha: 30, scale: 1.7f);
            SpawnRing(Player.Center, DustID.Gold, 55f, 26, 3f, alpha: 90, scale: 1.5f);
            Shake(Vector2.UnitY, 3f, 8);
        }

        private void PlaySteeledActivationEffects(float scale)
        {
            SoundEngine.PlaySound(SteeledUseSound, Player.position);
            SpawnDustExplosion(DustID.BlueTorch, 40, 9f);
            Flash(new Vector3(0.3f, 0.5f, 1.2f) * (0.4f + 0.6f * scale), 18);

            int auraRings = 2 + (int)Math.Round(2 * scale);
            for (int i = 0; i < auraRings; i++)
            {
                float ringRadius = 35f + i * 22f;
                SpawnRing(Player.Center, DustID.BlueTorch, ringRadius, 16, 2f + i * 0.5f, alpha: 100, scale: 1.4f);
            }

            float shieldSize = MathHelper.Lerp(48f, 84f, scale);
            SpawnShieldSymbol(Player.Center, DustID.BlueTorch, shieldSize, alpha: 70, dustScale: 1.4f + scale * 0.6f, outwardSpeed: 0.6f);

            Shake(Vector2.UnitY, 2f + 2f * scale, 8);
        }

        private void PlayMycelialExplosionEffects()
        {
            Vector2 center = Player.Center;
            SoundEngine.PlaySound(SoundID.NPCDeath6, center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = 0.3f }, center);
            Flash(new Vector3(0.2f, 1.4f, 0.3f), 18);

            const int ringLayers = 4;
            for (int layer = 1; layer <= ringLayers; layer++)
            {
                float layerRadius = MycelialExplosionRadius * layer / ringLayers;
                SpawnShockwaveRing(center, DustID.GlowingMushroom, layerRadius, 0.45f, alpha: 40, scale: 1.8f);
            }

            for (int i = 0; i < 16; i++)
            {
                Dust spore = Dust.NewDustPerfect(
                    center,
                    DustID.GlowingMushroom,
                    new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-7f, -2f)),
                    Alpha: 50,
                    Scale: Main.rand.NextFloat(1.2f, 2f));
                spore.noGravity = false;
            }

            SpawnDustExplosion(DustID.GlowingMushroom, 35, 7f);
            SpawnDustExplosion(DustID.Smoke, 18, 4f);

            Shake(Vector2.UnitY, 5f, 10);
        }

        public override void PostUpdateRunSpeeds()
        {
            if (parrySlowTimer > 0)
            {
                Player.maxRunSpeed *= ParryRunSlowdown;
                Player.accRunSpeed *= ParryRunSlowdown;
                Player.runAcceleration *= ParryRunSlowdown;
            }
        }

        // little silver ring that closes in as the window runs out, so you can see how much time is left
        private void SpawnWindowRing()
        {
            float progress = windowVisualTimer / (float)ParryWindowFrames;
            float radius = MathHelper.Lerp(18f, 44f, progress);
            float offset = windowVisualTimer * 0.4f;

            for (int i = 0; i < 6; i++)
            {
                float angle = offset + MathHelper.TwoPi * i / 6f;
                Dust d = Dust.NewDustPerfect(Player.Center + angle.ToRotationVector2() * radius, DustID.Silver, Vector2.Zero, 120, default, 1f);
                d.noGravity = true;
            }
        }

        // grey puff that sags down, you whiffed
        private void SpawnFizzle()
        {
            // the silver ring dies out, so it collapses in instead of popping out
            SpawnRing(Player.Center, DustID.Smoke, 34f, 12, -1.8f, alpha: 140, scale: 1.2f);

            for (int i = 0; i < 14; i++)
            {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(18f, 22f);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.5f));

                Dust d = Dust.NewDustPerfect(pos, DustID.Smoke, vel, 150, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }

            // dull silver bits that actually fall, like the parry just dropped
            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, -0.5f));
                Dust d = Dust.NewDustPerfect(Player.Center, DustID.Silver, vel, 170, default, 1f);
                d.noGravity = false;
            }

            Flash(new Vector3(0.18f, 0.18f, 0.22f), 8);
        }

        private void SpawnDustExplosion(int dustType, int amount, float speed, Vector2? at = null)
        {
            Vector2 origin = at ?? Player.Center;

            for (int i = 0; i < amount; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(speed, speed);

                Dust dust = Dust.NewDustPerfect(
                    origin,
                    dustType,
                    dustVelocity,
                    Alpha: 100,
                    newColor: default,
                    Scale: Main.rand.NextFloat(1.3f, 2.2f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnRing(Vector2 center, int dustType, float radius, int count, float outwardSpeed, int alpha = 60, float scale = 1.6f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 spawnPos = center + direction * radius;
                Vector2 velocity = direction * outwardSpeed;

                Dust ring = Dust.NewDustPerfect(spawnPos, dustType, velocity, Alpha: alpha, Scale: scale);
                ring.noGravity = true;
            }
        }

        // ring that starts inside and drifts out to roughly the real radius, count follows circumference so outer rings arent sparse
        private void SpawnShockwaveRing(Vector2 center, int dustType, float radius, float startFraction, int alpha = 60, float scale = 1.6f)
        {
            float startRadius = radius * startFraction;
            int count = Math.Max(8, (int)(MathHelper.TwoPi * radius / 20f));
            float speed = (radius - startRadius) / ShockwaveTravelFrames;

            SpawnRing(center, dustType, startRadius, count, speed, alpha, scale);
        }

        private void SpawnParrySparks(Vector2 center, int count, float speedMin, float speedMax, Color color, float sparkScale, int duration, Vector2? direction = null, float spread = MathHelper.Pi)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = direction.HasValue
                    ? direction.Value.RotatedByRandom(spread) * Main.rand.NextFloat(speedMin, speedMax)
                    : Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(speedMin, speedMax);

                BossParticleSystem.EmberBursts.Create(new ParticleInfo(
                    position: center.ToNumerics(),
                    velocity: velocity.ToNumerics(),
                    rotation: 0f,
                    scale: new SystemVector2(sparkScale, sparkScale),
                    color: color,
                    duration: duration
                ));
            }
        }

        private static readonly Vector2[] ShieldOutlinePoints = BuildShieldOutline();

        private static Vector2[] BuildShieldOutline()
        {
            List<Vector2> points = new List<Vector2>();

            Vector2 topLeft = new Vector2(-0.8f, -1.0f);
            Vector2 topRight = new Vector2(0.8f, -1.0f);
            Vector2 rightShoulder = new Vector2(1.0f, -0.35f);
            Vector2 rightWaist = new Vector2(0.8f, 0.65f);
            Vector2 bottomTip = new Vector2(0f, 1.3f);
            Vector2 leftWaist = new Vector2(-0.8f, 0.65f);
            Vector2 leftShoulder = new Vector2(-1.0f, -0.35f);

            AddLineSegment(points, topLeft, topRight, 8);                                          // flat top edge
            AddCurveSegment(points, topRight, new Vector2(1.05f, -1.0f), rightShoulder, 8);         // small rounded shoulder, not a bulge
            AddLineSegment(points, rightShoulder, rightWaist, 10);                                  // mostly straight side
            AddCurveSegment(points, rightWaist, new Vector2(0.35f, 1.15f), bottomTip, 10);          // curves inward into the point
            AddCurveSegment(points, bottomTip, new Vector2(-0.35f, 1.15f), leftWaist, 10);          // mirrored curve out of the point
            AddLineSegment(points, leftWaist, leftShoulder, 10);                                    // mostly straight side
            AddCurveSegment(points, leftShoulder, new Vector2(-1.05f, -1.0f), topLeft, 8);          // mirrored shoulder

            return ResampleEvenly(points, ShieldDustCount);
        }

        // the segments above have different point densities so the dust came out lumpy, this evens it out by arc length
        private static Vector2[] ResampleEvenly(List<Vector2> loop, int count)
        {
            float total = 0f;
            for (int i = 0; i < loop.Count; i++)
                total += Vector2.Distance(loop[i], loop[(i + 1) % loop.Count]);

            Vector2[] result = new Vector2[count];
            float step = total / count;
            int segment = 0;
            float segmentStart = 0f;

            for (int n = 0; n < count; n++)
            {
                float target = n * step;

                while (true)
                {
                    Vector2 a = loop[segment];
                    Vector2 b = loop[(segment + 1) % loop.Count];
                    float length = Vector2.Distance(a, b);

                    if (target <= segmentStart + length || segment == loop.Count - 1)
                    {
                        float t = length > 0f ? MathHelper.Clamp((target - segmentStart) / length, 0f, 1f) : 0f;
                        result[n] = Vector2.Lerp(a, b, t);
                        break;
                    }

                    segmentStart += length;
                    segment++;
                }
            }

            return result;
        }

        private static void AddLineSegment(List<Vector2> points, Vector2 start, Vector2 end, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                points.Add(Vector2.Lerp(start, end, t));
            }
        }

        private static void AddCurveSegment(List<Vector2> points, Vector2 start, Vector2 control, Vector2 end, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                Vector2 a = Vector2.Lerp(start, control, t);
                Vector2 b = Vector2.Lerp(control, end, t);
                points.Add(Vector2.Lerp(a, b, t));
            }
        }

        private void SpawnShieldSymbol(Vector2 center, int dustType, float size, int alpha = 70, float dustScale = 1.6f, float outwardSpeed = 1.5f)
        {
            foreach (Vector2 unitPoint in ShieldOutlinePoints)
            {
                Vector2 spawnPos = center + unitPoint * size;

                Vector2 direction = unitPoint == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(unitPoint);
                Vector2 velocity = direction * outwardSpeed;

                Dust glyph = Dust.NewDustPerfect(spawnPos, dustType, velocity, Alpha: alpha, Scale: dustScale);
                glyph.noGravity = true;
            }
        }
    }
}