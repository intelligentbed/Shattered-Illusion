using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Common.Visuals;
using Terraria;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    // King Slime's own FxDefs, in the same style as Fx. Everything here is particles only (Extra), so it follows the
    // EffectDensity / ImpactParticles settings and the shared effect cap. Camera work stays in the boss's cinematic beats.
    public static class KingSlimeFx
    {
        private static readonly Color Gel = new Color(120, 235, 255);
        private static readonly Color Echo = new Color(120, 255, 220);

        // Life for a converge streak spawned now so it arrives exactly on `frame`.
        private static int Until(int frame, FxContext c) => System.Math.Max(1, frame - c.Age);

        // Weapon form begins: streaks pull into the slime as it squashes into the orb. Play with PlayOn(npc).
        public static readonly FxDef WeaponGather = new FxDef()
            .Tinted(Gel)
            .Every(0, 6, 2, static c => VisualFX.Converge(c.Pos, 150f * c.Scale, 3, c.Tint, Until(14, c)))
            .Extra(14, static c => VisualFX.Flash(c.Pos, 150f * c.Scale, c.Hot, 10));

        // The weapon has finished growing out of the orb. scale ~0.6 for the quieter weapon -> weapon handoff.
        public static readonly FxDef WeaponBloom = new FxDef()
            .Tinted(Gel)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos, 200f * c.Scale, c.Hot, 10);
                VisualFX.Ring(c.Pos, 170f * c.Scale, c.Tint, 16);
                VisualFX.Sparks(c.Pos, Vector2.UnitX, MathHelper.Pi, 10, 3f, 8f, c.Tint, 8f, 18);
            })
            .Extra(4, static c => VisualFX.Ring(c.Pos, 260f * c.Scale, c.Tint, 22));

        // Echo Dive clone pops in at the ceiling. pos = spawn point.
        public static readonly FxDef EchoSpawn = new FxDef()
            .Tinted(Echo)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos, 90f * c.Scale, c.Hot, 8);
                VisualFX.Ring(c.Pos, 80f * c.Scale, c.Tint, 12);
                VisualFX.Sparks(c.Pos, Vector2.UnitX, MathHelper.Pi, 6, 2f, 5f, c.Tint, 7f, 14);
            });

        // Echo Dive clone rebounds off the floor. pos = floor contact, dir = horizontal travel direction.
        // The ground-ring half comes from Cinema.Impact, so this only adds the flash, sparks and skid streaks.
        public static readonly FxDef EchoBounce = new FxDef()
            .Tinted(Echo)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 8f), 120f * c.Scale, c.Hot, 8);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 1.2f, 8, 2.5f, 6f, c.Tint, 8f, 16);
                VisualFX.Streaks(c.Pos - new Vector2(0f, 2f), c.Dir, 0.2f, 4, 5f, 9f, 60f * c.Scale, 7f, c.Tint, 16);
            });

        // Shadow Dive: the ninja gathers energy while hovering and it lands exactly as he launches (frame 18).
        // Start it 18 frames before the plunge, with PlayOn(npc).
        public static readonly FxDef DiveCharge = new FxDef()
            .Tinted(Echo)
            .Every(0, 9, 2, static c => VisualFX.Converge(c.Pos, 150f * c.Scale, 3, c.Tint, Until(18, c)))
            .Extra(18, static c => VisualFX.Flash(c.Pos, 160f * c.Scale, c.Hot, 10));

        // The ninja bursts out of the floor (transform + ambush re-emerge). pos = ground contact. Bright enough to read at a glance:
        // a flash, a ground ring, a vertical column of streaks and a fan of gel sparks.
        public static readonly FxDef GroundEmerge = new FxDef()
            .Tinted(Gel)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 20f), 170f * c.Scale, c.Hot, 10);
                VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 130f * c.Scale, c.Tint, 16, 0.3f);
                VisualFX.Streaks(c.Pos, -Vector2.UnitY, 0.35f, 6, 6f, 12f, 70f * c.Scale, 6f, c.Tint, 18);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 0.9f, 12, 3f, 9f, c.Tint, 8f, 20);
            })
            .Extra(4, static c => VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 210f * c.Scale, c.Tint, 22, 0.3f));

        // Huge jump launch: he springs off the floor. pos = ground contact.
        public static readonly FxDef HugeLaunch = new FxDef()
            .Tinted(Gel)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 20f), 190f * c.Scale, c.Hot, 10);
                VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 170f * c.Scale, c.Tint, 16, 0.3f);
                VisualFX.Streaks(c.Pos, -Vector2.UnitY, 0.5f, 8, 6f, 14f, 90f * c.Scale, 6f, c.Tint, 20);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 1.0f, 14, 3f, 9f, c.Tint, 8f, 22);
            });

        // Huge jump hover: streaks converge on him and flash as the slam starts (24 frames). Start with PlayOn(npc).
        public static readonly FxDef HugeCharge = new FxDef()
            .Tinted(Gel)
            .Every(0, 12, 2, static c => VisualFX.Converge(c.Pos, 180f * c.Scale, 4, c.Tint, Until(24, c)))
            .Extra(24, static c => VisualFX.Flash(c.Pos, 200f * c.Scale, c.Hot, 10));

        // Huge jump slam. pos = ground contact. The camera shake / ground ring come from the boss's own beats.
        public static readonly FxDef HugeImpact = new FxDef()
            .Tinted(Gel)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 24f), 280f * c.Scale, c.Hot, 12);
                VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 220f * c.Scale, c.Tint, 18, 0.3f);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 1.3f, 16, 4f, 11f, c.Tint, 9f, 24);
                VisualFX.Streaks(c.Pos - new Vector2(0f, 2f), Vector2.UnitX, 0.15f, 4, 8f, 14f, 100f * c.Scale, 7f, c.Tint, 18);
                VisualFX.Streaks(c.Pos - new Vector2(0f, 2f), -Vector2.UnitX, 0.15f, 4, 8f, 14f, 100f * c.Scale, 7f, c.Tint, 18);
            })
            .Extra(5, static c => VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 360f * c.Scale, c.Tint, 26, 0.3f));

        // A gel geyser erupts. pos = ground.
        public static readonly FxDef GeyserBurst = new FxDef()
            .Tinted(Gel)
            .Extra(0, static c =>
            {
                VisualFX.Flash(c.Pos - new Vector2(0f, 16f), 120f * c.Scale, c.Hot, 8);
                VisualFX.Ring(c.Pos - new Vector2(0f, 3f), 90f * c.Scale, c.Tint, 14, 0.3f);
                VisualFX.Sparks(c.Pos, -Vector2.UnitY, 0.6f, 8, 3f, 8f, c.Tint, 8f, 18);
            });
    }

    // Builds the defs at load (like VisualFXSystem does for Fx) so a bad definition fails here, not mid-fight.
    public class KingSlimeFxSystem : ModSystem
    {
        public override void OnModLoad()
        {
            if (!Main.dedServ)
                RuntimeHelpers.RunClassConstructor(typeof(KingSlimeFx).TypeHandle);
        }
    }
}