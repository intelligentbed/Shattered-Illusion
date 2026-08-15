using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusionKeybinds
{
    public class ParryPlayer : ModPlayer
    {
        public int CooldownTimer = 0;
        public int parrySlowTimer = 0;

        public const int MaxCooldown = 120;

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
                //makes the player slow for 15 ticks thats like IDKth of a second might reduce depending how how the gameplay feels tbh also cooldown
                parrySlowTimer = 15;
                CooldownTimer = MaxCooldown;

                Player.velocity.X *= 0.2f;

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item37, Player.position);
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
    }

    public class ParryVisual : ModProjectile
    {
        public override string Texture => "ShatteredIllusion/Common/Textures/ParryVisual";
        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 15; 
        }
        
        // i got help on this part :(
        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            Projectile.Center = player.Center;
            Projectile.gfxOffY = player.gfxOffY;

            if (!player.active || player.dead)
                Projectile.Kill();
        }
    }
}