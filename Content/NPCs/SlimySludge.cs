using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.NPCs
{
    // oo shi moving like jelly
    public class SlimySludge : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private float stretch;
        private float stretchVel;
        private float lastVelY;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            if (!lateInstantiation)
                return false;

            if (entity.boss || entity.type == NPCID.KingSlime || entity.type == NPCID.QueenSlimeBoss)
                return false;

            return entity.aiStyle == NPCAIStyleID.Slime;
        }

        public override void PostAI(NPC npc)
        {
            if (Main.dedServ)
                return;

            float vy = npc.velocity.Y;

            // jump launch -> stretch up
            if (lastVelY > -1f && vy < -3f)
                stretchVel += 0.12f;

            // landing -> splat
            if (npc.collideY && lastVelY > 2f)
                stretchVel -= MathHelper.Clamp(lastVelY * 0.035f, 0.08f, 0.4f);

            // spring toward a stretch that follows vertical speed
            float target = MathHelper.Clamp(-vy * 0.03f, -0.3f, 0.4f);
            stretchVel += (target - stretch) * 0.2f;
            stretchVel *= 0.84f;
            stretch = MathHelper.Clamp(stretch + stretchVel, -0.45f, 0.5f);

            lastVelY = vy;
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            stretchVel -= 0.3f;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;

            float jiggle = (float)Math.Sin(Main.GameUpdateCount * 0.1f + npc.whoAmI * 1.7f) * 0.025f;
            float s = stretch + jiggle;
            Vector2 scale = new Vector2(1f - s * 0.8f, 1f + s) * npc.scale;

            float lean = MathHelper.Clamp(npc.velocity.X * 0.03f, -0.2f, 0.2f);
            Vector2 origin = new Vector2(frame.Width / 2f, frame.Height);
            Vector2 pos = new Vector2(npc.Center.X, npc.position.Y + npc.height + 4f + npc.gfxOffY) - screenPos;
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // vanilla draws the held item inside the slime, we skip vanilla so redo it
            int held = (int)npc.ai[1];
            if (held > 0 && held < ItemLoader.ItemCount)
            {
                Main.instance.LoadItem(held);
                Texture2D itemTex = TextureAssets.Item[held].Value;
                float fit = Math.Min(1f, 22f / Math.Max(itemTex.Width, itemTex.Height));
                spriteBatch.Draw(itemTex, npc.Center - screenPos + new Vector2(0f, 2f), null, npc.GetAlpha(drawColor),
                    0f, itemTex.Size() / 2f, fit * npc.scale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(tex, pos, frame, npc.GetAlpha(drawColor), npc.rotation + lean, origin, scale, fx, 0f);
            return false;
        }
    }
}