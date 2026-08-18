using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusionKeybinds
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
        public int CooldownTimer = 0;
        public int parrySlowTimer = 0;

        public const int MaxCooldown = 120;
        public bool IsParrying => parrySlowTimer > 0;

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
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (KeybindSystem.ParryKeybind.JustPressed && CooldownTimer <= 0)
            {
                // Active parry window is 12 tick so TIGHT TIGHT
                parrySlowTimer = 12;
                CooldownTimer = MaxCooldown;

                Player.velocity.X *= 0.2f;

                SoundEngine.PlaySound(SoundID.Item37, Player.position);
                SpawnDustExplosion(DustID.Silver, 25, 6f);
            }
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (IsParrying)
            {
                if (info.DamageSource.SourceNPCIndex >= 0 && info.DamageSource.SourceNPCIndex < Main.maxNPCs)
                {
                    NPC attacker = Main.npc[info.DamageSource.SourceNPCIndex];

                    //if the boss has iparryable and the attack has ISparryable then boom parry 
                    if (attacker.ModNPC is IParryable boss && boss.IsParryable)
                    {
                        boss.OnParried(Player);
                        Player.SetImmuneTimeForAllTypes(60);

                        SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.5f }, Player.position);
                        SoundEngine.PlaySound(SoundID.Item4, Player.position);
                        SpawnDustExplosion(DustID.Gold, 40, 9f);

                        parrySlowTimer = 0;
                        return true; // Completely blocks hit
                    }
                }
            }

            return base.FreeDodge(info);
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