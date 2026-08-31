using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ShatteredIllusion.Content.Buffs.StatBuffs;
using ShatteredIllusionKeybinds;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Players.ParrySystem
{
    // ok since i know people will be confused on what iparryable means 
    // IT MEANS THAT THE BOSS IS PARRYABLE BUT NOT THE ATTACK YOU GOTTA DO THAT YOURSELF  
    public interface IParryable
    {
        bool IsParryable { get; }
        void OnParried(Player player);
    }

    public class ParryPlayer : ModPlayer
    {
        private static readonly SoundStyle ParryFullSound = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/ParryBarFull");
        private static readonly SoundStyle SteeledUseSound = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/dry-fart");
        private static readonly SoundStyle FailedParry = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/FailedParry");
        private static readonly SoundStyle SuccessfulParry = new SoundStyle("ShatteredIllusion/Sounds/ParrySounds/SuccessfulParry");

        public int CooldownTimer = 0;
        public int parrySlowTimer = 0;
        private bool parrySucceededThisWindow = false;

        public const int MaxCooldown = 120;
        public bool IsParrying => parrySlowTimer > 0;

        public int SturdinessMeter = 0;
        public const int MaxSturdinessMeter = 100;
        public const int SturdinessMeterGainPerParry = 20;

        public const int FocusedBuffDuration = 600;
        public const int ParryHealAmount = 20;

        // The curve for the buff duration so above 60 = good below cant use 
        private const float SteeledCurveThreshold = 0.6f;

        private const float SteeledMinScale = 0.05f;
        private int steeledHealPerParry = 0;

        private int steeledDrainStartValue = 0;
        private int steeledDrainDuration = 0;
        private int steeledDrainTimer = 0;

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
        private const int MycelialPoisonedDuration = 180; // 3 seconds

        public override void ResetEffects()
        {
            MycelialSetActive = false;
        }

        public override void PreUpdate()
        {
            if (CooldownTimer > 0)
            {
                CooldownTimer--;
            }

            if (parrySlowTimer > 0)
            {
                parrySlowTimer--;

                // Window just expired without a successful parry
                if (parrySlowTimer <= 0 && !parrySucceededThisWindow)
                {
                    SoundEngine.PlaySound(FailedParry, Player.position);
                }
            }

            if (steeledDrainTimer > 0)
            {
                steeledDrainTimer--;
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (KeybindSystem.ParryKeybind.JustPressed && CooldownTimer <= 0)
            {
                // Active parry window is now 15 since some of the devs are buns at the game
                parrySlowTimer = 15;
                CooldownTimer = MaxCooldown;
                parrySucceededThisWindow = false;

                Player.velocity.X *= 0.2f;

                SpawnDustExplosion(DustID.Silver, 25, 6f);

                SpawnRing(Player.Center, DustID.Silver, 30f, 14, 3.5f, alpha: 80, scale: 1.3f);
            }

            if (KeybindSystem.SturdinessMeterUseKeybind.JustPressed)
            {
                ActivateSteeledBuff();
            }
        }

        // Duration and heal parry also the curve is set to 60 
        public void ActivateSteeledBuff()
        {
            float fraction = SturdinessMeter / (float)MaxSturdinessMeter;

            if (fraction < SteeledCurveThreshold)
                return;

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
            SoundEngine.PlaySound(SteeledUseSound, Player.position);
            SpawnDustExplosion(DustID.BlueTorch, 40, 9f);


            Lighting.AddLight(Player.Center, 0.3f, 0.5f, 1.2f * scale);

            int auraRings = 2 + (int)Math.Round(2 * scale); // 2-4 rings depending on power
            for (int i = 0; i < auraRings; i++)
            {
                float ringRadius = 35f + i * 22f;
                SpawnRing(Player.Center, DustID.BlueTorch, ringRadius, 16, 2f + i * 0.5f, alpha: 100, scale: 1.4f);
            }

            // Shield sigil flashes outward on activation, bigger/brighter the stronger the buff
            float shieldSize = MathHelper.Lerp(40f, 80f, scale);
            SpawnShieldSymbol(Player.Center, DustID.BlueTorch, shieldSize, alpha: 70, dustScale: 1.4f + scale * 0.6f, outwardSpeed: 1.5f);
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (IsParrying)
            {
                if (info.DamageSource.SourceNPCIndex >= 0 &&
                    info.DamageSource.SourceNPCIndex < Main.maxNPCs)
                {
                    NPC attacker = Main.npc[info.DamageSource.SourceNPCIndex];

                    // if the boss has IParryable and the attack has IsParryable then boom parry
                    if (attacker.ModNPC is IParryable boss && boss.IsParryable)
                    {
                        boss.OnParried(Player);
                        Player.SetImmuneTimeForAllTypes(60);

                        SoundEngine.PlaySound(
                            SoundID.Item37 with { Pitch = 0.5f },
                            Player.position
                        );

                        SoundEngine.PlaySound(SuccessfulParry, Player.position);
                        SpawnDustExplosion(DustID.Gold, 40, 9f);


                        Lighting.AddLight(Player.Center, 1.3f, 1.05f, 0.35f);
                        SpawnRing(Player.Center, DustID.Gold, 20f, 18, 9f, alpha: 40, scale: 1.7f);
                        SpawnRing(Player.Center, DustID.GoldFlame, 45f, 22, 5f, alpha: 70, scale: 1.4f);


                        Vector2 toAttacker = attacker.Center - Player.Center;
                        if (toAttacker != Vector2.Zero)
                        {
                            attacker.velocity += Vector2.Normalize(toAttacker) * 4f;
                            attacker.netUpdate = true;
                        }
                        for (int i = 0; i < 14; i++)
                        {
                            Vector2 dustVelocity = Main.rand.NextVector2Circular(5f, 5f);
                            Dust clash = Dust.NewDustPerfect(attacker.Center, DustID.Gold, dustVelocity, Alpha: 60, Scale: 1.6f);
                            clash.noGravity = true;
                        }

                        bool wasFull = SturdinessMeter >= MaxSturdinessMeter;

                        parrySucceededThisWindow = true;
                        SturdinessMeter += SturdinessMeterGainPerParry;

                        if (SturdinessMeter > MaxSturdinessMeter)
                            SturdinessMeter = MaxSturdinessMeter;

                        bool isFull = SturdinessMeter >= MaxSturdinessMeter;

                        if (isFull && !wasFull)
                        {
                            OnSturdinessMeterFull();
                        }

                        // Steeled heal parry  
                        if (steeledHealPerParry > 0 && Player.HasBuff(ModContent.BuffType<SteeledBuff>()))
                        {
                            Player.statLife = Math.Min(Player.statLife + steeledHealPerParry, Player.statLifeMax2);
                            Player.HealEffect(steeledHealPerParry);
                        }

                        if (MycelialSetActive)
                        {
                            SpawnMycelialExplosion();
                        }

                        parrySlowTimer = 0;

                        return true;
                    }
                }
            }

            return base.FreeDodge(info);
        }

        private void OnSturdinessMeterFull()
        {
            SoundEngine.PlaySound(ParryFullSound, Player.position);
            SpawnDustExplosion(DustID.GoldFlame, 60, 12f);

            Lighting.AddLight(Player.Center, 1.4f, 1.1f, 0.4f);
            SpawnRing(Player.Center, DustID.GoldFlame, 15f, 20, 10f, alpha: 30, scale: 1.9f);
            SpawnRing(Player.Center, DustID.Gold, 55f, 26, 3f, alpha: 90, scale: 1.5f);
        }

        private void SpawnMycelialExplosion()
        {
            Vector2 center = Player.Center;

            SoundEngine.PlaySound(SoundID.NPCDeath6, center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = 0.3f }, center);


            Lighting.AddLight(center, 0.2f, 1.4f, 0.3f);

            const int ringLayers = 4;
            const int dustPerLayer = 20;
            for (int layer = 1; layer <= ringLayers; layer++)
            {
                float layerRadius = MycelialExplosionRadius * layer / ringLayers;
                SpawnRing(center, DustID.GlowingMushroom, layerRadius, dustPerLayer, 3f, alpha: 40, scale: 1.8f);
            }


            for (int i = 0; i < 16; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-7f, -2f));
                Dust spore = Dust.NewDustPerfect(center, DustID.GlowingMushroom, velocity, Alpha: 50, Scale: Main.rand.NextFloat(1.2f, 2f));
                spore.noGravity = false;
            }

            SpawnDustExplosion(DustID.GlowingMushroom, 35, 7f);
            SpawnDustExplosion(DustID.Smoke, 18, 4f);
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;

                if (Vector2.Distance(npc.Center, center) > MycelialExplosionRadius)
                    continue;

                NPC.HitInfo hit = new NPC.HitInfo
                {
                    Damage = MycelialExplosionDamage,
                    HitDirection = Math.Sign(npc.Center.X - center.X),
                    Crit = false,
                };

                npc.StrikeNPC(hit);
                npc.AddBuff(BuffID.Poisoned, MycelialPoisonedDuration);

                for (int d = 0; d < 6; d++)
                {
                    Vector2 popVel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                    Dust pop = Dust.NewDustPerfect(npc.Center, DustID.GlowingMushroom, popVel, Alpha: 60, Scale: 1.3f);
                    pop.noGravity = true;
                }
            }
        }

        public override void PostUpdateRunSpeeds()
        {
            if (parrySlowTimer > 0)
            {
                Player.maxRunSpeed *= 0.15f;
                Player.accRunSpeed *= 0.15f;
                Player.runAcceleration *= 0.15f;
            }
        }

        private void SpawnDustExplosion(int dustType, int amount, float speed)
        {
            for (int i = 0; i < amount; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Circular(speed, speed);

                Dust dust = Dust.NewDustPerfect(
                    Player.Center,
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

            return points.ToArray();
        }

        private static void AddLineSegment(List<Vector2> points, Vector2 start, Vector2 end, int count)
        {
            // Skip the final t=1 point so segments don't double up their shared corner.
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