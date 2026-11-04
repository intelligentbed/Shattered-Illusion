# Helpers

Everything below is new and standalone; no existing code was changed except `SandBall.cs` (metaball demo).
Namespaces: `ShatteredIllusion.Common.Utilities` and `ShatteredIllusion.Common.Visuals.*`.

| File | What it gives you |
|---|---|
| `Utilities/Ease.cs` | `Ease.OutCubic/InOutSine/OutBack/OutElastic/OutBounce...`, `Remap`, `InverseLerp`, `Approach`, `Hump`, `Envelope`, `SmoothDamp`, `Spring`/`Spring2` structs |
| `Utilities/MathX.cs` | `AngleDiff`, `TurnToward`, `TurnVelocity` (homing), `Intercept` (lead a moving target), `ArcVelocity` (lob to a point), Bezier/CatmullRom, `CircleRect`, `LineRect`, `InCone`, `InRing`, `PickWeighted`, `Noise1D`, vector extensions (`Perp`, `WithLength`, `LimitLength`, `StepToward`, `DirTo`) |
| `Utilities/WorldUtils.cs` | `IsSolidAt`, `GroundYBelow`, `CeilingYAbove`, `WallDistance`, `Raycast`, `BeamLength`, `HasLineOfSight`, `PushOutOfSolidUp`, `IsInLiquid`, `OnScreen`, world/screen conversion |
| `Utilities/Targeting.cs` | `Targeting.ClosestPlayer/ClosestEnemy/PlayersInRange/EnsureTarget/Home`; `Net.IsAuthority/IsOwner/SyncNPC/HostileProjectile/SpawnNPC/Fan/Ring` |
| `Utilities/Timers.cs` | `Cooldown`, `Phase`, `Meter` structs, `PosHistory` ring buffer |
| `Utilities/DrawUtils.cs` | `using (DrawUtils.Additive(sb))` batch scopes, `Line/Rect/CircleOutline/ConeOutline/Bezier`, `Glow`, `Centered`, `Afterimages` (projectile + NPC), `Gradient`, `Rainbow` |
| `Visuals/Primitives/TrailRenderer.cs` | Ribbon trails from a `PosHistory` or a projectile's `oldPos`, no shader |
| `Visuals/Metaballs/MetaballSystem.cs` | Gooey merging blobs (see below) |

## Quick examples

```csharp
// homing that leads nothing, just turns
var t = Targeting.ClosestPlayer(Projectile.Center, 900f, requireLoS: true);
if (t != null) Targeting.Home(Projectile, t.Center, 0.06f);

// aim a shot at a moving player
Vector2 aim = MathX.Intercept(NPC.Center, target.Center, target.velocity, 12f);

// telegraph that stops at walls
float len = WorldUtils.BeamLength(NPC.Center, dir, 1200f);
DrawUtils.Line(sb, NPC.Center, NPC.Center + dir * len, Color.Red * 0.5f, 3f);

// squash on landing
Spring squash = new Spring(0.2f, 0.75f); squash.Kick(0.35f); ... scaleY = 1f - squash.Update();

// server-safe boss projectile ring
Net.Ring(NPC.Center, 12, 7f, (p, v) => Net.HostileProjectile(NPC.GetSource_FromAI(), p, v, type, dmg));
```

## Metaballs

`MetaballSystem.Register(name, outline, body, highlight)` returns a `MetaballLayer`.

* `layer.Add(worldPos, radius)` every AI tick for each live blob.
* `layer.Spawn(...)` / `layer.Burst(...)` for self-managed droplets that fall, splat on tiles, and shrink away.
* Demo: `SandBall.cs` (Great Antlion spit). Head and trail merge into a stretchy goo, impact sprays droplets. It follows the `Cinematics` config toggle.
* Tuning on the layer: `Resolution` (0.5 soft/cheap, 1 crisp), thresholds, `HighlightLift`.
* Method: additive blobs into a render target, then an `AlphaTestEffect` threshold. No `.fx` compile needed.

## Not verified

None of this was compiled or run (no tModLoader here). Check the Metaball draw hook (`PostDrawTiles`) and the AlphaTestEffect pass first when you build.
