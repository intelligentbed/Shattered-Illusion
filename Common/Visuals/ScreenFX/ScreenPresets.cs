using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ShatteredIllusion.Common.Visuals
{
    // Ready-made looks, built as FxDefs so they play with VisualFX.Play / PlayOn / ThreeDFX.Play and take pos, scale and tint.
    // Bursts end on their own. States (the *Tag ones) hold until you call ScreenFX.Release(ScreenPresets.TagX).
    public static class ScreenPresets
    {
        public const string TagTimeStop = "TimeStop";
        public const string TagDeath = "Death";
        public const string TagUnderwater = "Underwater";
        public const string TagHeat = "Heat";
        public const string TagRetro = "Retro";
        public const string TagNightmare = "Nightmare";
        public const string TagLowHealth = "LowHealth";

        public static readonly FxDef Shockwave = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Ripple(c.Pos, 800f * c.Scale, 36, c.Scale);
                ScreenFX.Aberration(0.6f * c.Scale, 14);
            });

        public static readonly FxDef Slam = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Ripple(c.Pos, 600f * c.Scale, 28, 0.9f * c.Scale);
                ScreenFX.ZoomBlur(0.5f * c.Scale, 10, c.Pos);
                ScreenFX.Aberration(0.8f * c.Scale, 12);
            });

        public static readonly FxDef Hit = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Aberration(0.35f * c.Scale, 8);
                ScreenFX.Flash(c.Hot, 0.1f * c.Scale, 4);
            });

        public static readonly FxDef Hurt = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Vignette(0.9f * c.Scale, new Color(160, 0, 0), 30);
                ScreenFX.Aberration(0.4f * c.Scale, 10);
            });

        public static readonly FxDef Teleport = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Swirl(c.Pos, 520f * c.Scale, 5f, Env.Timed(6, 6, 24));
                ScreenFX.ZoomBlur(0.8f * c.Scale, 22, c.Pos);
                ScreenFX.Aberration(c.Scale, 24);
            });

        public static readonly FxDef GlitchBurst = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Glitch(0.8f * c.Scale, 24);
                ScreenFX.Aberration(0.5f * c.Scale, 24);
            });

        public static readonly FxDef TimeStop = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Ripple(c.Pos, 1400f * c.Scale, 50, 1.3f * c.Scale);
                ScreenFX.Hold(Channel.Saturation, 0f, 12, tag: TagTimeStop);
                ScreenFX.Hold(Channel.Contrast, 1.3f, 12, tag: TagTimeStop);
                ScreenFX.Hold(Channel.Vignette, 0.35f, 25, Color.Black, tag: TagTimeStop);
            });

        public static readonly FxDef Death = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Saturation, 0.1f, 50, tag: TagDeath);
                ScreenFX.Hold(Channel.Contrast, 1.15f, 50, tag: TagDeath);
                ScreenFX.Hold(Channel.Vignette, 0.85f, 40, new Color(70, 0, 10), tag: TagDeath);
            });

        public static readonly FxDef Underwater = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Wobble, 0.8f, 30, tag: TagUnderwater);
                ScreenFX.Hold(Channel.Tint, 0.3f, 30, new Color(90, 160, 255), tag: TagUnderwater);
                ScreenFX.Hold(Channel.Saturation, 0.85f, 30, tag: TagUnderwater);
                ScreenFX.Hold(Channel.Vignette, 0.35f, 30, new Color(0, 40, 80), tag: TagUnderwater);
            });

        public static readonly FxDef Heat = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Wobble, 0.5f, 40, tag: TagHeat);
                ScreenFX.Hold(Channel.Tint, 0.12f, 40, new Color(255, 150, 70), tag: TagHeat);
            });

        public static readonly FxDef Retro = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Pixelate, 4f, 10, tag: TagRetro);
                ScreenFX.Hold(Channel.Posterize, 0.6f, 10, tag: TagRetro);
                ScreenFX.Hold(Channel.Scanlines, 0.4f, 10, tag: TagRetro);
            });

        public static readonly FxDef Nightmare = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Saturation, 0.45f, 60, tag: TagNightmare);
                ScreenFX.Hold(Channel.Contrast, 1.15f, 60, tag: TagNightmare);
                ScreenFX.Hold(Channel.Grain, 0.12f, 60, tag: TagNightmare);
                ScreenFX.Hold(Channel.Vignette, 0.7f, 60, new Color(40, 0, 20), tag: TagNightmare);
            });

        public static readonly FxDef LowHealth = new FxDef()
            .At(0, static c =>
            {
                ScreenFX.Hold(Channel.Vignette, 0.6f, 30, new Color(140, 0, 0), tag: TagLowHealth);
                ScreenFX.Hold(Channel.Saturation, 0.7f, 30, tag: TagLowHealth);
            });

        // By name, for commands and for ScreenFXCall. Add your own presets here to show up in /sfx list.
        public static readonly Dictionary<string, FxDef> All = new Dictionary<string, FxDef>(StringComparer.OrdinalIgnoreCase)
        {
            ["shockwave"] = Shockwave,
            ["slam"] = Slam,
            ["hit"] = Hit,
            ["hurt"] = Hurt,
            ["teleport"] = Teleport,
            ["glitchburst"] = GlitchBurst,
            ["timestop"] = TimeStop,
            ["death"] = Death,
            ["underwater"] = Underwater,
            ["heat"] = Heat,
            ["retro"] = Retro,
            ["nightmare"] = Nightmare,
            ["lowhealth"] = LowHealth
        };

        // Name of the tag a state preset holds under, or null for bursts. Used by /sfx release <preset>.
        public static string TagOf(string name) => name.ToLowerInvariant() switch
        {
            "timestop" => TagTimeStop,
            "death" => TagDeath,
            "underwater" => TagUnderwater,
            "heat" => TagHeat,
            "retro" => TagRetro,
            "nightmare" => TagNightmare,
            "lowhealth" => TagLowHealth,
            _ => null
        };
    }
}
