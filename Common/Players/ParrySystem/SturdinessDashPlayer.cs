using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Players.ParrySystem
{
    public class SturdinessDashPlayer : ModPlayer
    {
        // pretty self explanatory ngl
        public const float DashSpeed = 32f;
        public const float MaxDashDistance = 356f;
        public const int MaxDashDamage = 100;
        public const int DashImmunityTime = 10;

        // yes the dash is blue since i was told blue but i kinda like blue
        private static readonly Color DashBlue = new Color(50, 170, 255);
        private static readonly Color DashLightBlue = new Color(150, 230, 255);
        private static readonly Color DashDarkBlue = new Color(30, 90, 255);


        private bool isDashing;
        private int dashTimer;
        private int dashDuration;

        private Vector2 dashDirection;

        private int dashDamage;

        // keeps track of who we've already hit during the dash
        // because hitting the same boss 20 times in 10 frames would be
        // just a LITTLE bit stupid
        private HashSet<int> hitNPCs = new();

        public bool IsDashing => isDashing;

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!KeybindSystem.SturdinessMeterUseKeybind.JustPressed)
                return;

            TryStartDash();
        }

        private void TryStartDash()
        {
            if (Player.dead || Player.ghost)
                return;

            if (isDashing)
                return;

            ParryPlayer parryPlayer = Player.GetModPlayer<ParryPlayer>();

            int sturdiness = parryPlayer.SturdinessMeter;

            if (sturdiness <= 0)
                return;


            // turn the meter into a percentage
            float sturdinessRatio =
                sturdiness / (float)ParryPlayer.MaxSturdinessMeter;


            // Get the direction from the player to the mouse
            Vector2 direction = Main.MouseWorld - Player.Center;

            if (direction == Vector2.Zero)
                return;

            direction.Normalize();

            dashDirection = direction;


            // The more Sturdiness you have, the farther you go
            // More Sturdiness = more damage
            float dashDistance =
                MaxDashDistance * sturdinessRatio;

            dashDuration = (int)MathHelper.Clamp(
                dashDistance / DashSpeed,
                1f,
                MaxDashDistance / DashSpeed
            );

            dashDamage = (int)(
                MaxDashDamage * sturdinessRatio
            );

            dashDamage = System.Math.Max(1, dashDamage);


            parryPlayer.SturdinessMeter = 0;


            isDashing = true;
            dashTimer = dashDuration;

            hitNPCs.Clear();

            // Give us a tiny bit of immunity so we don't get
            // immediately smacked while starting the dash
            Player.SetImmuneTimeForAllTypes(DashImmunityTime);

            Player.velocity = dashDirection * DashSpeed;

            SpawnDashBurst();

            SpawnDashRing();

            SoundEngine.PlaySound(
                SoundID.Item74 with { Pitch = 0.2f, Volume = 0.9f },
                Player.position
            );

            Lighting.AddLight(
                Player.Center,
                0.1f,
                0.5f,
                1.0f
            );
        }

        public override void PreUpdateMovement()
        {
            if (!isDashing)
                return;

            // Force the velocity every frame so gravity / acceleration
            // doesn't start messing with the dash
            Player.velocity = dashDirection * DashSpeed;

            SpawnDashTrail();
            SpawnDashStreaks();
            SpawnDashAura();

            Lighting.AddLight(
                Player.Center,
                0.08f,
                0.4f,
                0.9f
            );

            CheckNPCContact();

            dashTimer--;

            if (dashTimer <= 0)
            {
                EndDash();
            }
        }

        private void CheckNPCContact()
        {
            Rectangle playerHitbox = Player.Hitbox;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active)
                    continue;

                if (npc.friendly)
                    continue;

                if (npc.dontTakeDamage)
                    continue;

                if (!npc.CanBeChasedBy())
                    continue;

                if (hitNPCs.Contains(npc.whoAmI))
                    continue;

                if (!playerHitbox.Intersects(npc.Hitbox))
                    continue;


                int hitDirection =
                    Player.Center.X < npc.Center.X ? 1 : -1;

                npc.SimpleStrikeNPC(
                    dashDamage,
                    hitDirection,
                    crit: false,
                    knockBack: 0f,
                    damageType: DamageClass.Generic
                );

                hitNPCs.Add(npc.whoAmI);


                SpawnHitBurst(npc.Center);

                SpawnImpactRing(npc.Center);

                SoundEngine.PlaySound( //we'll get a sound for this later 
                    SoundID.Item14 with
                    {
                        Pitch = 0.3f,
                        Volume = 0.8f
                    },
                    npc.Center
                );

                Lighting.AddLight(
                    npc.Center,
                    0.15f,
                    0.6f,
                    1.2f
                );
            }
        }

        private void EndDash()
        {
            isDashing = false;
            dashTimer = 0;

            SpawnEndBurst();

            // Kill all leftover momentum
            Player.velocity = Vector2.Zero;

            hitNPCs.Clear();
        }

        private void SpawnDashTrail()
        {
            // Main dash trail basically spawns partical trail and randomizes them so not all straight 
            for (int i = 0; i < 4; i++)
            {
                // Spawn the particles behind us
                Vector2 backwards =
                    -dashDirection * Main.rand.NextFloat(8f, 22f);

                Vector2 velocity =
                    backwards +
                    Main.rand.NextVector2Circular(1.5f, 1.5f);

                Dust dust = Dust.NewDustPerfect(
                    Player.Center +
                    Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextBool(3)
                        ? DustID.BlueTorch
                        : DustID.GemSapphire,
                    velocity,
                    Alpha: 80,
                    newColor: DashBlue,
                    Scale: Main.rand.NextFloat(0.9f, 1.7f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnDashStreaks()
        {
            if (!Main.rand.NextBool(2))
                return;

            Vector2 perpendicular =
                new Vector2(-dashDirection.Y, dashDirection.X);

            Vector2 spawnOffset =
                perpendicular *
                Main.rand.NextFloat(-18f, 18f);

            // Streaks move backwards to make the dash look faster
            Vector2 velocity =
                -dashDirection *
                Main.rand.NextFloat(6f, 14f);

            Dust dust = Dust.NewDustPerfect(
                Player.Center + spawnOffset,
                DustID.BlueTorch,
                velocity,
                Alpha: 60,
                newColor: DashLightBlue,
                Scale: Main.rand.NextFloat(0.7f, 1.3f)
            );

            dust.noGravity = true;
        }

        private void SpawnDashAura()
        {
            if (!Main.rand.NextBool(2))
                return;

            Vector2 offset =
                Main.rand.NextVector2Circular(
                    Player.width * 0.7f,
                    Player.height * 0.7f
                );

            Vector2 velocity =
                offset.SafeNormalize(Vector2.Zero) *
                Main.rand.NextFloat(1f, 3f);

            Dust dust = Dust.NewDustPerfect(
                Player.Center + offset,
                DustID.GemSapphire,
                velocity,
                Alpha: 70,
                newColor: DashLightBlue,
                Scale: Main.rand.NextFloat(0.6f, 1.1f)
            );

            dust.noGravity = true;
        }

        private void SpawnDashBurst()
        {
            // explosion when the dash starts
            for (int i = 0; i < 45; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2CircularEdge(
                        Main.rand.NextFloat(4f, 11f),
                        Main.rand.NextFloat(4f, 11f)
                    );

                Dust dust = Dust.NewDustPerfect(
                    Player.Center,
                    i % 3 == 0
                        ? DustID.BlueTorch
                        : DustID.GemSapphire,
                    velocity,
                    Alpha: 50,
                    newColor: DashBlue,
                    Scale: Main.rand.NextFloat(1f, 2.2f)
                );

                dust.noGravity = true;
            }

            // extra particles going backwards because apparently one explosion wasn't enough
            for (int i = 0; i < 20; i++)
            {
                Vector2 velocity =
                    -dashDirection *
                    Main.rand.NextFloat(4f, 12f);

                velocity +=
                    Main.rand.NextVector2Circular(3f, 3f);

                Dust dust = Dust.NewDustPerfect(
                    Player.Center,
                    DustID.BlueTorch,
                    velocity,
                    Alpha: 40,
                    newColor: DashLightBlue,
                    Scale: Main.rand.NextFloat(1f, 2f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnDashRing()
        {
            // Spawn a ring around the player when the dash starts
            int amount = 30;

            for (int i = 0; i < amount; i++)
            {
                float angle =
                    MathHelper.TwoPi * i / amount;

                Vector2 direction =
                    angle.ToRotationVector2();

                Dust dust = Dust.NewDustPerfect(
                    Player.Center + direction * 8f,
                    DustID.BlueTorch,
                    direction * Main.rand.NextFloat(3f, 6f),
                    Alpha: 40,
                    newColor: DashLightBlue,
                    Scale: Main.rand.NextFloat(0.8f, 1.4f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnHitBurst(Vector2 position)
        {
            // Big explosion when we hit an NPC
            for (int i = 0; i < 35; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2CircularEdge(
                        Main.rand.NextFloat(5f, 12f),
                        Main.rand.NextFloat(5f, 12f)
                    );

                Dust dust = Dust.NewDustPerfect(
                    position,
                    i % 2 == 0
                        ? DustID.BlueTorch
                        : DustID.GemSapphire,
                    velocity,
                    Alpha: 30,
                    newColor: DashLightBlue,
                    Scale: Main.rand.NextFloat(1f, 2f)
                );

                dust.noGravity = true;
            }

            // Extra particles going in the direction of the dash
            // to make the hit feel like an actual impact
            for (int i = 0; i < 15; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.BlueTorch,
                    dashDirection *
                    Main.rand.NextFloat(4f, 10f),
                    Alpha: 20,
                    newColor: DashBlue,
                    Scale: Main.rand.NextFloat(0.8f, 1.6f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnImpactRing(Vector2 position)
        {
            int amount = 28;

            for (int i = 0; i < amount; i++)
            {
                float angle =
                    MathHelper.TwoPi * i / amount;

                Vector2 direction =
                    angle.ToRotationVector2();

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.BlueTorch,
                    direction * Main.rand.NextFloat(5f, 9f),
                    Alpha: 20,
                    newColor: DashLightBlue,
                    Scale: Main.rand.NextFloat(0.8f, 1.5f)
                );

                dust.noGravity = true;
            }
        }

        private void SpawnEndBurst()
        {
            for (int i = 0; i < 25; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(5f, 5f);

                Dust dust = Dust.NewDustPerfect(
                    Player.Center,
                    DustID.BlueTorch,
                    velocity,
                    Alpha: 50,
                    newColor: DashBlue,
                    Scale: Main.rand.NextFloat(0.8f, 1.7f)
                );

                dust.noGravity = true;
            }
        }

        public override void OnEnterWorld()
        {
            // reset everything just in case
            isDashing = false;
            dashTimer = 0;
            hitNPCs.Clear();
        }

        public override void OnRespawn()
        {
            // don't let us respawn while somehow still dashing
            isDashing = false;
            dashTimer = 0;
            hitNPCs.Clear();
        }
    }
}