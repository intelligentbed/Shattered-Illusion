using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ParticleLibrary.Core;
using ParticleLibrary.Core.V3;
using Terraria;
using Terraria.ModLoader;
using PLayer = ParticleLibrary.Core.Layer;

namespace ShatteredIllusion.Common.Visuals.ThreeD
{
    public interface IThreeDDrawable
    {
        bool Alive { get; }
        ThreeDLayer Layer { get; }
        float Depth { get; }

        void Draw(SpriteBatch sb, in View3 view, Rectangle bounds);

        void Tick() { }

        void DrawShadow(SpriteBatch sb) { }
    }

    // Keeps every live 3D object in one registry and draws it through ParticleLibrary's layer hooks,
    // so nothing scans the projectile array and nothing is detoured.
    public class ThreeDRenderer : ModSystem
    {
        private const int InstanceCapacity = 1024;

        // List keeps a stable order for ticking and tie-breaking; the set makes the "already added?" check O(1).
        private static readonly List<IThreeDDrawable> drawables = new();
        private static readonly HashSet<IThreeDDrawable> members = new();
        private static readonly List<IThreeDDrawable>[] buckets = { new(), new(), new() };
        private static readonly InstancedShapeBuffer[] instanced = new InstancedShapeBuffer[3];

        private static int version;
        private static int bucketVersion = -1;
        private static uint bucketStamp = uint.MaxValue;

        public static void Add(IThreeDDrawable drawable)
        {
            if (Main.dedServ || !members.Add(drawable))
                return;

            drawables.Add(drawable);
            version++;
        }

        public static void Remove(IThreeDDrawable drawable)
        {
            // The list scan only runs when the drawable really was registered.
            if (members.Remove(drawable) && drawables.Remove(drawable))
                version++;
        }

        public static void ClearAll()
        {
            drawables.RemoveAll(d =>
            {
                if (d is not Shape3D { AutoTick: true })
                    return false;

                members.Remove(d);
                return true;
            });

            version++;
        }

        // Fire-and-forget visual shape: moves, spins, expires and follows its anchor on its own.
        public static Shape3D Spawn(Shape3D shape)
        {
            shape.AutoTick = true;
            Add(shape);
            return shape;
        }

        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;

            ThreeDLayer[] layers = { ThreeDLayer.BehindWorld, ThreeDLayer.BehindNPCs, ThreeDLayer.OverPlayers };
            PLayer[] hooks = { PLayer.BeforeWalls, PLayer.BeforeNPCs, PLayer.BeforeItems };

            for (int i = 0; i < 3; i++)
            {
                instanced[i] = new InstancedShapeBuffer(layers[i], InstanceCapacity);
                ParticleManagerV3.RegisterRenderable(hooks[i], instanced[i]);
            }
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;

            DrawHooks.Hook(PLayer.BeforeWalls, DrawBehindWorld);
            DrawHooks.Hook(PLayer.BeforeNPCs, DrawBehindNPCs);
            DrawHooks.Hook(PLayer.BeforeItems, DrawOverPlayers);
        }

        public override void Unload()
        {
            if (!Main.dedServ)
            {
                DrawHooks.UnHook(PLayer.BeforeWalls, DrawBehindWorld);
                DrawHooks.UnHook(PLayer.BeforeNPCs, DrawBehindNPCs);
                DrawHooks.UnHook(PLayer.BeforeItems, DrawOverPlayers);
            }

            drawables.Clear();
            members.Clear();

            foreach (var bucket in buckets)
                bucket.Clear();

            for (int i = 0; i < instanced.Length; i++)
                instanced[i] = null;

            ThreeDMath.CameraCenterOverride = null;
            ThreeDDraw.Unload();
        }

        public override void OnWorldUnload()
        {
            drawables.Clear();
            members.Clear();
            version++;
        }

        public override void PostUpdateEverything()
        {
            for (int i = drawables.Count - 1; i >= 0; i--)
            {
                IThreeDDrawable d = drawables[i];

                if (!d.Alive)
                {
                    drawables.RemoveAt(i);
                    members.Remove(d);
                    version++;
                    continue;
                }

                d.Tick();
            }
        }

        // Rebuilt at most once per game tick: layer buckets sorted far to near, plus the instancing budget.
        private static void Rebuild()
        {
            if (bucketVersion == version && bucketStamp == Main.GameUpdateCount)
                return;

            bucketVersion = version;
            bucketStamp = Main.GameUpdateCount;

            foreach (var bucket in buckets)
                bucket.Clear();

            foreach (IThreeDDrawable d in drawables)
            {
                if (!d.Alive)
                    continue;

                buckets[(int)d.Layer].Add(d);

                if (d is Shape3D s)
                {
                    s.UsingInstancing = s.Instanced;

                    if (s.Straddles)
                        buckets[(int)(s.Layer == ThreeDLayer.BehindNPCs ? ThreeDLayer.OverPlayers : ThreeDLayer.BehindNPCs)].Add(d);
                }
            }

            for (int layer = 0; layer < buckets.Length; layer++)
            {
                List<IThreeDDrawable> list = buckets[layer];
                int budget = instanced[layer]?.GetMaxInstances() ?? 0;

                for (int i = 1; i < list.Count; i++)
                {
                    IThreeDDrawable v = list[i];
                    int j = i - 1;

                    while (j >= 0 && list[j].Depth > v.Depth)
                    {
                        list[j + 1] = list[j];
                        j--;
                    }

                    list[j + 1] = v;
                }

                foreach (IThreeDDrawable d in list)
                {
                    if (d is not Shape3D { UsingInstancing: true } shape)
                        continue;

                    // only ever turns off, so a shape in two buckets is instanced in both or neither
                    if (shape.Count <= budget)
                        budget -= shape.Count;
                    else
                        shape.UsingInstancing = false;
                }
            }
        }

        private static Rectangle ScreenBounds => new(0, 0, Main.screenWidth, Main.screenHeight);

        private static void DrawBehindWorld(PLayer _) => DrawBucket(ThreeDLayer.BehindWorld);

        private static void DrawBehindNPCs(PLayer _)
        {
            Rebuild();

            foreach (IThreeDDrawable d in buckets[(int)ThreeDLayer.BehindNPCs])
                d.DrawShadow(Main.spriteBatch);

            foreach (IThreeDDrawable d in buckets[(int)ThreeDLayer.OverPlayers])
                d.DrawShadow(Main.spriteBatch);

            DrawBucket(ThreeDLayer.BehindNPCs);
        }

        private static void DrawOverPlayers(PLayer _) => DrawBucket(ThreeDLayer.OverPlayers);

        private static void DrawBucket(ThreeDLayer layer)
        {
            if (Main.gameMenu)
                return;

            Rebuild();

            List<IThreeDDrawable> list = buckets[(int)layer];

            if (list.Count == 0)
                return;

            View3 view = View3.World;
            Rectangle bounds = ScreenBounds;

            foreach (IThreeDDrawable d in list)
            {
                if (!d.Alive || d is Shape3D { UsingInstancing: true })
                    continue;

                if (d is Shape3D shape)
                    shape.Draw(Main.spriteBatch, view, bounds, layer);
                else
                    d.Draw(Main.spriteBatch, view, bounds);
            }
        }

        internal static List<IThreeDDrawable> Bucket(ThreeDLayer layer)
        {
            Rebuild();
            return buckets[(int)layer];
        }

        internal static Rectangle Bounds => ScreenBounds;
    }
}
