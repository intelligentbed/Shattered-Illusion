using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using ShatteredIllusion.Content.Dusts;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Projectiles.Mage
{
    internal class StarryProjectile : ModProjectile
    {
        private bool IsMiniStar => Projectile.ai[0] >= 1f;
        private int StarIndex => (int)Projectile.ai[0] - 1;

        // ai[1] on a mini star = (whoAmI of the previous star in the chain) + 1, or 0 for "no link".
        private int LinkedStarId => (int)Projectile.ai[1] - 1;

        private const int BoltMaxTime = 45;
        private const int MiniStarCount = 5;

        private const int HoverTime = 26;        // ticks the first star hangs before launching
        private const int StaggerTime = 7;       // each following star waits this much longer
        private const int LaunchedLifetime = 70;
        private const float LaunchSpeed = 12f;
        private const float SeekRange = 650f;

        // localAI[0] = initialised, localAI[1] = hover timer, localAI[2] = launched
        private ref float HoverTimer => ref Projectile.localAI[1];

        private bool Launched
        {
            get => Projectile.localAI[2] == 1f;
            set => Projectile.localAI[2] = value ? 1f : 0f;
        }

        private static readonly Color Gold = new Color(255, 214, 110);
        private static readonly Color DeepBlue = new Color(70, 100, 230);

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = BoltMaxTime;
            Projectile.light = 0.6f;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.alpha = 0;
        }

        // Hovering stars are just decoration until they launch.
        public override bool? CanDamage()
        {
            if (IsMiniStar && !Launched)
                return false;
            return null;
        }

        public override void AI()
        {
            if (IsMiniStar)
                MiniStarAI();
            else
                BoltAI();
        }

        private void BoltAI()
        {
            Projectile.rotation += 0.15f;
            Projectile.velocity.Y += 0.02f;

            if (Main.rand.NextBool(2))
            {
                Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 1.1f);
            }

            if (Main.rand.NextBool(5))
            {
                Dust sparkle = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f),
                    DustID.Enchanted_Gold, Main.rand.NextVector2Circular(0.6f, 0.6f), 0, default, 0.9f);
                sparkle.noGravity = true;
            }
        }

        private void MiniStarAI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.Resize(8, 8);
                Projectile.light = 0.35f;
                Projectile.extraUpdates = 0;
                Projectile.tileCollide = false; // only solid once launched
                Projectile.timeLeft = HoverTime + StarIndex * StaggerTime + LaunchedLifetime;
            }

            if (!Launched)
            {
                // drift to a stop, spinning slowly, twinkling
                Projectile.velocity *= 0.9f;
                Projectile.rotation += 0.12f;
                HoverTimer++;

                if (Main.rand.NextBool(6))
                {
                    Dust twinkle = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                        DustID.Enchanted_Gold, Vector2.Zero, 0, default, 0.7f);
                    twinkle.noGravity = true;
                }

                if (HoverTimer >= HoverTime + StarIndex * StaggerTime)
                    Launch();
            }
            else
            {
                Projectile.rotation += 0.25f;

                if (Main.rand.NextBool(2))
                {
                    Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 0.8f);
                }
            }
        }

        private void Launch()
        {
            Launched = true;
            Projectile.tileCollide = true;
            Projectile.ai[1] = 0f; // cut the constellation line

            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.45f, Pitch = 0.2f + StarIndex * 0.12f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.Enchanted_Gold, 6, 2f, 0.9f);

            // Only the owner decides where it goes, then syncs it to everyone else.
            if (Projectile.owner != Main.myPlayer)
                return;

            NPC target = FindTarget();
            Vector2 aimPoint = target != null ? target.Center : Main.MouseWorld;

            Projectile.velocity = (aimPoint - Projectile.Center).SafeNormalize(Vector2.UnitY) * LaunchSpeed;
            Projectile.netUpdate = true;
        }

        private NPC FindTarget()
        {
            NPC best = null;
            float bestDistance = SeekRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        // Bursting only happens here, in OnKill, so it can never happen twice.
        // (The old version burst in OnHitNPC / OnTileCollide AND OnKill, spawning 10 stars.)
        public override void OnKill(int timeLeft)
        {
            if (IsMiniStar)
            {
                for (int i = 0; i < 4; i++)
                {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        ModContent.DustType<LanternDust>(), 0f, 0f, 0, default, 1f);
                }

                WeaponFX.DustRing(Projectile.Center, DustID.Enchanted_Gold, 5, 2.2f, 0.9f);
            }
            else
            {
                Burst();
            }
        }

        private void Burst()
        {
            SoundEngine.PlaySound(SoundID.Item29, Projectile.position);
            WeaponFX.DustRing(Projectile.Center, DustID.Enchanted_Gold, 12, 3.2f, 1.1f);

            if (Projectile.owner != Main.myPlayer)
                return;

            float startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            int previous = -1;

            for (int i = 0; i < MiniStarCount; i++)
            {
                float angle = startAngle + MathHelper.TwoPi * i / MiniStarCount + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 5.5f);

                int id = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    Projectile.type,
                    System.Math.Max(1, (int)(Projectile.damage * 0.5f)),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    ai0: 1f + i,
                    ai1: previous + 1f);

                previous = id < Main.maxProjectiles ? id : -1;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float twinkle = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.whoAmI);

            float spriteScale;

            if (IsMiniStar)
            {
                DrawConstellationLine();

                WeaponFX.DrawGlowTrail(Projectile, Gold, DeepBlue, 0.28f, 0.8f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Gold, 0.7f * twinkle), 0.5f);
                spriteScale = 0.8f;
            }
            else
            {
                WeaponFX.DrawGlowTrail(Projectile, Gold, DeepBlue, 0.55f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Gold, 0.55f * twinkle), 1f);
                WeaponFX.DrawGlow(center, WeaponFX.Additive(Color.White, 0.5f * twinkle), 0.4f);
                spriteScale = 1f;
            }

            Main.EntitySpriteDraw(texture, center, null, Color.White * Projectile.Opacity, Projectile.rotation,
                texture.Size() * 0.5f, Projectile.scale * spriteScale * twinkle, SpriteEffects.None, 0);

            return false;
        }

        private void DrawConstellationLine()
        {
            if (Launched)
                return;

            int linkId = LinkedStarId;
            if (linkId < 0 || linkId >= Main.maxProjectiles)
                return;

            Projectile other = Main.projectile[linkId];
            bool validPartner = other.active && other.type == Type && other.owner == Projectile.owner
                                && other.ai[0] >= 1f && other.localAI[2] == 0f;
            if (!validPartner)
                return;

            float flicker = 0.7f + 0.3f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI);
            WeaponFX.DrawLine(Projectile.Center, other.Center, WeaponFX.Additive(Gold, 0.55f * flicker), 1.5f);
            WeaponFX.DrawLine(Projectile.Center, other.Center, WeaponFX.Additive(DeepBlue, 0.35f * flicker), 4f);
        }
    }
}