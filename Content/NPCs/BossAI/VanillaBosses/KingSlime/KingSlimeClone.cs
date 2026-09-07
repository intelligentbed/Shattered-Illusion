using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs.BossAI.VanillaBosses.KingSlime
{
    public class KingSlimeClone : ModNPC
    {
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = Main.npcFrameCount[NPCID.KingSlime];
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults()
        {
            NPC.width = 60;
            NPC.height = 45;
            NPC.damage = 30;
            NPC.defense = 12;
            NPC.lifeMax = 1000;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontTakeDamage = true;
            NPC.aiStyle = -1;
            NPC.alpha = 30;
        }

        public override void AI()
        {
            int parentIndex = (int)NPC.ai[0];

            if (parentIndex < 0 || parentIndex >= Main.maxNPCs || !Main.npc[parentIndex].active || Main.npc[parentIndex].type != NPCID.KingSlime)
            {
                NPC.active = false;
                return;
            }


            NPC parent = Main.npc[parentIndex];
            NPC.ai[1]++;
            float timer = NPC.ai[1];
            bool isVertical = NPC.ai[2] == 1f;
            float side = NPC.ai[3];


            const float splitAnchorYOffset = 16f;
            Vector2 anchor = parent.Center + new Vector2(0f, splitAnchorYOffset);

            // Travel distance scales with difficulty - classic pulls the clones in a bit
            // closer, master sends them further out.
            float maxOffset = Main.masterMode ? 280f : (Main.expertMode ? 240f : 200f);
            Vector2 offsetDirection = isVertical ? new Vector2(0f, side) : new Vector2(side, 0f);

            // Total duration
            float maxDuration = 70f;

            const float cloneBaseScale = 0.85f;
            const float growWindow = 10f;

            float travelProgress;

            if (timer <= maxDuration)
            {
                float t = timer / maxDuration;

                if (t < 0.5f)
                {
                    float normT = t * 2f;
                    travelProgress = 1f - (1f - normT) * (1f - normT);
                }
                else
                {
                    float normT = (t - 0.5f) * 2f;
                    travelProgress = 1f - (normT * normT);
                }

                NPC.Center = anchor + (offsetDirection * maxOffset * travelProgress);
            }
            else
            {
                NPC.active = false;
                return;
            }

            float scaleT = 1f;
            if (timer < growWindow)
                scaleT = timer / growWindow;
            else if (timer > maxDuration - growWindow)
                scaleT = MathHelper.Clamp((maxDuration - timer) / growWindow, 0f, 1f);

            NPC.scale = cloneBaseScale * scaleT;
            NPC.alpha = (int)MathHelper.Lerp(255, 30, scaleT);
            NPC.spriteDirection = parent.spriteDirection;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {

            Texture2D texture = ModContent.Request<Texture2D>("ShatteredIllusion/Content/NPCs/BossAI/VanilliaBosses/KingSlime/KingSlimeClone").Value;

            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            spriteBatch.Draw(
                texture,
                drawPos,
                sourceRectangle,
                drawColor * ((255 - NPC.alpha) / 255f),
                NPC.rotation,
                drawOrigin,
                NPC.scale,
                NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}