using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ShatteredIllusion.Common.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Items.Weapons.Melee
{
    /// <summary>
    /// A thrown chakram made from King Slime's crown. Each throw uses the NEXT gem in the
    /// crown (Ruby -> Emerald -> Sapphire -> Ruby ...), and each gem changes how the throw behaves:
    ///   Ruby     - heavy: bigger, +35% damage, big knockback
    ///   Emerald  - bounces between up to 3 nearby enemies before coming back
    ///   Sapphire - flies out, then hovers and spins, slowing and dragging enemies toward it
    /// Max 2 in the air at once.
    ///
    /// Expects the sprite at Content/Items/Weapons/Melee/ChaCrown.png (the projectile reuses it).
    /// </summary>
    public class ChaCrown : ModItem
    {
        private int nextGem;

        public override void SetDefaults()
        {
            Item.width = 36;
            Item.height = 36;

            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 5f;
            Item.crit = 4;

            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;

            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<ChaCrownProjectile>();
            Item.shootSpeed = 13f;

            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Blue;
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] < 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            int gem = nextGem;
            nextGem = (nextGem + 1) % 3;

            float damageMult = gem == ChaCrownProjectile.Ruby ? 1.35f : gem == ChaCrownProjectile.Sapphire ? 0.85f : 1f;
            float knockbackMult = gem == ChaCrownProjectile.Ruby ? 1.6f : 1f;

            Projectile.NewProjectile(source, position, velocity, type, (int)(damage * damageMult),
                knockback * knockbackMult, player.whoAmI, ai0: gem);

            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            float rotation, float scale, int whoAmI)
        {
            WeaponFX.DrawWorldAura(Item, rotation, scale, new Color(255, 210, 90));
        }
    }

    public class ChaCrownProjectile : ModProjectile
    {
        public const int Ruby = 0;
        public const int Emerald = 1;
        public const int Sapphire = 2;

        // Re-uses the item's sprite so you only need one .png.
        public override string Texture => "ShatteredIllusion/Content/Items/Weapons/Melee/ChaCrown";

        // ai[0] = which gem, ai[1] = state (0 = flying out, 1 = hovering (sapphire), 2 = returning)
        private int GemType => (int)Projectile.ai[0];
        private ref float State => ref Projectile.ai[1];

        // localAI[0] = out timer, [1] = bounces left (emerald), [2] = hover timer
        private ref float Timer => ref Projectile.localAI[0];
        private ref float BouncesLeft => ref Projectile.localAI[1];
        private ref float HoverTimer => ref Projectile.localAI[2];

        private const float StateOut = 0f;
        private const float StateHover = 1f;
        private const float StateReturn = 2f;

        private const int OutTime = 30;
        private const int RubyOutTime = 26;
        private const int HoverTime = 75;
        private const int MaxBounces = 3;
        private const float BounceRange = 420f;
        private const float BounceSpeed = 14f;
        private const float ReturnSpeed = 18f;
        private const float PullRange = 170f;
        private const float SpriteDrawSize = 40f; // on-screen size in pixels, whatever the .png size is

        private bool initialized;

        private static readonly Color Gold = new Color(255, 210, 90);
        private static readonly Color RubyColor = new Color(255, 80, 80);
        private static readonly Color EmeraldColor = new Color(80, 255, 130);
        private static readonly Color SapphireColor = new Color(90, 160, 255);

        private Color GemColor => GemColorOf(GemType);

        private static Color GemColorOf(int gem) => gem == Ruby ? RubyColor : gem == Emerald ? EmeraldColor : SapphireColor;

        private static int GemDust(int gem) => gem == Ruby ? DustID.RedTorch : gem == Emerald ? DustID.GreenTorch : DustID.BlueTorch;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;

            // spinning blade: can hit the same enemy again every few ticks
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            if (!initialized)
                Initialize();

            Projectile.rotation += 0.5f;

            if (Projectile.timeLeft % 18 == 0)
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.5f }, Projectile.Center);

            Vector3 light = GemColor.ToVector3() * 0.5f;
            Lighting.AddLight(Projectile.Center, light.X, light.Y, light.Z);

            if (State == StateOut)
                OutAI();
            else if (State == StateHover)
                HoverAI();
            else
                ReturnAI(owner);

            // gem sparkle
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    GemDust(GemType), Vector2.Zero, 0, default, 0.9f);
                d.noGravity = true;
            }
        }

        private void Initialize()
        {
            initialized = true;
            BouncesLeft = GemType == Emerald ? MaxBounces : 0;

            if (GemType == Ruby)
            {
                Projectile.Resize(46, 46);
                Projectile.scale = 1.2f;
            }
        }

        private void OutAI()
        {
            Timer++;

            int outTime = GemType == Ruby ? RubyOutTime : OutTime;

            if (Timer > outTime - 8)
                Projectile.velocity *= 0.9f;

            if (Timer >= outTime)
                EndOutPhase();
        }

        private void EndOutPhase()
        {
            if (GemType == Sapphire)
                StartHover();
            else
                StartReturn();
        }

        private void StartHover()
        {
            State = StateHover;
            HoverTimer = 0f;
            Projectile.localNPCHitCooldown = 10;
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f }, Projectile.Center);
            WeaponFX.DustRing(Projectile.Center, DustID.BlueTorch, 14, 3f, 1.2f);

            if (Projectile.owner == Main.myPlayer)
                Projectile.netUpdate = true;
        }

        private void StartReturn()
        {
            State = StateReturn;
            Projectile.tileCollide = false;
            Projectile.localNPCHitCooldown = 12;

            if (Projectile.owner == Main.myPlayer)
                Projectile.netUpdate = true;
        }

        // Sapphire: sit still, spin, slow and drag enemies in.
        private void HoverAI()
        {
            Projectile.velocity *= 0.85f;
            HoverTimer++;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f)
                    continue;

                Vector2 toCenter = Projectile.Center - npc.Center;
                float distance = toCenter.Length();
                if (distance > PullRange || distance < 24f)
                    continue;

                npc.velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.55f * npc.knockBackResist;
            }

            if (Main.rand.NextBool(2))
            {
                Vector2 edge = Main.rand.NextVector2CircularEdge(PullRange, PullRange);
                Dust d = Dust.NewDustPerfect(Projectile.Center + edge, DustID.BlueTorch, -edge * 0.04f, 0, default, 1f);
                d.noGravity = true;
            }

            if (HoverTimer >= HoverTime)
                StartReturn();
        }

        private void ReturnAI(Player owner)
        {
            Vector2 toPlayer = owner.Center - Projectile.Center;

            if (toPlayer.Length() < 28f)
            {
                Projectile.Kill(); // caught
                return;
            }

            Projectile.velocity += toPlayer.SafeNormalize(Vector2.Zero) * 1.3f;
            if (Projectile.velocity.Length() > ReturnSpeed)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * ReturnSpeed;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (State == StateOut)
            {
                SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
                WeaponFX.DustCone(Projectile.Center, -oldVelocity, 0.9f, GemDust(GemType), 8, 2f, 5f, 1.1f);

                if (GemType == Sapphire)
                {
                    Projectile.velocity = Vector2.Zero;
                    StartHover();
                }
                else
                {
                    Projectile.velocity = -oldVelocity * 0.3f;
                    StartReturn();
                }
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Vector2 away = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            WeaponFX.DustCone(Projectile.Center, away, 0.8f, GemDust(GemType), 6, 2f, 5f, 1.1f);
            WeaponFX.DustCone(Projectile.Center, away, 0.8f, DustID.Enchanted_Gold, 4, 1.5f, 4f, 1f);

            switch (GemType)
            {
                case Ruby:
                    WeaponFX.DustRing(Projectile.Center, DustID.RedTorch, 10, 4f, 1.3f);
                    break;

                case Emerald:
                    TryBounce(target);
                    break;

                default:
                    target.AddBuff(BuffID.Slow, 120);
                    break;
            }
        }

        private void TryBounce(NPC hit)
        {
            if (Projectile.owner != Main.myPlayer || State != StateOut)
                return;

            NPC next = FindBounceTarget(hit);

            if (BouncesLeft > 0f && next != null)
            {
                BouncesLeft--;
                Projectile.velocity = (next.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * BounceSpeed;
                Timer = System.Math.Min(Timer, OutTime - 16f); // make sure it has time to reach the next one
                Projectile.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f }, Projectile.Center);
                WeaponFX.DustRing(Projectile.Center, DustID.GreenTorch, 8, 3f, 1.1f);
            }
            else
            {
                StartReturn();
            }
        }

        private NPC FindBounceTarget(NPC exclude)
        {
            NPC best = null;
            float bestDistance = BounceRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy(Projectile))
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

        public override void OnKill(int timeLeft)
        {
            if (State == StateReturn)
            {
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.5f }, Projectile.Center);
                WeaponFX.DustRing(Projectile.Center, DustID.Enchanted_Gold, 10, 3f, 1.1f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            float drawScale = SpriteDrawSize / System.Math.Max(texture.Width, texture.Height) * Projectile.scale;

            Color gem = GemColor;
            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 10f);

            if (State == StateHover)
                DrawPullRing();

            // --- trail: gold at the head fading to the gem color, with ghost copies of the ring
            WeaponFX.DrawGlowTrail(Projectile, Gold, gem, 0.5f, 0.8f);

            for (int i = 2; i < Projectile.oldPos.Length; i += 2)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float k = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    WeaponFX.Additive(gem, 0.35f * k), Projectile.oldRot[i], origin, drawScale, SpriteEffects.None, 0);
            }

            WeaponFX.DrawGlow(center, WeaponFX.Additive(gem, 0.5f * pulse), 1.2f * Projectile.scale);

            // --- the crown ring
            Main.EntitySpriteDraw(texture, center, null, Color.Lerp(lightColor, Color.White, 0.6f) * Projectile.Opacity,
                Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);

            // --- three gem flares spinning with the ring; the active gem burns brightest
            float radius = SpriteDrawSize * 0.36f * Projectile.scale;
            for (int g = 0; g < 3; g++)
            {
                Vector2 offset = (Projectile.rotation + g * MathHelper.TwoPi / 3f).ToRotationVector2() * radius;
                float brightness = g == GemType ? 1f * pulse : 0.3f;
                WeaponFX.DrawGlow(center + offset, WeaponFX.Additive(GemColorOf(g), 0.9f * brightness), g == GemType ? 0.38f : 0.2f);
            }

            return false;
        }

        // dashed circle showing how far the sapphire's pull reaches
        private void DrawPullRing()
        {
            const int segments = 28;
            float fade = MathHelper.Clamp(1f - HoverTimer / HoverTime, 0.2f, 1f);

            for (int i = 0; i < segments; i++)
            {
                float a1 = MathHelper.TwoPi * i / segments + Main.GlobalTimeWrappedHourly * 0.8f;
                float a2 = a1 + MathHelper.TwoPi / segments * 0.55f;

                WeaponFX.DrawLine(Projectile.Center + a1.ToRotationVector2() * PullRange,
                    Projectile.Center + a2.ToRotationVector2() * PullRange,
                    WeaponFX.Additive(SapphireColor, 0.45f * fade), 2f);
            }
        }
    }
}