# Shattered Illusion: code audit

Audit date: 2026-10-13. Branch: `audit/optimization-pass`. Target: tModLoader v2026.08 (Terraria 1.4.4.9, .NET 8).

Scope: 81 C# files, about 12,800 lines, plus assets, localization and build config. Four areas were reviewed: runtime performance, multiplayer correctness, code structure, and load time / mod size.

How to read this: section 1 is the summary, section 2 is what this branch changed, section 3 is what was found and deliberately left for the team (with the reason), section 4 is a playtest checklist for the changes, section 5 is a checklist of tModLoader rules with sources, and section 6 is questions only the authors can answer.

The fixes were given an independent second review, which checked the multiplayer changes against the tModLoader assembly; its findings are folded in.

Nothing in this audit was playtested. The C# was compiled; behaviour claims come from reading the code. Items marked *(inferred)* depend on vanilla or tModLoader behaviour that was not verified in source.

## 1. Summary

**The mod is in better shape than most first mods.** It is on the current API throughout (no pre-1.4.4 leftovers), uses `ItemID`/`NPCID` constants rather than raw numbers, has no shaders or render targets to get wrong, and several parts are well designed: the `NPCBehaviorOverride` system in `Core/OverrideSystem/`, `DownedSystem`, `KingSlimeInstanceData`, and the texture caching in `GreatAntlionCharger`. Detours are unsubscribed on unload. Server-authority guards on NPC and projectile spawns are mostly right.

**What needed attention, most serious first:**

1. `main` did not compile. `Core/ILEditing/IHateWorldGenILChanges.cs:29` reads `AshenShore.BiomeWidth`, and `AshenShore.cs` was deleted in `ea1dbff`. Fixed here by restoring the file.
2. Two classes were both named `LivingWandProjectile`. Fixed here with a temporary rename.
3. A dedicated server would throw every tick while the Great Antlion's rubble telegraphs (`FallingRubble.cs`). Fixed here.
4. Multiplayer is partly wired: a packet with no handler, a boss summon that cannot work for clients, several projectiles that look wrong to other players. The simple ones are fixed here.
5. Parry does not work in multiplayer as designed, and the King Slime cutscene freeze reads the wrong player. These need a design decision and are not changed (section 3.1).
6. Two files hold a quarter of the code and most of the duplication (section 3.4).
7. Placeholder content is live: an inert Eye of Cthulhu, joke Journal text, joke audio (section 3.3).

**Performance** is not a serious problem today. Costs are concentrated in the two boss fights: textures looked up by string every frame, full NPC-array scans every tick, and sounds and dust spawned every tick of a window rather than once.

**Load time and mod size** are fine. All PNGs total 664 KB (largest 30 KB), audio is 776 KB, and there is one `ImmediateLoad` call, in the right place. No action needed beyond tidying duplicates.

## 2. Changed on this branch

Every change is meant to leave single-player gameplay, balance, timing and visuals as they were. The two exceptions are marked **audible**.

Build status: the C# compiles with 0 errors and 51 warnings, the same 51 as before the fixes (nearly all nullable-annotation warnings). The `.tmod` packaging step fails when run from the command line on the audit machine with an `FNA3D` native-library error, which is the local `dotnet` setup and not the mod; build inside tModLoader as usual.

### Build and load

| Change | Where |
|---|---|
| Restored `AshenShore.cs` from `f4fcb3f` so the dungeon IL edit compiles. The biome generates again. | `Content/World/WorldGeneration/AshenShore.cs` |
| Renamed the Proto wand's projectile to `LivingWandProtoProjectile` (class and texture) to end the duplicate name. Temporary; see 3.3. | `Content/Items/Weapons/Mage/LivingWandProto/` |
| Added `.gitignore` for `bin/`, `obj/`, `.vs/`, `.DS_Store`, `*.user`. | repo root |

### Multiplayer and server

| Change | Where |
|---|---|
| Particle calls in the rubble telegraph are skipped on a dedicated server, where the particle buffers are null. Previously this would throw every tick per rubble. | `FallingRubble.cs` `DoTelegraphDust` |
| Added the missing handler for packet 8 (`SyncAntlionDiveState`). Clients now receive the dive landing position instead of using 0. | `ShatteredIllusion.cs` `HandlePacket` |
| Added `SetStaticDefaults` to the Great Antlion: the frame count moved there from `SetDefaults`, and `NPCID.Sets.MPAllowedEnemies` is set so a multiplayer client can summon from the altar. | `GreatAntlionCharger.cs` |
| Starry Night's mini-stars are spawned only by the projectile's owner, so other machines no longer spawn duplicates. Count per burst is unchanged. | `StarryProjectile.cs` `Burst` |
| Mandible Shooter bullets send their start and target to other players (`netUpdate` after the fields are set). | `MandibleShooter.cs` |
| Great Club swing direction is read from the owner's mouse only and synced; other machines no longer use their own cursor. | `LivingWoodGreatClub.cs` |
| The protected highway rectangle is sent to clients, so tile and wall protection works client-side. | `HighwayGeneration.cs` `NetSend`/`NetReceive` |
| When King Slime recalls his minions, the server now tells clients they are gone (`SyncNPC`). Before, clients kept ghost minions that could still hurt the local player. | `KingSlime.cs` `RecallAllMinions` |
| The server sends the boss to clients before the cutscene packet, so the client no longer discards the packet for an NPC it has not heard of yet *(inferred from packet ordering; needs a multiplayer test)*. | `CutSceneProto.cs` `StartServerCutscene` |
| Another player disconnecting no longer resets your camera zoom. | `CutSceneProto.cs` `EndCutscene` |
| `GetGroundY` can no longer index outside the world. | `GreatAntlionCharger.cs` |
| `SandBall.Kill` renamed to `OnKill` (the old name is obsolete). Altar item's static registrations moved to `SetStaticDefaults`. | `SandBall.cs`, `AntlionBossAltar.cs` |

### Performance

| Change | Where |
|---|---|
| Textures that were looked up by string every frame are now requested once and cached: King Slime body and crown, the four ninja sheets, the clone, and the menu logo, pixel and background. | `KingSlime.cs`, `KingSlime.Ninja.cs`, `KingSlimeClone.cs`, `ShatteredIllusionMainMenu.cs` |
| The King Slime minion count (a scan of all 200 NPC slots) is computed at most once per tick instead of at every call. | `KingSlime.cs` `CountActiveMinions`, `KingSlimeInstanceData.cs` |
| The cutscene `GlobalNPC` now applies only to bosses that have a cutscene, instead of attaching an instance to every NPC in the game. | `CutSceneProto.cs` `UniversalBossCutsceneNPC` |
| The parry tutorial watcher checks the King Slime it is attached to instead of scanning all NPCs each tick. | `ParryTutorialSystem.cs` |
| Journal pages are word-wrapped once and reused, rather than re-split and re-joined every frame the journal is open. | `Journal.cs` |
| Removed an array allocated every third tick in the split attack. | `KingSlime.cs` |
| **Audible:** the King Slime split-windup roar plays once per split attack. It was being triggered on every tick of the 60-tick windup. Tracked with a local flag, not an exact tick, so clients that join the state late still hear it. | `KingSlime.cs` `DoSplitAttack`, `KingSlimeInstanceData.cs` |
| **Audible:** the Great Antlion launch roar plays once per launch. It was being triggered on each of the first 8 ticks. Same local-flag approach. | `GreatAntlionCharger.cs` `HandleLaunch` |

### Hygiene

| Change | Where |
|---|---|
| Removed unused template `using` lines. | 11 small item, tile, buff and biome files |
| `.csproj` dll paths all use `Lib\`, matching the folder (mixed case breaks on Linux). | `ShatteredIllusion.csproj` |
| `AncientShieldItem.PNG` renamed to `.png`. | `Content/Items/Accessories/` |
| Removed localization keys for content that no longer exists (`RustyKeys`, `Rustylocker`, `DeleteThisLater`, `MossTileItem`, `AntlionRelicItem`, old parry-bar config keys, old keybind and projectile names). | `Localization/en-US_Mods.ShatteredIllusion.hjson` |
| Removed four counters that were incremented and never read. | `WorldGenerationMisc.cs` |

## 3. Found and left for the team

These either change gameplay, need a design decision, or are large enough to be their own piece of work.

### 3.1 Multiplayer: needs design

| # | Where | Problem | Suggested direction |
|---|---|---|---|
| 1 | `Common/Players/ParrySystem/ParrySystem.cs:199-262`, `:113-129` | Parry success never reaches the server. `FreeDodge` runs on the hurt player's own client *(inferred: player damage is client-side in vanilla)*, which takes the branch at `:220-225` and returns without calling `IParryable.OnParried`. In multiplayer the boss is not stunned, the meter does not rise, there is no heal; the player just gets a free dodge. Starting a parry also waits on a server round trip (`:117-118`, `:156-169`), adding latency to a timing mechanic. | Decide the parry on the owning client, send "parry succeeded, attacker X" to the server, have the server validate and apply `OnParried`, meter and heal. |
| 2 | `ParrySystem.cs:264-289` | The Mycelial explosion calls `npc.StrikeNPC(hit)` on the server with no `NetMessage.SendStrikeNPC` *(inferred)*. Unreachable today because of item 1. | Fix together with item 1. |
| 3 | `KingSlime.cs:216-225` | `PreAI` reads `Main.LocalPlayer.GetModPlayer<CutscenePlayer>().IsCutsceneActive` to freeze the boss. That runs on the server and every client with a different "local player", so each machine freezes its own copy at different times while the server keeps simulating. `UniversalBossCutsceneNPC` (`CutSceneProto.cs:570-605`) sets `dontTakeDamage = true` and `KingSlime.cs:243` sets it back to false, so the result depends on hook order. | Drive the freeze from synced NPC state (a flag in `ai[]` or extra AI), not from a player. |
| 4 | `KingSlimeMinion.cs:56-72`, `:112-124`, `:389-392` | `SubPhase`, `SubPhaseTimer`, `OrbitAngle` are in `localAI`, which is not synced. `CanHitPlayer` depends on them and is evaluated on the hit player's client, so hits can land while the server shows the minion orbiting. | Move to `ai[]` or `SendExtraAI`. |
| 5 | `CutSceneProto.cs:559-647`, receive at `:151` | The cutscene packet is sent from `OnSpawn`, before the NPC's first sync, so the client drops it *(inferred)*. This branch sends a `SyncNPC` first as a minimal fix. Also, `cutsceneTimer` is a server-only field, so clients never run the end-of-intro logic themselves. | Confirm the fix in a multiplayer test. If it does not hold, send the packet on the first `PostAI`. |
| 6 | `GreatAntlionCharger.cs:658-672`, `:678`; `EyeofCthulhu.cs:36-52`, `:72-83` | `TargetClosest(true)` every tick, so the Antlion switches targets mid-attack. Despawn is hand-rolled and sets `active = false` on client and server independently. | Keep a target until it is invalid; use `NPC.EncourageDespawn` as ExampleMod's boss does. |
| 7 | `KingSlime.cs:1057-1084`, `:1031` | Re-merge and split sound, shake and particles are inside `if (IsAuthority)`, so other clients see and hear nothing. | Run cosmetic effects on every client; keep state changes on the authority. |
| 8 | `GreatAntlionCharger.cs` phase-2 dive | This branch wires up the existing dive-state packet. Players who join mid-dive still do not get the values. | Move `Phase2DiveLandingX`/`GroundY` into `SendExtraAI`/`ReceiveExtraAI` and delete packet 8. |
| 9 | `Content/Tiles/AntlionBossAltar.cs:84-93` | With the summon now permitted for clients, the multiplayer path uses vanilla `SpawnOnPlayer`: the boss appears at a random off-screen spot near the summoner (possibly outside the arena), a "has awoken!" chat line shows, and if no spot is found nothing spawns although the item was consumed. Single player spawns 12 tiles right of and 5 above the altar. Any client can also request the boss without the item, as with vanilla bosses. | Replace the vanilla message with a mod packet carrying the altar position; the server then spawns exactly as single player does, and `MPAllowedEnemies` can be removed. |

### 3.2 Balance and gameplay: authors' call

- **Boss HP ignores player count.** `GreatAntlionCharger.cs:226-238` overwrites `lifeMax` per difficulty and ignores `numPlayers`/`balance`.
- **Classic mode drops no weapons.** Loot at `GreatAntlionCharger.cs:240-247` is coins and a trophy; weapons only come from the Expert bag.
- **Master projectile damage is at or below Expert**: `DifficultyValue(10, 25, 15)` for spit, `(15, 30, 15)` for rubble. Hostile projectile damage is doubled by the game, and doubled again in Expert, so pass in roughly half the intended number.
- **`StarryProjectile` bursts twice** (`StarryProjectile.cs:99`, `:107`, `:117`): `OnTileCollide` and `OnHitNPC` call `Burst()`, then the kill calls `OnKill`, which calls it again. Ten mini-stars spawn where the code reads as if five were meant. Not changed, since fixing it halves the weapon's output.
- **Every player gets +5% move speed and -5% pick speed unconditionally** (`Common/Players/StartingPlayer.cs:19-20`).
- **`EvergreenJadeCharm`** tooltip says +5% melee speed; the code grants 10% (`EvergreenJadeCharm.cs:53`).
- **Desert chest loot chance** is `NextBool((int)Math.Round(1f / 0.30f))`, which is 1 in 3 (33%), not the 30% the constant suggests (`WorldGenerationMisc.cs`).
- **`KingSlime.cs:~902`** gates a `ChargeCoreParticle` on `timer == 0f`, but the timer is incremented before that code runs, so the particle likely never appears.
- **Tutorial hint count is per world** (`ParryTutorialSystem`), so in a shared world one player can use up all five hints for everyone.
- **Dust and light volume** in the Antlion fight: `FallingRubble.cs:122-197` (about 24 dusts and 24 particles per tick with 12 rubble in Master, saturating the 400-slot telegraph buffer), `GreatAntlionCharger.cs:1267-1300` (about 25 dusts per tick in Master), and `Content/Dusts/LanternDust.cs:20-31` (every dust adds light every tick for about 85 ticks). Thinning these is the next performance win, and it changes the look, so it is left to whoever owns the visuals. Options: spawn every second or third tick, share one telegraph per burst, set `noLight` on the dimmer dusts.

### 3.3 Placeholder content that is live

- **Eye of Cthulhu** (`Content/NPCs/BossAI/VanillaBosses/EoC/EyeofCthulhu.cs:34-71`, `:107-113`): the override returns `false` from `PreAI` and its only attack does nothing. It registers automatically, so with the mod enabled the Eye spawns and sits still. Left as is by decision; do not ship a release with it in this state.
- **Living Wand duplicate**: `LivingWand.cs` and `LivingWandProto.cs` are the same file apart from names. The Proto projectile was renamed to `LivingWandProtoProjectile` on this branch only to stop the name clash. Delete one of the two items.
- **Journal** (`Common/GUI/JournalUI/Journal.cs:94-147`): "BOOK NAME HERE", "SYMBOL HERE", keyboard-mash entries and joke text are visible in game.
- **Audio**: `Sounds/Silence.mp3` is byte-identical to `Sounds/ParrySounds/dry-fart.mp3`, and it is what the Great Antlion cutscene plays (`CutSceneProto.cs:534`). `Sounds/BarkFart.mp3` is the King Slime cutscene audio (`:548`). `RootPulse.mp3` is marked as a placeholder (`LivingWoodSet.cs:40`).
- **Duplicate placeholder art** (identical files): the three Frost armor pieces and both Living Wand sprites are one image; `AntlionAltar`/`AntlionBossAltar`; `SandBall`/`MandibleShooterBullet`; `SproutedBuff`/`SteeledBuff`; `ShieldBuff`/`AntlionAttractor`; `AshenDirtItem`/`AshenSandItem`.
- **`SpecialIronBricksItem`** tooltip says "[DEBUG]". `KingSlimeMinion.cs:14`: "TODO: GET A SPRITE".
- **`VolcanicStone`** has a tile and no item.
- **`description.txt` and `description_workshop.txt`** are still the template text.

### 3.4 Structure

- **Two files are 25% of the code**: `GreatAntlionCharger.cs` (1,955 lines) and `KingSlime.cs` (1,311). Longest methods: `HandlePhase2BurrowDive` 338 lines, `HandleBurrow` 278, `DoSplitAttack` 212. `GreatAntlionCharger.cs` has about 75 bare float literals and 27 comparisons of `Timer` against a literal; the same timing boundaries (55/75/91/111) appear separately in AI and draw code (`:132-133`, `:365`, `:400`, `:507`), with a comment at `:49` saying one "must match" another. Split each boss into partial classes by attack (King Slime already does this with `KingSlime.Ninja.cs`) and name the timings as constants shared by AI and draw.
- **Boss helpers are copied, not shared**: `ScreenShake` (`GreatAntlionCharger.cs:1939`, `KingSlime.cs:1197`), difficulty scaling (`DifficultyValue` in `GreatAntlionCharger.cs:301-313` versus `DifficultySpeedMultiplier` in `KingSlimeMinion.cs:49`), despawn constants (King Slime and EoC), and 24 near-identical `Dust.NewDustPerfect` blocks in the Antlion file. A small `BossUtils` static class would hold all of them. Calamity's public repo keeps a `Utilities/` folder for the same purpose.
- **Armor sets**: four sets repeat the same piece boilerplate and the same `IsArmorSet` check (`FrostArmor.cs`, `MossArmor.cs`, `MycelialArmor.cs`, `LivingWoodSet.cs`). A base class would remove about 200 lines.
- **Block tiles**: `AshenDirt`, `AshenSand`, `VolcanicStone`, `MossBlock`, `SpecialIronBricks` differ by a map colour and a few flags; a `SimpleBlockTile` base would cover them.
- **One-field ModPlayers**: `FrostArmorPlayer`, `MossArmorPlayer`, `ShieldPlayer`, `EvergreenJadeCharmPlayer` each hold a single flag for one item. Fine while small; merge if the list grows.
- **Texture paths as strings**: 40+ `"ShatteredIllusion/..."` literals. A constants class, or autoloaded `Texture` properties, would stop typos becoming runtime errors.
- **Files that do not match their contents**: `overshieldprototype.cs` holds `AncientShieldItem`, `ShieldPlayer` and `ShieldBuff`; `CutSceneProto.cs` holds 8 types; `Mossgen.cs` holds `MossWorldGen`; `StarHouse.cs` holds `StarHouseGen`; `HighwayGeneration.cs` also holds the tile and wall globals; `DownedSystem` lives in the Mod class file. One type per file, named after the type.
- **Namespaces that do not match folders**: worldgen files use `ShatteredIllusion.World.WorldGeneration` (except the restored `AshenShore.cs`, which uses `ShatteredIllusion.Content.World.WorldGeneration`); `ShatteredIllusionKeybinds.cs` uses namespace `ShatteredIllusionKeybinds`. Also, the class `ShatteredIllusion` sits inside namespace `ShatteredIllusion`, which is why code has to write relative names like `Core.OverrideSystem...`; renaming the class (`ShatteredIllusionMod`) would remove that friction but changes the mod's internal name handling, so check `build.txt`/internal name expectations first.
- **Cutscene registration** takes 11 positional numbers per boss (`CutSceneProto.cs:520-548`), hard-codes the duration twice (`:20`, `:609`), and needs edits in three places to add a boss. A small definition object per boss would fix all three.
- **Hard-coded English** instead of localization keys: all four set bonuses (`FrostArmor.cs:68`, `MossArmor.cs:77-79`, `MycelialArmor.cs:73-75`, `LivingWoodSet.cs:~81`), the parry hint (`ParryTutorialSystem.cs:117-118`), menu labels (`MenuHook.cs:38-43`), keybind names (`ShatteredIllusionKeybinds.cs:13-15`), Journal text, worldgen pass messages.
- **Localization file**: `Buffs.ShieldBuff.Description` and `Buffs.Bleeding.Description` are set to their own key names, so the raw key shows in game. `Bosses.GreatAntlionCharger.SpawnInfo` says "Underground Desert" with a hard-coded `[i:5948]`, while the code default (`ShatteredIllusion.cs:42`) says "Underground Railway" and uses the attractor item. `AntlionTrophy` and `LanternDust` have no keys.
- **`<Nullable>enable</Nullable>`** is on, but almost none of the code is written for it, which accounts for nearly all of the 51 build warnings. Either annotate properly or turn it off; today the warnings hide anything real.
- **Comments**: a number of comments are jokes, apologies or profanity (`CutSceneProto.cs:16`, `:20`; `WorldGenerationMisc.cs:28-29`; `KingSlimeInstanceData.cs:8-9`). Harmless, but they will be read by anyone who decompiles or contributes.

### 3.5 World generation

- **Moss under living trees** (`Mossgen.cs:106-171`) spawns 3-6 veins for every `LivingWood` tile in a column, so a 40-tile trunk spawns 120-280 overlapping veins in the same spot. Do it once per trunk column.
- **No overlap checks**: railway cabins are placed across the whole span at the highway's depth and can overlap the 329-wide arena or each other; star houses can overlap floating islands.
- **Edge checks**: `RailwayCabinGen.cs:93-94` and `StarHouse.cs:103-104` clamp the origin but do not subtract the structure's width, so a structure can run off the right edge. `HighwayGeneration.cs:120-128` does it correctly.
- **Pass order depends on class names**: `RailwayCabinGen.cs:27` looks up the highway pass by name, which only exists if `HighwayGeneration` ran `ModifyWorldGenTasks` first. That holds today because H sorts before R. Put both in one system, or have the cabin pass check `HighwayArea` at run time.
- **`StarHouse.cs:70`** gives up after 30 attempts and then uses an unchecked random X; `:28-31` falls back to "before the last pass" if "Settle Liquids" is missing.
- **If the arena fails to place**, the error is logged and the world has no boss arena, with nothing shown to the player.
- **Dungeon IL edit** (`IHateWorldGenILChanges.cs:84`) patches after the first `ldarg.0` in `WorldGen.DungeonHalls`. That is a loose match and will silently patch the wrong place if vanilla changes. Match a more specific instruction sequence. The class name should say what it does (`DungeonShoreOffset`).
- **Ashen Shore**: `AshenShore.cs` (1,062 lines) is restored as it was at `f4fcb3f` and was not reviewed line by line in this audit. `AshenShoreBiome` is still an empty class.

### 3.6 Smaller performance items

- `GreatAntlionCharger.GetGroundY` (`:620-636`) walks up to 120 tiles and is called every tick of the burrow pursuit and telegraph (`:1026`, `:1100`). Compute once per stage.
- `AnyPlayerPresentNearby` (`GreatAntlionCharger.cs:640-653`) scans all 255 player slots every tick with `Vector2.Distance`. Check every 10-30 ticks and use `DistanceSQ`.
- `NPCBehaviorOverrideGlobalNPC` (`Core/OverrideSystem/NPCBehaviorOverrideSystem.cs:158-259`) has no `AppliesToEntity`, so its hooks dispatch for every NPC. Each is an O(1) lookup, so the cost is small. Adding the filter needs care because the registry is built in `PostSetupContent`.
- `JournalUISystem.cs:68-83` builds a capturing delegate and a `new GameTime()` every frame; `SturdinessBarSystem.cs:26-33` shows the allocation-free version.
- `CutSceneProto.cs:107-110` splits and trims the title text every frame during a cutscene; `:236-266` draws each string 9 times for the outline.
- `SturdinessBarUI.Draw` (`:56-82`) draws three sprites every frame even when the meter is empty.
- `BossParticleSystem.cs:38-71` registers six particle buffers and has no `Unload`. Whether ParticleLibrary clears its registry on mod reload could not be checked from the dll; if it does not, each reload leaks six buffers.
- Dead code: `TelegraphBeamParticle` and `GroundGlowFillParticle` are never registered.

### 3.7 Project and process

- **`docs/` and `README.md` are probably packed into the `.tmod`**, since `build.txt` has no `buildIgnore` line *(not confirmed)*. Add `buildIgnore = docs/*, *.md` after checking how it combines with tModLoader's default ignores.

- **No CI, no tests.** A GitHub Action that runs the compile step would have caught the broken `main`.
- **The team commits through the GitHub web upload** ("Add files via upload", "Delete ..."). That is how a file got deleted while another still depended on it. Working from a local clone and building before pushing would prevent it.
- **Three dlls are committed** in `Lib/`. `ShatteredMusiMod.dll` is referenced by the `.csproj` but no type from it is used (the code uses `ModLoader.TryGetMod`), so the reference can go. `build.txt` lists `StructureHelper` and `ParticleLibrary` as hard references and has no `modloaderVersion` or `weakReferences` line.
- **`README.md`** is two lines. A short "how to build, how the folders are laid out, how to add a boss override" would help new contributors most.
- **Line endings and encoding are mixed** (68 CRLF files, 13 LF; 56 with a BOM). An `.editorconfig` and `.gitattributes` would pin them.
- **`MenuHook.cs:378-382`** reflects into private tModLoader members and replaces `Main.DrawMenu` without calling the original when the mod menu is active. It will break on a tModLoader update; wrap it so a failure falls back to the vanilla menu.
- **Journey mode and bestiary**: `Item.ResearchUnlockCount` is set for 2 of about 35 items. No NPC has a bestiary entry, and `KingSlimeClone.cs:15` adds the clone to `BossBestiaryPriority`.
- **Save data**: no ModPlayer saves anything, so `SturdinessMeter` and shield HP reset every session. Fine if intended. If a content class is renamed later, add `[LegacyName]` so existing saves keep the item.

## 4. Playtest checklist

None of the changes in section 2 has been run in game. Each line below is one thing to try and what should happen.

**Load**
- [ ] The mod builds and loads in tModLoader with no error about `LivingWandProjectile` or missing textures.
- [ ] Main menu with the mod's menu selected: logo, background and buttons draw as before.

**Single player**
- [ ] New world generates to completion. The Ashen Shore is present on the left edge and the dungeon is clear of it.
- [ ] Both Living Wand items fire and their projectiles have sprites.
- [ ] Ancient Shield accessory shows its sprite (the file was renamed).
- [ ] King Slime: body, crown and ninja sprites all draw through the whole fight. He shrinks and grows with the number of minions out, as before. The split-attack windup roars once.
- [ ] King Slime minions: they return and are absorbed as before; none linger after he recalls them or dies.
- [ ] Great Antlion: fight plays as before. Launch roars once. Rubble telegraphs show sparks, lines and shockwaves. The phase-2 dive lands where it should.
- [ ] Boss cutscenes still play for King Slime and the Great Antlion, and the camera zoom returns to normal afterwards.
- [ ] Parry tutorial hint still appears during King Slime's ninja stab and the Antlion's parryable attack.
- [ ] Journal: opens, pages read the same, page numbers update when turning, table of contents links work.
- [ ] Starry Night: stars burst into the same number of mini-stars as before.
- [ ] Living Wood Great Club: swings toward the cursor on first use, both directions.
- [ ] Desert chests in a new world still sometimes contain Dune Carapace.
- [ ] Item names, tooltips, config labels and keybind names all show text, not `Mods.ShatteredIllusion...` keys (beyond the two already known: Shield and Bleeding buff descriptions).

**Multiplayer, host and one client**
- [ ] Client uses the altar: the Great Antlion spawns (it should appear near the summoning player), and the item is consumed.
- [ ] Antlion phase-2 dive: on the client the boss follows the arc instead of jumping toward the left edge of the world.
- [ ] Client tries to break arena tiles inside the highway: the tile does not start breaking.
- [ ] Each player fires the Mandible Shooter and swings the Great Club while the other watches: path and direction match what the user sees.
- [ ] Starry Night used by either player: both see the same number of mini-stars, with no extras that pass through enemies.
- [ ] King Slime fight: when minions are recalled or absorbed they disappear for the client too. Both players hear the split-windup roar once.
- [ ] Boss intro cutscene plays for the client as well as the host, and both hear the Antlion's first launch roar.
- [ ] One player leaves during the other's boss cutscene: the remaining player's zoom is unaffected.

**Dedicated server**
- [ ] Start a dedicated server, fight the Great Antlion through a rubble attack, and check the server log has no `NullReferenceException` from `FallingRubble`.

## 5. tModLoader rules worth pinning to the wall

Sources are the official wiki and ExampleMod. `[v]` marks version-specific items.

| # | Rule | Source |
|---|---|---|
| 1 | Only the server (or single player) spawns NPCs, changes boss phases or changes the world. Guard with `Main.netMode != NetmodeID.MultiplayerClient`. | [Basic Netcode](https://github.com/tModLoader/tModLoader/wiki/Basic-Netcode) |
| 2 | Clients cannot talk to each other. Anything others must see goes to the server, and the server relays it. | [Intermediate netcode](https://github.com/tModLoader/tModLoader/wiki/Intermediate-netcode) |
| 3 | Read a packet in exactly the order it was written. Every `MessageType` needs a `case`. | [Intermediate netcode](https://github.com/tModLoader/tModLoader/wiki/Intermediate-netcode) |
| 4 | Send on events, not every frame. Let the server make random choices and broadcast the result. | [Intermediate netcode](https://github.com/tModLoader/tModLoader/wiki/Intermediate-netcode) |
| 5 | NPC state that drives behaviour lives in `ai[]`, or in `SendExtraAI`/`ReceiveExtraAI`. `localAI` and plain fields are not synced. Set `netUpdate = true` when it changes. | [Basic Netcode](https://github.com/tModLoader/tModLoader/wiki/Basic-Netcode) |
| 6 | Projectile code that reads the mouse or spawns child projectiles goes inside `if (Main.myPlayer == Projectile.owner)`, followed by `netUpdate = true`. | [Basic Projectile](https://github.com/tModLoader/tModLoader/wiki/Basic-Projectile) |
| 7 | Hostile projectiles from an NPC: guard with `netMode != MultiplayerClient`, use `NPC.GetSource_FromAI()`, pass `Main.myPlayer` as owner. | [Basic NPC](https://github.com/tModLoader/tModLoader/wiki/Basic-NPC) |
| 8 | Hostile projectile damage is doubled by the game, and again in Expert. Pass in about half of what you intend. | [Basic NPC](https://github.com/tModLoader/tModLoader/wiki/Basic-NPC) |
| 9 | Boss summons from a client need `NPCID.Sets.MPAllowedEnemies[Type] = true` on the boss. | [ExampleMod summon item](https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Items/Consumables/MinionBossSummonItem.cs) |
| 10 | Despawn bosses with `NPC.EncourageDespawn(n)`. | [ExampleMod boss](https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/NPCs/MinionBoss/MinionBossBody.cs) |
| 11 | When the server spawns or removes another NPC, sync it with `NetMessage.SendData(MessageID.SyncNPC, number: whoAmI)`. | same as 10 |
| 12 | Downed flags: reset in `ClearWorld`, save only when true, sync in `NetSend`/`NetReceive`. `DownedSystem` in this mod already does this. | [ExampleMod DownedBossSystem](https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Common/Systems/DownedBossSystem.cs) |
| 13 | ModPlayer data other players need: `CopyClientState`, `SendClientChanges`, `SyncPlayer`. | [ExampleMod stat player](https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Common/Players/ExampleStatIncreasePlayer.cs) |
| 14 | `[v]` Since v2025.05, accessory equip code can run on the server and on other clients. | [Update Migration Guide](https://github.com/tModLoader/tModLoader/wiki/Update-Migration-Guide) |
| 15 | Client-only objects (UI, particles, textures' `.Value`) are null on a dedicated server. Guard with `!Main.dedServ`. | [Debugging Multiplayer Usage Issues](https://github.com/tModLoader/tModLoader/wiki/Debugging-Multiplayer-Usage-Issues) |
| 16 | Request a texture once and keep the `Asset<Texture2D>` in a field. Never call `ModContent.Request` in `AI` or `PreDraw`. Use `ImmediateLoad` only when you need real dimensions right away. | [Assets](https://github.com/tModLoader/tModLoader/wiki/Assets) |
| 17 | On unload, null static references to mod objects and unsubscribe every detour and event. | [Fixing Mod Not Fully Unloaded](https://github.com/tModLoader/tModLoader/wiki/Fixing-Mod-Not-Fully-Unloaded) |
| 18 | Save keys are forever: add keys, do not rename them. Use `[LegacyName]` when renaming a class. Reset statics in `ClearWorld`. | [Saving and loading using TagCompound](https://github.com/tModLoader/tModLoader/wiki/Saving-and-loading-using-TagCompound) |
| 19 | All player-facing text goes in the hjson. | [Localization](https://github.com/tModLoader/tModLoader/wiki/Localization) |
| 20 | Every `GlobalNPC`/`GlobalProjectile` gets `AppliesToEntity`. Use `InstancePerEntity` only when it stores fields. | [GlobalType.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/GlobalType.cs) |
| 21 | Static registration (`Main.npcFrameCount`, `ItemID.Sets`, `NPCID.Sets`) goes in `SetStaticDefaults`, per-instance values in `SetDefaults`. | [Basic NPC](https://github.com/tModLoader/tModLoader/wiki/Basic-NPC) |
| 22 | `[v]` `Kill` became `OnKill` in v2023.08. Modded `NPC.aiStyle` defaults to -1 since v2023.11. | [Update Migration Guide](https://github.com/tModLoader/tModLoader/wiki/Update-Migration-Guide) |
| 23 | In anything that runs every tick, no `new` collections, no LINQ, no string building, no full scans of `Main.npc`/`Main.projectile`. Cache an index or a count instead. | General practice; no official tModLoader page covers it |
| 24 | Play a sound or spawn a burst on one tick of a window (`timer == 1`), not on every tick of it. | General practice |

No official tModLoader page on draw performance, trails or render targets was found; rules 23 and 24 are general engineering practice, not quoted guidance.

## 6. Questions for the authors

1. Is the Eye of Cthulhu override meant to be in the next release, or should it be switched off until it has attacks?
2. Is the mod meant to run on a dedicated server, or only host-and-play? The guards suggest the former; it does not look like it has been tested.
3. How should parry work in multiplayer: decided by the player's own client, or by the server?
4. Is Master projectile damage being lower than Expert intended?
5. Is the Ashen Shore in or out? The generator has been added and deleted three times.
6. Which Living Wand is the real one?
7. Should the parry tutorial count be per player instead of per world?
8. Should the boss spawn at the altar or at the player in multiplayer?
9. Are the weapons with no recipe (`LivingWand`, `StarryNight`, `LivingWoodGreatClub`, `MandibleShooter`, `DuneRendPincers`) bag-only by design?
10. Is `ShatteredMusiMod` required or optional? The code treats it as optional, the project file as required.
11. Should `SturdinessMeter` persist between sessions?
