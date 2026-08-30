using System;
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
        private static readonly SoundStyle ParryFullSound = new SoundStyle("ShatteredIllusion/Sounds/ParryFull");
        private static readonly SoundStyle SteeledUseSound = new SoundStyle("ShatteredIllusion/Sounds/dry-fart");

        public int CooldownTimer = 0;
        public int parrySlowTimer = 0;

        public const int MaxCooldown = 120;
        public bool IsParrying => parrySlowTimer > 0;

        public int SturdinessMeter = 0;
        public const int MaxSturdinessMeter = 100;
        public const int SturdinessMeterGainPerParry = 20;

        public const int FocusedBuffDuration = 600;
        public const int ParryHealAmount = 20;

        // The curve for the buff duration so above 60 = good below bad
        private const float SteeledCurveThreshold = 0.6f;

        private const float SteeledMinScale = 0.05f;
        private int steeledHealPerParry = 0;

        // Purely visual: lets the bar drain smoothly over the buff's duration instead
        // of snapping to empty the instant Steeled is activated. SturdinessMeter itself
        // still hits 0 immediately below - this never touches the real resource value.
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

                Player.velocity.X *= 0.2f;

                SoundEngine.PlaySound(SoundID.Item37, Player.position);
                SpawnDustExplosion(DustID.Silver, 25, 6f);
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

            // Bar drains from its current fill down to empty over exactly `duration` ticks,
            // so a max-duration activation drains slow and a min-duration one drains fast.
            steeledDrainStartValue = SturdinessMeter;
            steeledDrainDuration = duration;
            steeledDrainTimer = duration;

            SturdinessMeter = 0;
            SoundEngine.PlaySound(SteeledUseSound, Player.position);
            SpawnDustExplosion(DustID.BlueTorch, 40, 9f);

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

                        SoundEngine.PlaySound(SoundID.Item4, Player.position);
                        SpawnDustExplosion(DustID.Gold, 40, 9f);

                        bool wasFull = SturdinessMeter >= MaxSturdinessMeter;

                        steeledDrainTimer = 0;
                        SturdinessMeter += SturdinessMeterGainPerParry;

                        if (SturdinessMeter > MaxSturdinessMeter)
                            SturdinessMeter = MaxSturdinessMeter;

                        bool isFull = SturdinessMeter >= MaxSturdinessMeter;

                        if (isFull && !wasFull)
                        {
                            OnSturdinessMeterFull();
                        }

                        // Steeled heal parry payoff 
                        if (steeledHealPerParry > 0 && Player.HasBuff(ModContent.BuffType<SteeledBuff>()))
                        {
                            Player.statLife = Math.Min(Player.statLife + steeledHealPerParry, Player.statLifeMax2);
                            Player.HealEffect(steeledHealPerParry);
                        }

                        // Mycelial set bonus - parrying pops a burst of spores that
                        // damages and poisons anything nearby, not just a visual.
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

                for (int i = 0; i < dustPerLayer; i++)
                {
                    float angle = MathHelper.TwoPi * i / dustPerLayer;
                    Vector2 direction = angle.ToRotationVector2();
                    Vector2 spawnPos = center + direction * layerRadius;
                    Vector2 velocity = direction * 3f;

                    Dust ring = Dust.NewDustPerfect(spawnPos, DustID.GlowingMushroom, velocity, Alpha: 40, Scale: 1.8f);
                    ring.noGravity = true;
                }
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
    }
}