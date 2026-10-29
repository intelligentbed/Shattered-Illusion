# ScreenFX

Fullscreen filters. One shader pass, nothing running while idle, every effect is a call.

## Use it

```csharp
ScreenFX.Ripple(pos, 600f);                          // shockwave bend
ScreenFX.Aberration(1f, 20);                         // color split pulse
ScreenFX.Swirl(pos, 500f, 5f, Env.Timed(6, 6, 24));  // twist around a point
ScreenFX.Flash(Color.White, 0.4f, 8);                // fade to a color and back

VisualFX.Play(ScreenPresets.Slam, pos, scale: 1.5f); // presets are FxDefs, so PlayOn and ThreeDFX.Play work too
```

Anything not in the quick list goes through the generic calls and reaches every channel:

```csharp
ScreenFX.Pulse(Channel.Pixelate, 6f, 30);            // runs out on its own
var h = ScreenFX.Hold(Channel.Saturation, 0.2f);     // stays until released
h.Release();                                         // or ScreenFX.Release("tag")
ScreenFX.Add(Channel.Vignette, 0.8f, Env.Timed(10, 60, 30), new Color(120, 0, 0), follow: npc, tag: "boss");
```

- Calling a pulse every frame holds it at full.
- Same tag + channel retargets the running effect instead of stacking.
- `follow` releases it when that NPC/projectile dies. `ScreenFX.Release("tag")` ends a group.
- Different systems stack: Add channels add up, Max channels take the strongest, Mul channels multiply.

## Channels

Aberration, ZoomBlur, MotionX, MotionY, Wobble, WobbleFreq, WobbleSpeed, Glitch, Lens, Pixelate (px), Posterize,
Scanlines, Grain, Saturation, Contrast, Exposure, Brightness, Hue (radians), Tint, Invert, Vignette, Fade.
Tint, Vignette and Fade take a color. 1 is a strong amount for most. Saturation, Contrast, Exposure and the Wobble
multipliers take the value to reach (1 = unchanged). Point effects: Ripple, Swirl, Bulge (4 at once).

## Your own shader (no changes to the core)

```csharp
// in any ModSystem.OnModLoad
public static ScreenShader Wave;
Wave = ScreenFX.RegisterShader(Mod, "Wave", "Effects/Wave",
    (fx, level) => fx.Parameters["uStrength"]?.SetValue(level * 0.02f));   // must do nothing at level 0

// anywhere
ScreenFX.Play(Wave, 1f, Env.Pulse(30));
```

Compile the `.fx` with EasyXnb (HiDef), keep the `.xnb` next to it. A missing shader returns null and logs, it never breaks loading.

## Adding a built-in channel

Add it to `Channel` and `Infos` (same order, bump `ChannelCount`), declare `fx<Name>` in `ScreenFX.fx`, use it in `Warp` or `Finish`,
and recompile the shader. Binding and pass selection are automatic.

## Other mods

Paste the one `Call` override from `ScreenFXCall.cs` into your Mod class.

## Settings

`ScreenFilters`, `ScreenFilterIntensity` (warp/blur), `ScreenColorIntensity` (grades/overlays), `ScreenReduceFlashing`.

## Test

`/sfx list`, `/sfx slam`, `/sfx hold saturation 0.2`, `/sfx release`, `/sfx stop`.

## Cost

Color work only: 1 texture read. Warping or aberration: 3. Zoom or motion blur: 15. Idle: nothing.
