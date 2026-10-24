using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ShatteredIllusion.Content.Biomes
{
    public class AshenWaterStyle : ModWaterStyle
    {
        public override string Texture => "ShatteredIllusion/Assets/ExtraTextures/Water/AshenWaterStyle";
        public override string BlockTexture => Texture + "_Block";
        public override string SlopeTexture => Texture + "_Slope";

        public override int ChooseWaterfallStyle() => ModContent.GetInstance<AshenWaterfallStyle>().Slot;

        public override int GetSplashDust() => DustID.Water;

        public override int GetDropletGore() => GoreID.WaterDrip;

        public override void LightColorMultiplier(ref float r, ref float g, ref float b)
        {
            // a bit of a glow so it reads as warm turquoise instead of flat blue
            r = 0.9f;
            g = 1.05f;
            b = 1.05f;
        }

        public override Color BiomeHairColor() => new Color(70, 220, 200);
    }

    public class AshenWaterfallStyle : ModWaterfallStyle
    {
        public override string Texture => "ShatteredIllusion/Assets/ExtraTextures/Water/AshenWaterfallStyle";
    }

    public class AshenSteamSystem : ModSystem
    {
        private struct Puff
        {
            public Vector2 Pos, Vel, StartSize, EndSize;
            public float Rot, RotSpeed, Life, MaxLife, Alpha, Phase;
            public int Tex;
        }

        private const int MaxPuffs = 700;
        private const int TexSize = 128;
        private const int TexCount = 4;

        private static readonly Color FreshColor = new Color(215, 255, 248);
        private static readonly Color OldColor = new Color(110, 220, 205);

        private readonly Puff[] _puffs = new Puff[MaxPuffs];
        private int _count;
        private Texture2D[] _tex;

        public override void Load()
        {
            if (!Main.dedServ)
                Main.QueueMainThreadAction(BuildTextures);
        }

        public override void Unload()
        {
            Texture2D[] old = _tex;
            _tex = null;

            if (old != null)
                Main.QueueMainThreadAction(() => { foreach (Texture2D t in old) t.Dispose(); });
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gamePaused || _tex == null)
                return;

            UpdatePuffs();

            if (Main.LocalPlayer.InModBiome<AshenShoreBiome>())
                SpawnPuffs();
        }

        public override void PostDrawTiles()
        {
            if (_tex == null || _count == 0)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < _count; i++)
            {
                Puff p = _puffs[i];
                float t = p.Life / p.MaxLife;

                float fadeIn = Smooth(Math.Min(1f, t / 0.18f));
                float fadeOut = (float)Math.Pow(1f - t, 1.6);
                float a = p.Alpha * fadeIn * fadeOut;
                if (a <= 0.003f)
                    continue;

                float grow = 1f - (1f - t) * (1f - t);
                Vector2 scale = Vector2.Lerp(p.StartSize, p.EndSize, grow) / TexSize;

                Vector2 screen = p.Pos - Main.screenPosition;
                float pad = Math.Max(p.EndSize.X, p.EndSize.Y);
                if (screen.X < -pad || screen.Y < -pad || screen.X > Main.screenWidth + pad || screen.Y > Main.screenHeight + pad)
                    continue;

                // prevent its from going pure black at night
                Color light = Lighting.GetColor((int)(p.Pos.X / 16f), (int)(p.Pos.Y / 16f));
                float lum = 0.3f + 0.7f * (light.R + light.G + light.B) / 765f;

                Vector3 rgb = Color.Lerp(FreshColor, OldColor, t).ToVector3() * (lum * a);
                Color col = new Color(rgb.X, rgb.Y, rgb.Z, a * 0.7f);

                Main.spriteBatch.Draw(_tex[p.Tex], screen, null, col, p.Rot, new Vector2(TexSize / 2f), scale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
        }

        private void UpdatePuffs()
        {
            float wind = Main.windSpeedCurrent * 0.6f;

            for (int i = _count - 1; i >= 0; i--)
            {
                Puff p = _puffs[i];
                p.Life++;

                if (p.Life >= p.MaxLife)
                {
                    _puffs[i] = _puffs[--_count];
                    continue;
                }

                // slow lazy sway so it doesn't rise in straight lines
                p.Vel.X += (float)Math.Sin(p.Life * 0.03f + p.Phase) * 0.004f + wind * 0.002f;
                p.Vel *= 0.994f;
                p.Pos += p.Vel;
                p.Rot += p.RotSpeed;

                _puffs[i] = p;
            }
        }

        private void SpawnPuffs()
        {
            Point tl = (Main.screenPosition / 16f).ToPoint();
            int w = Main.screenWidth / 16 + 2;
            int h = Main.screenHeight / 16 + 2;
            float wind = Main.windSpeedCurrent * 0.6f;

            for (int i = 0; i < 4; i++)
            {
                if (_count >= MaxPuffs)
                    return;

                int x = tl.X + Main.rand.Next(w);
                if (!TryFindSurface(x, tl.Y, tl.Y + h, out int sy))
                    continue;

                Vector2 pos = new Vector2(x * 16f + Main.rand.NextFloat(16f), sy * 16f - 4f);
                bool bank = i < 2;

                Puff p = new Puff
                {
                    Pos = pos,
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Tex = Main.rand.Next(TexCount),
                };

                if (bank)
                {
                    // big slow bank hugging the water
                    if (!Main.rand.NextBool(3))
                        continue;

                    float s = Main.rand.NextFloat(36f, 60f);
                    p.Vel = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f) + wind * 0.4f, Main.rand.NextFloat(-0.35f, -0.1f));
                    p.StartSize = new Vector2(s);
                    p.EndSize = new Vector2(s * Main.rand.NextFloat(2.2f, 3.2f), s * Main.rand.NextFloat(1.6f, 2.4f));
                    p.MaxLife = Main.rand.Next(260, 460);
                    p.Alpha = Main.rand.NextFloat(0.2f, 0.34f);
                    p.Rot = Main.rand.NextFloat(MathHelper.TwoPi);
                    p.RotSpeed = Main.rand.NextFloat(-0.004f, 0.004f);
                }
                else
                {
                    // thin fast wisps shooting up
                    float s = Main.rand.NextFloat(12f, 20f);
                    p.Vel = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f) + wind * 0.5f, Main.rand.NextFloat(-1.5f, -0.7f));
                    p.StartSize = new Vector2(s, s * 2f);
                    p.EndSize = new Vector2(s * 2.2f, s * 5.5f);
                    p.MaxLife = Main.rand.Next(70, 140);
                    p.Alpha = Main.rand.NextFloat(0.22f, 0.4f);
                    p.Rot = Main.rand.NextFloat(-0.25f, 0.25f);
                    p.RotSpeed = Main.rand.NextFloat(-0.003f, 0.003f);
                }

                _puffs[_count++] = p;
            }
        }

        private static bool TryFindSurface(int x, int topY, int bottomY, out int surfaceY)
        {
            surfaceY = 0;
            if (x < 10 || x >= Main.maxTilesX - 10)
                return false;

            topY = Utils.Clamp(topY, 10, Main.maxTilesY - 10);
            bottomY = Utils.Clamp(bottomY, 10, Main.maxTilesY - 10);

            for (int y = topY; y < bottomY; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount < 128 || tile.LiquidType != LiquidID.Water)
                    continue;

                Tile above = Main.tile[x, y - 1];
                if (above.LiquidAmount > 0 || (above.HasTile && Main.tileSolid[above.TileType]))
                    continue;

                surfaceY = y;
                return true;
            }

            return false;
        }

        #region Puff textures

        // round falloff times fbm so each puff looks like a cloud, not a blurry circle
        private void BuildTextures()
        {
            Texture2D[] tex = new Texture2D[TexCount];

            for (int n = 0; n < TexCount; n++)
            {
                Color[] data = new Color[TexSize * TexSize];
                int seed = 1337 + n * 977;

                for (int y = 0; y < TexSize; y++)
                {
                    for (int x = 0; x < TexSize; x++)
                    {
                        float nx = (x + 0.5f) / TexSize * 2f - 1f;
                        float ny = (y + 0.5f) / TexSize * 2f - 1f;
                        float r = (float)Math.Sqrt(nx * nx + ny * ny);

                        float a = 0f;
                        if (r < 1f)
                        {
                            float falloff = Smooth(1f - r);
                            float noise = Fbm(nx * 2.2f + 8f, ny * 2.2f + 8f, seed);
                            a = MathHelper.Clamp(falloff * (0.35f + 1.1f * noise), 0f, 1f);
                        }

                        // premultiplied white
                        data[y * TexSize + x] = new Color(a, a, a, a);
                    }
                }

                tex[n] = new Texture2D(Main.instance.GraphicsDevice, TexSize, TexSize);
                tex[n].SetData(data);
            }

            _tex = tex;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float sx = Smooth(x - x0);
            float sy = Smooth(y - y0);

            float top = MathHelper.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), sx);
            float bottom = MathHelper.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), sx);
            return MathHelper.Lerp(top, bottom, sy);
        }

        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;

            for (int i = 0; i < 4; i++)
            {
                sum += ValueNoise(x * freq, y * freq, seed + i * 101) * amp;
                amp *= 0.5f;
                freq *= 2f;
            }

            return sum / 0.9375f;
        }

        #endregion
    }
}