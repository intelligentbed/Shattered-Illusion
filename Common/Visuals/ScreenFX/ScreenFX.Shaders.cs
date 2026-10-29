using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.Visuals
{
    // A filter running your own shader. Get one from ScreenFX.RegisterShader and start it with ScreenFX.Play.
    public sealed class ScreenShader
    {
        internal string FilterName;
        internal Action<Effect, float> Apply;
        internal float Level;
        internal bool Active;

        internal ScreenShader() { }

        public bool IsActive => Active;
    }

    public static partial class ScreenFX
    {
        // Call from OnModLoad / PostSetupContent of any system or mod. Returns null (and logs) if the effect is missing,
        // and Play(null, ...) does nothing, so a missing shader never crashes the load.
        // pass = the pass name inside your .fx (null = its first pass). apply(effect, level) sets your uniforms.
        public static ScreenShader RegisterShader(Mod mod, string name, string effectPath, Action<Effect, float> apply,
            string pass = null, EffectPriority priority = EffectPriority.High)
        {
            if (Main.dedServ)
                return null;

            Asset<Effect> effect;

            try
            {
                effect = mod.Assets.Request<Effect>(effectPath, AssetRequestMode.ImmediateLoad);
            }
            catch (AssetLoadException)
            {
                mod.Logger.Warn($"ScreenFX shader '{name}' not found at {effectPath}.");
                return null;
            }

            var shader = new ScreenShader { FilterName = mod.Name + ":" + name, Apply = apply };

            Filters.Scene[shader.FilterName] = new Filter(new CustomData(effect, pass ?? effect.Value.CurrentTechnique.Passes[0].Name, shader), priority);
            Filters.Scene[shader.FilterName].Load();
            customs.Add(shader);

            return shader;
        }

        // Registers the three built-in filters. Passes missing from an older compiled effect are skipped.
        internal static void Register(Asset<Effect> effect)
        {
            if (Infos.Length != ChannelCount || Enum.GetValues<Channel>().Length != ChannelCount)
                throw new InvalidOperationException("ScreenFX channel table is out of sync with the Channel enum.");

            var passes = effect.Value.CurrentTechnique.Passes;

            for (int i = 0; i < FilterNames.Length; i++)
            {
                if (passes[PassNames[i]] == null)
                    continue;

                Filters.Scene[FilterNames[i]] = new Filter(new Data(effect, PassNames[i], i), EffectPriority.VeryHigh);
                Filters.Scene[FilterNames[i]].Load();
                have[i] = true;
            }

            registered = have[ModeFull];
        }

        private sealed class CustomData : ScreenShaderData
        {
            private readonly ScreenShader owner;

            public CustomData(Asset<Effect> effect, string pass, ScreenShader owner) : base(effect, pass)
            {
                this.owner = owner;
            }

            public override void Apply()
            {
                owner.Apply?.Invoke(Shader, owner.Level * warpScale);
                base.Apply();
            }
        }

        // A deactivated filter can keep drawing while it fades out, so any filter that is not the current mode
        // pushes identity values and its pass does nothing visible.
        private sealed class Data : ScreenShaderData
        {
            private readonly int id;
            private readonly EffectParameter[] channel = new EffectParameter[ChannelCount];
            private readonly Vector4[] pointA = new Vector4[MaxPoints];
            private readonly Vector4[] pointB = new Vector4[MaxPoints];
            private bool bound;

            private EffectParameter pPointA, pPointB, pResolution, pBlurCenter, pAspect, pTime, pTintColor, pVignetteColor, pFadeColor;

            public Data(Asset<Effect> effect, string pass, int id) : base(effect, pass)
            {
                this.id = id;
            }

            public override void Apply()
            {
                if (!bound)
                    Bind();

                Push(id == mode);
                base.Apply();
            }

            private void Bind()
            {
                var p = Shader.Parameters;

                for (int i = 0; i < ChannelCount; i++)
                    channel[i] = p["fx" + (Channel)i];

                pPointA = p["fxPointA"];
                pPointB = p["fxPointB"];
                pResolution = p["fxResolution"];
                pBlurCenter = p["fxBlurCenter"];
                pAspect = p["fxAspect"];
                pTime = p["fxTime"];
                pTintColor = p["fxTintColor"];
                pVignetteColor = p["fxVignetteColor"];
                pFadeColor = p["fxFadeColor"];
                bound = true;
            }

            private void Push(bool live)
            {
                for (int i = 0; i < ChannelCount; i++)
                    channel[i]?.SetValue(live ? uni[i] : Infos[i].Identity);

                Vector2 size = new Vector2(Main.screenWidth, Main.screenHeight);
                float zoom = Main.GameViewMatrix.Zoom.Y;

                for (int i = 0; i < MaxPoints; i++)
                {
                    if (!live || i >= pointCount)
                    {
                        pointA[i] = new Vector4(0f, 0f, 1f, 1f);
                        pointB[i] = default;
                        continue;
                    }

                    PointEval e = points[i];
                    Vector2 uv = WorldToUV(e.Pos);
                    float maxUV = e.Max * zoom / size.Y;

                    pointA[i] = new Vector4(uv.X, uv.Y, Math.Max(e.Radius * zoom / size.Y, 0.0001f), MathHelper.Clamp(maxUV * 0.3f, 0.03f, 0.12f));
                    pointB[i] = new Vector4(e.Ring, e.Swirl, e.Bulge, 0f);
                }

                Vector2 center = zoomCenterSet ? zoomCenter : Main.LocalPlayer.Center;

                pPointA?.SetValue(pointA);
                pPointB?.SetValue(pointB);
                pResolution?.SetValue(size);
                pBlurCenter?.SetValue(WorldToUV(center));
                pAspect?.SetValue(size.X / size.Y);
                pTime?.SetValue(Main.GlobalTimeWrappedHourly);
                pTintColor?.SetValue(live ? colorOut[0] : Vector3.One);
                pVignetteColor?.SetValue(live ? colorOut[1] : Vector3.Zero);
                pFadeColor?.SetValue(live ? colorOut[2] : Vector3.Zero);
            }
        }
    }

    public class ScreenFXSystem : ModSystem
    {
        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;

            try
            {
                ScreenFX.Register(Mod.Assets.Request<Effect>(ScreenFX.EffectPath, AssetRequestMode.ImmediateLoad));
            }
            catch (AssetLoadException)
            {
                Mod.Logger.Warn("ScreenFX effect not found, built-in screen filters disabled.");
            }

            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ScreenPresets).TypeHandle);
        }

        public override void PostUpdateEverything() => ScreenFX.Update();

        public override void OnWorldUnload()
        {
            if (!Main.dedServ)
                ScreenFX.Clear();
        }

        public override void Unload() => ScreenFX.Unload();
    }
}
