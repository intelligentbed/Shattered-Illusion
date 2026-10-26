using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles.Ranger
{
    /// <summary>
    /// Two of these fire at once, curving out to either side and converging on your cursor
    /// like a pair of mandibles. NEW: the right-hand one "snaps shut" where they meet,
    /// dealing a small area hit, so aiming the pincers AT an enemy pays off.
    /// </summary>
    internal class MandibleShooterBullet : ModProjectile
    {
        private const int TravelTime = 45;
        private const float MaxSpread = 90f;

        private const int SnapTime = 6;           // ticks the snap lingers
        private const int SnapSize = 110;         // hitbox size (pixels) of the snap
        private const float SnapDamageMult = 0.6f;

        public Vector2 Start;
        public Vector2 Target;

        /// <summary>whoAmI of the other bullet in this pair (-1 = none). Synced.</summary>
        public int PartnerWho = -1;

        private float Side => Projectile.ai[0];

        // localAI[0] == 1 once this bullet has turned into the snap
        private bool Snapping => Projectile.localAI[0] == 1f;

        private static readonly Color Sand = new Color(255, 214, 140);
        private static readonly Color Deep = new Color(190, 120, 50);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = TravelTime + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            if (Snapping)
            {
                SnapAI();
                return;
            }

            if (Projectile.ai[1] == 0f && Start == Target)
            {
                Start = Projectile.Center;
                Target = Projectile.Center + Vector2.UnitX * 200f;
            }

            float t = MathHelper.Clamp(Projectile.ai[1] / TravelTime, 0f, 1f);

            Vector2 toTarget = Target - Start;
            Vector2 forward = toTarget.SafeNormalize(Vector2.UnitX);
            Vector2 right = forward.RotatedBy(MathHelper.PiOver2);

            Vector2 basePos = Vector2.Lerp(Start, Target, t);

            float amplitude = MaxSpread * (float)System.Math.Sin(t * MathHelper.Pi);
            Vector2 offset = right * amplitude * Side;

            Vector2 newPos = basePos + offset;

            Vector2 delta = newPos - Projectile.Center;
            if (delta != Vector2.Zero)
                Projectile.rotation = delta.ToRotation() + MathHelper.PiOver2;

            Projectile.Center = newPos;
            Projectile.velocity = Vector2.Zero;

            // dust
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Sandstorm, delta * 0.1f, 0, default, 1.1f);
            trail.noGravity = true;
            trail.fadeIn = 0.5f;

            if (Main.rand.NextBool(3))
            {
                Dust grit = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.Sand, -delta * 0.15f, 0, default, 0.9f);
                grit.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.38f, 0.18f);

            Projectile.ai[1] += 1f;

            if (t >= 1f)
            {
                if (Side > 0f)
                    BeginSnap();   // only one of the pair snaps, so the area hit isn't doubled
                else
                    Projectile.Kill();
            }
        }

        private void BeginSnap()
        {
            Projectile.localAI[0] = 1f;
            Projectile.Resize(SnapSize, SnapSize);
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.damage = (int)(Projectile.damage * SnapDamageMult);
            Projectile.timeLeft = SnapTime;
            Projectile.velocity = Vector2.Zero;

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Sand, 20, 5f, 1.4f);
            WeaponFX.DustRing(Projectile.Center, DustID.Sandstorm, 12, 2.5f, 1.2f);
        }

        private void SnapAI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.4f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.25f);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Start.X);
            writer.Write(Start.Y);
            writer.Write(Target.X);
            writer.Write(Target.Y);
            writer.Write(PartnerWho);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Start = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Target = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            PartnerWho = reader.ReadInt32();
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 12; i++)
            {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (Snapping)
            {
                float life = Projectile.timeLeft / (float)SnapTime; // 1 -> 0
                float size = MathHelper.Lerp(1.2f, 2.2f, 1f - life);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Sand, 0.9f * life), size);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Color.White, 0.6f * life), size * 0.5f);
                return false;
            }

            DrawJawLine();

            WeaponFX.DrawGlowTrail(Projectile, Sand, Deep, 0.45f);
            WeaponFX.DrawGlow(center, WeaponFX.Additive(Sand, 0.55f), 0.7f);

            Main.EntitySpriteDraw(texture, center, null, Color.White * Projectile.Opacity, Projectile.rotation,
                texture.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        // A thin glowing thread between the two bullets that gets brighter as the jaws close.
        // Only the Side > 0 bullet draws it, so it isn't drawn twice.
        private void DrawJawLine()
        {
            if (Side <= 0f || PartnerWho < 0 || PartnerWho >= Main.maxProjectiles)
                return;

            Projectile other = Main.projectile[PartnerWho];
            if (!other.active || other.type != Type || other.owner != Projectile.owner)
                return;

            if (other.ModProjectile is not MandibleShooterBullet partner || partner.PartnerWho != Projectile.whoAmI || partner.Snapping)
                return;

            float progress = MathHelper.Clamp(Projectile.ai[1] / TravelTime, 0f, 1f);
            WeaponFX.DrawLine(Projectile.Center, other.Center, WeaponFX.Additive(Sand, 0.2f + 0.5f * progress), 2f);
        }
    }
}