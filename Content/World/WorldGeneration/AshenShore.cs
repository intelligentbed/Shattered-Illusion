using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.WorldBuilding;
using ShatteredIllusion.Content.Tiles.BoilingOcean;

namespace ShatteredIllusion.Content.World.WorldGeneration
{
    // HOW THIS WORKS NOW (in-place conversion instead of clear-and-rebuild)
    //
    //  1. Setup() records the surface height of the terrain that is ALREADY there (vanilla ocean / forest) for every column.
    //  2. The designed shore + seafloor profile is blended into that real surface over SeamBlendWidth columns,
    //     so at the seam the new ground height == the existing ground height. No cliff, no box.
    //  3. ReshapeColumn() then only touches what has to change:
    //       - ground above the new surface gets dug out (and water added/removed)
    //       - gaps between the new surface and the old one get filled
    //       - everything below the surface is CONVERTED tile-by-tile (dirt -> AshenDirt, stone -> VolcanicStone, etc).
    //         Ores, gems, caves and structures that are already there are left alone.
    //  4. The amount of conversion fades in with a noise dither toward the seam and toward the bottom, so the
    //     biome "grows" out of the old terrain instead of being stamped on top of it.
    //  5. Deeper into the biome (past SeamBlendWidth) the designed shore/ocean is PLACED over whatever was there,
    //     like the original generator did, so the interior is clean strata instead of vanilla leftovers.
    //     The handoff from "convert" to "place" is dithered over PlaceBlendWidth columns so there is no visible line.
    public static class AshenShore
    {
        #region Tile / wall types

        public static ushort ShoreSandTile;
        public static ushort ShoreSandWall = WallID.None; // TODO: real wall for this layer eventually

        public static ushort SeafloorTile;
        public static ushort SeafloorWall = WallID.None;

        public static ushort VolcanicStoneTile;
        public static ushort VolcanicStoneWall = WallID.None;

        private static bool _initialized;

        public static void Init()
        {
            if (_initialized)
                return;

            ShoreSandTile = (ushort)ModContent.TileType<AshenSand>();
            SeafloorTile = (ushort)ModContent.TileType<AshenDirt>();
            VolcanicStoneTile = (ushort)ModContent.TileType<VolcanicStone>();

            _initialized = true;
        }

        // Vanilla tiles that get swapped to our soil layer (AshenSand cap / AshenDirt / deeper VolcanicStone).
        // Anything solid that is NOT in one of these two sets (ores, gems, wood, bricks, ...) is left exactly as it is.
        private static readonly HashSet<ushort> SoilTiles = new HashSet<ushort>
        {
            TileID.Dirt, TileID.Grass, TileID.CorruptGrass, TileID.CrimsonGrass, TileID.HallowedGrass,
            TileID.JungleGrass, TileID.MushroomGrass, TileID.Mud, TileID.ClayBlock,
            TileID.Sand, TileID.Ebonsand, TileID.Crimsand, TileID.Pearlsand,
            TileID.HardenedSand, TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand, TileID.HallowHardenedSand,
            TileID.Sandstone, TileID.CorruptSandstone, TileID.CrimsonSandstone, TileID.HallowSandstone,
            TileID.Silt, TileID.Slush, TileID.SnowBlock,
            TileID.IceBlock, TileID.CorruptIce, TileID.HallowedIce, TileID.FleshIce,
        };

        private static readonly HashSet<ushort> StoneTiles = new HashSet<ushort>
        {
            TileID.Stone, TileID.Ebonstone, TileID.Crimstone, TileID.Pearlstone,
            TileID.GreenMoss, TileID.BrownMoss, TileID.RedMoss, TileID.BlueMoss, TileID.PurpleMoss,
        };

        // Corruption / Crimson tiles and what they turn into when they are purged BELOW the biome (where the surrounding rock is still vanilla).
        // Inside the biome they become our own tiles instead.
        private static readonly Dictionary<ushort, ushort> EvilToVanilla = new Dictionary<ushort, ushort>
        {
            { TileID.Ebonstone, TileID.Stone }, { TileID.Crimstone, TileID.Stone },
            { TileID.CorruptGrass, TileID.Dirt }, { TileID.CrimsonGrass, TileID.Dirt },
            { TileID.Ebonsand, TileID.Sand }, { TileID.Crimsand, TileID.Sand },
            { TileID.CorruptHardenedSand, TileID.HardenedSand }, { TileID.CrimsonHardenedSand, TileID.HardenedSand },
            { TileID.CorruptSandstone, TileID.Sandstone }, { TileID.CrimsonSandstone, TileID.Sandstone },
            { TileID.CorruptIce, TileID.IceBlock }, { TileID.FleshIce, TileID.IceBlock },
        };

        private static readonly HashSet<ushort> EvilWalls = new HashSet<ushort>
        {
            WallID.EbonstoneUnsafe, WallID.CrimstoneUnsafe, WallID.CorruptGrassUnsafe, WallID.CrimsonGrassUnsafe,
            WallID.CorruptionUnsafe1, WallID.CorruptionUnsafe2, WallID.CorruptionUnsafe3, WallID.CorruptionUnsafe4,
            WallID.CrimsonUnsafe1, WallID.CrimsonUnsafe2, WallID.CrimsonUnsafe3, WallID.CrimsonUnsafe4,
        };

        #endregion

        #region Logging

        private static Mod ModInstance => ModContent.GetInstance<AshenShoreWorldGen>()?.Mod;

        private static void Log(string message) => ModInstance?.Logger.Info($"[AshenShore] {message}");

        private static void LogWarn(string message) => ModInstance?.Logger.Warn($"[AshenShore] {message}");

        #endregion

        #region Tunable constants

        public const int BiomeWidth = 700;

        public const int ShoreSurfaceOffset = 0;
        public const int WaterlineOffset = 10;
        public const int SeafloorDepth = 280;
        public const int SeafloorSolidBuffer = 100;

        public const int SeafloorRampWidth = 320;
        public const int ShallowSeafloorDepth = 110;

        public const int ShoreWidthMin = 170;
        public const int ShoreWidthMax = 230;

        // The designed beach peaks at (waterline - this). Lower it for a flatter beach. With the seam blend
        // the peak no longer shows up as a cliff, but it still sets how tall the dune/beach ridge gets.
        public const int BeachMaxHeightMin = 40;

        public const float BeachDescentSmoothness = 0.5f;

        public const float BeachNoiseMagnification = 0.03f;
        public const float BeachNoiseAmplitude = 6f;

        public const int ShelfRampWidth = 120;

        // The seafloor depth tapers to zero toward the seam over this many columns (design profile only)
        public const int RightEdgeTransitionWidth = 150;

        // How far below the seafloor the dirt layer runs before it transitions into Volcanic Stone
        public const int BackfillDepth = 90;

        public const float SurfaceWaveMagnification = 0.02f;
        public const float SurfaceWaveAmplitude = 7f;
        public const float DuneMagnification = 0.0045f;
        public const float DuneAmplitude = 16f;

        public const int ShoreSandCapDepth = 12;
        public const int ShoreSandTransitionDepth = 6;
        // Ground shallower than (surfaceY + this) gets the sand cap
        public const int ShoreSandScanBelowSurface = 60;

        // How many columns it takes the biome to fully take over from the existing terrain, counted from the seam.
        // Controls BOTH the height blend and the tile conversion dither.
        public const int SeamBlendWidth = 180;

        public const int TransitionDepth = 40;
        public const float TransitionNoiseMagnification = 0.08f;

        public const float DirtDepthWobbleMagnification = 0.008f;
        public const float DirtDepthWobbleAmplitude = 40f;

        public const float WaterlineWaveMagnification = 0.017f;
        public const float WaterlineWaveAmplitude = 6f;

        public static readonly (float centerXPercent, float centerYPercent, float radiusX, float radiusY)[] SeafloorPockets =
        {
            (0.08f, 0.50f, 65f, 20f),
            (0.22f, 0.55f, 85f, 23f),
            (0.34f, 0.48f, 100f, 25f),
            (0.44f, 0.55f, 95f, 26f),
            (0.54f, 0.50f, 75f, 22f),
        };

        public const float PocketEdgeNoiseMagnification = 0.05f;
        public const float PocketEdgeNoiseStrength = 10f;
        public const float PocketAngularWobbleStrength = 0.3f;

        //connections between pockets.
        public const int ChannelBaseHalfHeight = 14;
        public const float ChannelWidthNoiseAmplitude = 5f;

        // The save at the bottom of the surface layer
        public const int CaveWormCount = 16;
        public const int CaveWormMinLength = 70;
        public const int CaveWormMaxLength = 170;
        public const float CaveWormMinRadius = 2.5f;
        public const float CaveWormMaxRadius = 6.5f;
        public const int CaveNetworkMinDepthBelowFloor = 20;
        public const int CaveNetworkMaxDepthBelowFloor = 140;

        // I HATE LAVA
        public const int LavaFringeBandTop = 60;
        public const int LavaFringeBandBottom = 150;

        // Max column-to-column step of the DESIGNED profile (the existing terrain keeps its own shape)
        public const int MaxHeightStepPerColumn = 1;

        // ---- in-place conversion ----

        // Where to start looking for the existing ground, relative to the neighbor surface.
        // If this lands inside solid ground we walk upward to find the true top of the hill.
        public const int ExistingSurfaceScanAbove = 100;

        // How far below the surface tile conversion goes before it fades back out to untouched vanilla terrain
        public const int ConvertMargin = 60;
        public const int ConvertDepth = SeafloorSolidBuffer + BackfillDepth + TransitionDepth + ConvertMargin;
        public const int ConvertFadeWidth = 80;

        // Under open water, every vanilla cave / tunnel / water pocket this deep below the seafloor is sealed solid so
        // nothing vanilla can open into the ocean. Our own pockets and worms are carved AFTER this, so they are unaffected.
        // Keep it >= CaveNetworkMaxDepthBelowFloor + WormFloorSafetyMargin + CaveWormMaxRadius (~177) so our caves sit inside the sealed band.
        public const int OceanSealDepth = 200;

        // Past the seam blend zone, tiles switch from "convert what is there" to "place the designed terrain".
        // This is how many columns that handoff is dithered over.
        public const int PlaceBlendWidth = 120;

        // Trees, plants, pots, chests, vines etc. get wiped this deep below the surface
        public const int DecorClearDepth = 40;

        // The new surface always gets at least this much solid ground under it (cave openings get filled to this depth)
        public const int MinSurfaceCrust = 6;

        // In the blend zone vanilla decoration is thinned out in chunks this wide instead of cut off in a straight line
        public const int DecorCellWidth = 12;

        // ---- cleanup of things vanilla puts where we don't want them ----

        // Corruption/Crimson tiles and walls inside the biome are always converted (not dithered) once the conversion
        // weight passes EvilForceMinWeight, so an evil biome crossing the seam still fades out instead of being cut off.
        public const bool PurgeEvilBiome = true;
        public const float EvilForceMinWeight = 0.25f;

        // evil tiles are also swapped for their plain vanilla versions this far below the biome, so chasms don't poke up into it
        public const int EvilPurgeExtraDepth = 200;

        // Chests (wooden, ocean, cabin...) inside the biome are removed if they sit in water or within this many tiles
        // of the ground surface. Deeper dry chests are left alone.
        public const int ShoreChestClearDepth = 30;

        // No open water this close to the seam, so a valley right at the border can't flood the neighbor
        public const int SeamWaterGuard = 20;

        #endregion

        #region Saved result of generation

        /// <summary>
        /// Depth (tile y) where the generated shore actually ends, i.e. the deepest seafloor plus the dirt
        /// layer under it (where the terrain turns to Volcanic Stone). 0 means "unknown" (world made before
        /// this existed, or generation was skipped). Saved with the world by AshenShoreWorldGen, and used by
        /// AshenShoreBiome.AreaBottom so the biome area matches the terrain that was really generated.
        /// </summary>
        public static int GeneratedShoreBottomY;

        #endregion

        #region Per-generation state (reset every time Setup() runs)

        private static int _worldSeed;

        // Number of columns actually generated
        private static int _width;

        // Surface height of the neighboring biome at the seam
        private static int _surfaceY;

        // Neighboring biome material at the seam (used for tiles we have to place newly near the seam)
        private static ushort _neighborCapTileType;
        private static ushort _neighborCapWallType;

        // Randomized once per world
        private static int _shoreWidth;

        // Vertical extent of everything we may touch
        private static int _clearTopY;
        private static int _affectedBottomY;

        // Height of the terrain that was there BEFORE we touched anything
        private static int[] _existingTopByX;

        // Final ground height for each column (beach or seafloor), blended with the existing terrain near the seam
        private static int[] _seafloorTopByX;

        private static bool[] _isDryBeachByX;
        private static bool[] _sandCapByX;
        private static int[] _dirtEndDepthByX;
        private static int[] _convertBottomByX;

        // debug counters
        private static int _tilesDug;
        private static int _tilesFilled;
        private static int _tilesConverted;
        private static int _tilesPlaced;

        #endregion

        #region Entry point

        public static void Generate(GenerationProgress progress = null)
        {
            if (!Setup())
            {
                LogWarn("Could not find the neighboring ground at the seam - Ashen Shore generation skipped.");
                return;
            }

            try
            {
                progress?.Set(0.00);
                RemoveLeftoverDebris();

                progress?.Set(0.10);
                ReshapeTerrain();

                progress?.Set(0.50);
                CarveSeafloorPockets();

                progress?.Set(0.58);
                CarveCaveNetwork();

                progress?.Set(0.65);
                ScatterWaterClutter();

                progress?.Set(0.75);
                SmoothPass();

                progress?.Set(0.80);
                SuppressLavaFringe();
                PurgeOrphanedChestsAndSigns();
                FrameAffectedArea();

                Log($"dug {_tilesDug}, filled {_tilesFilled}, converted {_tilesConverted}, placed {_tilesPlaced} tiles");

                progress?.Set(1.00);
            }
            finally
            {
                _existingTopByX = null;
                // _seafloorTopByX stays alive: the late cleanup pass still needs it (it nulls it when done)
                _isDryBeachByX = null;
                _sandCapByX = null;
                _dirtEndDepthByX = null;
                _convertBottomByX = null;
            }
        }

        private static bool Setup()
        {
            Init();

            _worldSeed = WorldGen.genRand.Next();
            _shoreWidth = WorldGen.genRand.Next(ShoreWidthMin, ShoreWidthMax + 1);
            _width = Math.Min(BiomeWidth, Main.maxTilesX - 5);

            _tilesDug = 0;
            _tilesFilled = 0;
            _tilesConverted = 0;
            _tilesPlaced = 0;
            GeneratedShoreBottomY = 0;

            if (!TryFindNeighborSurface(out int neighborSurfaceY, out _neighborCapTileType, out _neighborCapWallType))
                return false;

            _surfaceY = Math.Max(50, neighborSurfaceY + ShoreSurfaceOffset);
            _clearTopY = Math.Max(5, _surfaceY - 80);

            _existingTopByX = new int[_width];
            _seafloorTopByX = new int[_width];
            _isDryBeachByX = new bool[_width];
            _sandCapByX = new bool[_width];
            _dirtEndDepthByX = new int[_width];
            _convertBottomByX = new int[_width];

            // 1. what is already there (must happen before anything is modified)
            for (int x = 0; x < _width; x++)
                _existingTopByX[x] = FindExistingSurface(x);

            // 2. the designed profile, still with the hammered drop into the ocean
            var design = new int[_width];
            for (int x = 0; x < _width; x++)
                design[x] = ComputeDesignTop(x, ComputeWaterTop(x));

            SmoothHeightProfile(design, MaxHeightStepPerColumn);

            // 3. blend the design into the existing terrain. weight is 0 at the seam (=> existing height) and 1 inside the biome
            int deepest = 0;
            int deepestFloor = 0;
            for (int x = 0; x < _width; x++)
            {
                float weight = ConversionWeight(x);
                int mirroredX = _width - 1 - x;
                int waterTop = ComputeWaterTop(x);

                int groundTop = (int)Math.Round(MathHelper.Lerp(_existingTopByX[x], design[x], weight));
                groundTop = (int)MathHelper.Clamp(groundTop, 10, Main.maxTilesY - 10);

                _seafloorTopByX[x] = groundTop;
                _isDryBeachByX[x] = groundTop <= waterTop || mirroredX <= SeamWaterGuard;
                _sandCapByX[x] = groundTop <= _surfaceY + ShoreSandScanBelowSurface;
                _dirtEndDepthByX[x] = ComputeDirtEndDepth(x);
                _convertBottomByX[x] = Math.Min(Main.maxTilesY - 5, groundTop + ConvertDepth + ConvertFadeWidth);

                deepest = Math.Max(deepest, _convertBottomByX[x]);
                deepestFloor = Math.Max(deepestFloor, groundTop);
            }

            _affectedBottomY = Math.Min(Main.maxTilesY - 5, deepest + 1);

            // The shore ends where the dirt layer under the deepest seafloor hands over to Volcanic Stone.
            // Stay out of the underworld (it starts ~200 tiles above the bottom of the world).
            GeneratedShoreBottomY = Math.Min(
                Main.maxTilesY - 200,
                deepestFloor + SeafloorSolidBuffer + BackfillDepth);

            Log($"surfaceY = {_surfaceY}, cap tile = {_neighborCapTileType}, cap wall = {_neighborCapWallType}, shoreWidth = {_shoreWidth}, width = {_width}, deepestFloor = {deepestFloor}, shoreBottomY = {GeneratedShoreBottomY}");
            return true;
        }

        // samples a few columns past the seam and takes the median height so one dumb tree/floating island/dungeon
        // hill can't fuck it up false if no ground found at all
        private static bool TryFindNeighborSurface(out int surfaceY, out ushort capTile, out ushort capWall)
        {
            surfaceY = 0;
            capTile = TileID.Dirt;
            capWall = 0;

            const int sampleCount = 9;
            const int sampleSpacing = 6;

            int startY = Math.Max(10, (int)GenVars.worldSurfaceLow - 20);
            int endY = Math.Min(Main.maxTilesY - 10, (int)Main.worldSurface + 100);

            var samples = new List<(int y, ushort tile, ushort wall)>();

            for (int i = 0; i < sampleCount; i++)
            {
                int x = _width + 1 + i * sampleSpacing;
                if (x >= Main.maxTilesX - 10)
                    break;

                for (int y = startY; y < endY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!IsNaturalGround(tile))
                        continue;

                    samples.Add((y, tile.TileType, FindWallBelow(x, y)));
                    break;
                }
            }

            if (samples.Count == 0)
                return false;

            samples.Sort((a, b) => a.y.CompareTo(b.y));
            var median = samples[samples.Count / 2];

            surfaceY = median.y;
            capTile = median.tile;
            capWall = median.wall;
            return true;
        }

        private static bool IsNaturalGround(Tile tile)
        {
            if (!tile.HasTile)
                return false;

            ushort type = tile.TileType;

            if (!Main.tileSolid[type] || Main.tileSolidTop[type])
                return false;

            return type != TileID.Cloud && type != TileID.RainCloud && type != TileID.SnowCloud;
        }

        // wall sits behind the ground not above it so look a few tiles down yes this was a problem before
        private static ushort FindWallBelow(int x, int surfaceTileY)
        {
            for (int dy = 2; dy <= 12 && surfaceTileY + dy < Main.maxTilesY - 5; dy++)
            {
                ushort wall = Main.tile[x, surfaceTileY + dy].WallType;
                if (wall != 0)
                    return wall;
            }

            return 0;
        }

        // Top of the terrain that currently exists in this column (ocean floor, beach, hill...)
        private static int FindExistingSurface(int x)
        {
            int limit = Math.Min(Main.maxTilesY - 10, _surfaceY + SeafloorDepth + SeafloorSolidBuffer);
            int y = Math.Max(10, _surfaceY - ExistingSurfaceScanAbove);

            // started inside a hill that is taller than the scan window, walk up to its real top
            if (IsNaturalGround(Main.tile[x, y]))
            {
                while (y > 10 && IsNaturalGround(Main.tile[x, y - 1]))
                    y--;

                return y;
            }

            for (; y < limit; y++)
            {
                if (IsNaturalGround(Main.tile[x, y]))
                    return y;
            }

            return _surfaceY + WaterlineOffset + ShallowSeafloorDepth;
        }

        #endregion

        #region Steps

        // rails/platforms/rope left behind by vanilla structures
        private static void RemoveLeftoverDebris()
        {
            int maxY = Math.Min(Main.maxTilesY - 5, _affectedBottomY + 200);

            for (int x = 0; x < _width; x++)
            {
                for (int y = 5; y < maxY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile)
                        continue;

                    ushort type = tile.TileType;
                    if (type != TileID.MinecartTrack && type != TileID.Platforms && type != TileID.Rope)
                        continue;

                    if (Main.wallDungeon[tile.WallType])
                        continue;

                    ClearTileKeepWall(x, y);
                }
            }
        }

        private static void ReshapeTerrain()
        {
            for (int x = 0; x < _width; x++)
                ReshapeColumn(x);
        }

        // The whole "replace instead of rebuild" idea lives here.
        private static void ReshapeColumn(int x)
        {
            int existingTop = _existingTopByX[x];
            int groundTop = _seafloorTopByX[x];
            int waterTop = ComputeWaterTop(x);
            bool hasWater = !_isDryBeachByX[x];
            float weight = ConversionWeight(x);
            float placeWeight = PlaceWeight(x);

            // only keep vanilla trees/plants if the ground under them is still where it was
            bool keepDecor = Math.Abs(groundTop - existingTop) <= 1 && KeepDecorInColumn(x, weight);

            int startY = Math.Max(5, Math.Min(_clearTopY, existingTop));
            int endY = _convertBottomByX[x];
            int decorBottomY = Math.Min(endY, Math.Max(groundTop, existingTop) + DecorClearDepth);
            int fillBelowY = Math.Max(existingTop, groundTop + MinSurfaceCrust);

            // ---- above the new surface: dig out whatever is there, then set water / no water ----
            for (int y = startY; y < groundTop; y++)
            {
                if (!InBounds(x, y))
                    continue;

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && (!keepDecor || !IsDecoration(tile.TileType)))
                {
                    tile.ClearTile();
                    _tilesDug++;
                }

                if (tile.WallType != 0 && !Main.wallDungeon[tile.WallType])
                    tile.WallType = 0;

                if (hasWater && y >= waterTop)
                {
                    tile.LiquidType = LiquidID.Water;
                    tile.LiquidAmount = 255;
                }
                else
                {
                    tile.LiquidAmount = 0;
                }
            }

            // ---- from the new surface down: fill gaps, strip decoration, convert existing tiles in place ----
            for (int y = groundTop; y < endY; y++)
            {
                if (!InBounds(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                int depth = y - groundTop;

                // ocean: seal vanilla caves under the seafloor, regardless of how far the convert -> place handoff has got
                if (hasWater && depth < OceanSealDepth && (!tile.HasTile || IsDecoration(tile.TileType)))
                {
                    FillTile(x, y, depth);
                    _tilesPlaced++;
                    continue;
                }

                // interior: place the designed terrain over whatever was there (caves, ores, water and all).
                // placeWeight ramps 0 -> 1 so vanilla caves/ores fade out gradually instead of stopping at a line
                if (placeWeight > 0f
                    && ShouldConvert(x, y, depth, weight)
                    && (placeWeight >= 0.999f || DitherPastThreshold(x, y, placeWeight, 1400)))
                {
                    FillTile(x, y, depth);
                    _tilesPlaced++;
                    continue;
                }

                // new ground is higher than the old one (or a cave opening right under the surface): this is the only place we add tiles
                if (y < fillBelowY && (!tile.HasTile || IsDecoration(tile.TileType)))
                {
                    FillTile(x, y, depth);
                    _tilesFilled++;
                    continue;
                }

                if (tile.HasTile && IsDecoration(tile.TileType))
                {
                    if (!keepDecor && y < decorBottomY)
                        tile.ClearTile();
                }
                else if (tile.HasTile)
                {
                    ConvertSolidInPlace(x, y, depth, weight);
                }

                ConvertWallInPlace(x, y, depth, weight);
            }

            if (PurgeEvilBiome && weight >= EvilForceMinWeight)
                PurgeEvilBelow(x, endY);
        }

        // Below the biome the terrain is still vanilla, so evil tiles become their plain equivalents and evil walls are removed
        private static void PurgeEvilBelow(int x, int startY)
        {
            int endY = Math.Min(Main.maxTilesY - 5, startY + EvilPurgeExtraDepth);

            for (int y = startY; y < endY; y++)
            {
                if (!InBounds(x, y))
                    continue;

                Tile tile = Main.tile[x, y];

                if (tile.WallType != 0 && EvilWalls.Contains(tile.WallType))
                    tile.WallType = 0;

                if (tile.HasTile && EvilToVanilla.TryGetValue(tile.TileType, out ushort replacement))
                {
                    tile.TileType = replacement;
                    tile.TileFrameX = 0;
                    tile.TileFrameY = 0;
                }
            }
        }

        // 0 at the seam (leave the existing terrain alone), 1 once we are SeamBlendWidth columns into the biome
        private static float ConversionWeight(int x)
        {
            int mirroredX = _width - 1 - x;
            return SmoothStep(MathHelper.Clamp(mirroredX / (float)Math.Max(1, SeamBlendWidth), 0f, 1f));
        }

        // 0 inside the seam blend zone (convert only), 1 once we are PlaceBlendWidth columns past it (place only)
        private static float PlaceWeight(int x)
        {
            int mirroredX = _width - 1 - x;
            return SmoothStep(MathHelper.Clamp((mirroredX - SeamBlendWidth) / (float)Math.Max(1, PlaceBlendWidth), 0f, 1f));
        }

        // fades the conversion out toward the bottom so there is no flat edge where the biome stops
        private static float ConversionDepthFade(int depth)
        {
            int fadeStart = ConvertDepth;
            if (depth <= fadeStart)
                return 1f;

            float t = (depth - fadeStart) / (float)Math.Max(1, ConvertFadeWidth);
            return 1f - SmoothStep(MathHelper.Clamp(t, 0f, 1f));
        }

        private static bool ShouldConvert(int x, int y, int depth, float weight)
        {
            float chance = weight * ConversionDepthFade(depth);

            if (chance >= 0.999f)
                return true;
            if (chance <= 0f)
                return false;

            return DitherPastThreshold(x, y, chance, 1300);
        }

        private static void ConvertSolidInPlace(int x, int y, int depth, float weight)
        {
            Tile tile = Main.tile[x, y];
            ushort type = tile.TileType;

            bool soil = SoilTiles.Contains(type);
            bool stone = !soil && StoneTiles.Contains(type);

            // ores, gems, wood, bricks, our own tiles... are not touched
            if (!soil && !stone)
                return;

            bool forcedEvil = PurgeEvilBiome && weight >= EvilForceMinWeight && EvilToVanilla.ContainsKey(type);
            if (!forcedEvil && !ShouldConvert(x, y, depth, weight))
                return;

            // only the type changes, so slopes / half blocks / the cave shape that is already there stay as they are
            tile.TileType = PickMaterial(x, y, depth, stone);
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
            tile.TileColor = 0;

            _tilesConverted++;
        }

        private static void ConvertWallInPlace(int x, int y, int depth, float weight)
        {
            Tile tile = Main.tile[x, y];
            ushort wall = tile.WallType;

            if (wall == 0 || Main.wallDungeon[wall])
                return;

            bool forcedEvil = PurgeEvilBiome && weight >= EvilForceMinWeight && EvilWalls.Contains(wall);
            if (!forcedEvil && !ShouldConvert(x, y, depth, weight))
                return;

            // while the biome walls are still WallID.None this just removes the old wall
            tile.WallType = WallForTile(PickMaterial(x, y, depth, false));
        }

        // decides what a tile becomes based on how deep it is: sand cap -> dirt -> dithers into volcanic stone
        private static ushort PickMaterial(int x, int y, int depth, bool stoneLike)
        {
            // natural rock stays rock, it just turns volcanic
            if (stoneLike)
                return VolcanicStoneTile;

            if (_sandCapByX[x] && depth < ShoreSandCapDepth)
            {
                int transitionStart = ShoreSandCapDepth - ShoreSandTransitionDepth;
                if (depth < transitionStart)
                    return ShoreSandTile;

                float p = (depth - transitionStart) / (float)Math.Max(1, ShoreSandTransitionDepth);
                return DitherPastThreshold(x, y, p, 900) ? SeafloorTile : ShoreSandTile;
            }

            int dirtEnd = _dirtEndDepthByX[x];
            if (depth >= dirtEnd)
            {
                float progress = Math.Min(1f, (depth - dirtEnd) / (float)Math.Max(1, TransitionDepth));
                if (DitherPastThreshold(x, y, progress, 700))
                    return VolcanicStoneTile;
            }

            return SeafloorTile;
        }

        private static ushort WallForTile(ushort tileType)
        {
            if (tileType == ShoreSandTile)
                return ShoreSandWall;
            if (tileType == SeafloorTile)
                return SeafloorWall;

            return VolcanicStoneWall;
        }

        // Brand new tile (only used where the new ground is higher than what was there). Near the seam it
        // uses the neighbor's material so the patch doesn't stand out.
        private static void FillTile(int x, int y, int depth)
        {
            ushort tileType = PickMaterial(x, y, depth, false);
            ushort wallType = WallForTile(tileType);

            ApplySeamBlend(x, y, ref tileType, ref wallType);
            SetSolid(x, y, tileType, wallType);
        }

        private static bool IsDecoration(ushort type) => !Main.tileSolid[type] || Main.tileSolidTop[type];

        // thins the vanilla trees/plants out in chunks across the blend zone instead of a hard cutoff
        private static bool KeepDecorInColumn(int x, float weight)
        {
            if (weight >= 1f)
                return false;

            return Hash(x / DecorCellWidth, 17, _worldSeed + 1500) >= weight;
        }

        private static int ComputeWaterTop(int x)
        {
            float waterNoise = FBM(x * WaterlineWaveMagnification, 100f, _worldSeed, 4) * WaterlineWaveAmplitude;
            return _surfaceY + WaterlineOffset + (int)waterNoise;
        }

        // The DESIGNED ground height. This is not used directly anymore, Setup() blends it into the existing terrain.
        private static int ComputeDesignTop(int x, int waterTop)
        {
            int mirroredX = _width - 1 - x;
            float rightFade = RightEdgeFade(x);

            int beachPeakY = Math.Min(_surfaceY, waterTop - BeachMaxHeightMin);
            float beachNoise = FBM(x * BeachNoiseMagnification, 500f, _worldSeed, 3) * BeachNoiseAmplitude;
            float beachT = MathHelper.Clamp(mirroredX / (float)Math.Max(1, _shoreWidth), 0f, 1f);
            float beachFalloff = (float)Math.Pow(Math.Sin((1f - beachT) * MathHelper.PiOver2), BeachDescentSmoothness);

            float seamLock = (float)Math.Pow(1f - beachT, 2f);

            int beachTop = (int)Math.Round(
                MathHelper.Lerp(
                    _surfaceY,
                    MathHelper.Lerp(waterTop, beachPeakY, beachFalloff) + beachNoise * seamLock,
                    seamLock
                )
            );

            if (mirroredX < _shoreWidth && beachTop < waterTop)
                return beachTop;

            float rampX = Math.Max(0, mirroredX - _shoreWidth);

            float shelfT = MathHelper.Clamp(rampX / (float)Math.Max(1, ShelfRampWidth), 0f, 1f);
            float shelfFalloff = SmoothStep(shelfT);
            int shelfDepth = (int)(ShallowSeafloorDepth * shelfFalloff);

            float deepT = MathHelper.Clamp((rampX - ShelfRampWidth) / (float)Math.Max(1, SeafloorRampWidth), 0f, 1f);
            float deepFalloff = SmoothStep(deepT);
            int deepDepth = (int)((SeafloorDepth - ShallowSeafloorDepth) * deepFalloff);

            // layered noise for varied terrain
            float rippleNoise = FBM(x * SurfaceWaveMagnification, 200f, _worldSeed, 4) * SurfaceWaveAmplitude;
            float duneNoise = FBM(x * DuneMagnification, 250f, _worldSeed, 3) * DuneAmplitude;
            float floorNoise = FBM(x * 0.025f, 300f, _worldSeed, 3) * 8f;

            int fullDepth = shelfDepth + deepDepth
                + (int)floorNoise + (int)(rippleNoise * 0.4f) + (int)(duneNoise * 0.6f);

            int seafloorDepth = Math.Max(0, (int)(fullDepth * rightFade));

            return waterTop + seafloorDepth;
        }

        private static int ComputeDirtEndDepth(int x)
        {
            float depthWobble = FBM(x * DirtDepthWobbleMagnification, 900f, _worldSeed, 3) * DirtDepthWobbleAmplitude;
            return Math.Max(0, SeafloorSolidBuffer + BackfillDepth - TransitionDepth + (int)depthWobble);
        }

        // clamps the diff between adjacent columns. runs both directions so a run of jumps gets spread out
        // instead of just clipping the first one and leaving the rest
        private static void SmoothHeightProfile(int[] heights, int maxStep)
        {
            for (int x = 1; x < heights.Length; x++)
                heights[x] = (int)MathHelper.Clamp(heights[x], heights[x - 1] - maxStep, heights[x - 1] + maxStep);

            for (int x = heights.Length - 2; x >= 0; x--)
                heights[x] = (int)MathHelper.Clamp(heights[x], heights[x + 1] - maxStep, heights[x + 1] + maxStep);
        }

        private static bool DitherPastThreshold(int x, int y, float progress, int seedOffset)
        {
            float dither = FBM(x * TransitionNoiseMagnification, y * TransitionNoiseMagnification, _worldSeed + seedOffset, 2) * 0.5f + 0.5f;
            return dither < progress;
        }

        // Only used for tiles we place newly. Near the seam they take the neighbor's material.
        private static void ApplySeamBlend(int x, int y, ref ushort tileHere, ref ushort wallHere)
        {
            if (x < _width - SeamBlendWidth)
                return;

            float seamProgress = (x - (_width - SeamBlendWidth)) / (float)Math.Max(1, SeamBlendWidth - 1);
            seamProgress = SmoothStep(MathHelper.Clamp(seamProgress, 0f, 1f));

            if (DitherPastThreshold(x, y, seamProgress, 1200))
            {
                tileHere = _neighborCapTileType;
                wallHere = _neighborCapWallType;
            }
        }

        private static float RightEdgeFade(int x)
        {
            float distanceFromEdge = _width - x;
            float t = MathHelper.Clamp(distanceFromEdge / RightEdgeTransitionWidth, 0f, 1f);
            return SmoothStep(t);
        }

        // Adds rock spires and debris to the open water
        private static void ScatterWaterClutter()
        {
            const int clusterSpacing = 45;
            int waterCheckY = _surfaceY + WaterlineOffset + 5;

            int startX = 30;
            int endX = _width - _shoreWidth - 30;

            for (int x = startX; x < endX; x += clusterSpacing)
            {
                int jitteredX = x + WorldGen.genRand.Next(-15, 16);
                if (jitteredX < 0 || jitteredX >= _width)
                    continue;

                if (!InBounds(jitteredX, waterCheckY) || Main.tile[jitteredX, waterCheckY].LiquidAmount == 0)
                    continue;

                if (IsNearSeafloorPocket(jitteredX))
                    continue;

                int floorY = FloorAt(jitteredX);
                int waterDepthHere = floorY - waterCheckY;

                if (waterDepthHere < 40)
                    continue;

                if (WorldGen.genRand.NextFloat() < 0.55f)
                    PlaceRockSpire(jitteredX, floorY, waterDepthHere);
                else
                    PlaceDebrisCluster(jitteredX, waterCheckY);
            }
        }

        private static bool IsNearSeafloorPocket(int x)
        {
            foreach (var pocket in SeafloorPockets)
            {
                int centerX = (int)(_width * pocket.centerXPercent);
                if (Math.Abs(x - centerX) < pocket.radiusX + 30)
                    return true;
            }

            return false;
        }

        // Places a tapered rock pillar rising from the seafloor.
        private static void PlaceRockSpire(int centerX, int floorY, int waterDepth)
        {
            int height = Math.Min(waterDepth - 25, WorldGen.genRand.Next(18, 55));
            if (height < 12)
                return;

            int baseHalfWidth = WorldGen.genRand.Next(3, 6);

            for (int i = 0; i < height; i++)
            {
                int y = floorY - i;
                float t = i / (float)height;
                int halfWidth = Math.Max(0, (int)(baseHalfWidth * (1f - t)) - WorldGen.genRand.Next(0, 2));

                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    int x = centerX + dx;
                    float edgeNoise = FBM(x * 0.1f, y * 0.1f, _worldSeed + 800, 2);
                    if (Math.Abs(dx) == halfWidth && edgeNoise < -0.2f)
                        continue;

                    PlaceSeafloorOrVolcanic(x, y, 0.75f);
                }
            }
        }

        // Places a few small rubble mounds on the seafloor
        private static void PlaceDebrisCluster(int centerX, int waterCheckY)
        {
            int clusters = WorldGen.genRand.Next(2, 5);

            for (int c = 0; c < clusters; c++)
            {
                int cx = centerX + WorldGen.genRand.Next(-12, 13);
                if (cx < 0 || cx >= _width)
                    continue;

                int floorY = FloorAt(cx);
                int waterDepth = floorY - waterCheckY;
                int radius = WorldGen.genRand.Next(2, 5);
                int riseHeight = Math.Min(waterDepth - 15, WorldGen.genRand.Next(3, 10));

                if (riseHeight < 2)
                    continue;

                int cy = floorY - WorldGen.genRand.Next(0, riseHeight);

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx * dx + dy * dy > radius * radius)
                            continue;

                        PlaceSeafloorOrVolcanic(cx + dx, cy + dy, 0.8f);
                    }
                }
            }
        }

        private static void PlaceSeafloorOrVolcanic(int x, int y, float seafloorChance)
        {
            bool seafloor = WorldGen.genRand.NextFloat() < seafloorChance;
            SetSolid(x, y, seafloor ? SeafloorTile : VolcanicStoneTile, seafloor ? SeafloorWall : VolcanicStoneWall);
        }

        //yum yum caves
        private static void CarveSeafloorPockets()
        {
            foreach (var pocket in SeafloorPockets)
            {
                int centerX = (int)(_width * pocket.centerXPercent);
                int centerY = FloorAt(centerX) + (int)(pocket.centerYPercent * SeafloorSolidBuffer);
                float radiusX = pocket.radiusX;
                float radiusY = pocket.radiusY;

                int minX = Math.Max(5, centerX - (int)radiusX - 15);
                int maxX = Math.Min(_width - 1, centerX + (int)radiusX + 15);
                int minY = Math.Max(5, centerY - (int)radiusY - 15);
                int maxY = Math.Min(Main.maxTilesY - 5, centerY + (int)radiusY + 15);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;

                        float angle = (float)Math.Atan2(dy, dx);
                        float angularNoise = FBM((float)Math.Cos(angle) * 2.5f + 40f, (float)Math.Sin(angle) * 2.5f + 40f, _worldSeed + 500, 3);
                        float wobble = 1f + angularNoise * PocketAngularWobbleStrength;

                        float edgeNoise = FBM(x * PocketEdgeNoiseMagnification, y * PocketEdgeNoiseMagnification, _worldSeed + 500, 3) * PocketEdgeNoiseStrength;

                        float normDist = (float)Math.Sqrt((dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY));

                        if (normDist * wobble <= 1f + edgeNoise * 0.015f)
                            SetLiquid(x, y);
                    }
                }
            }

            for (int i = 0; i < SeafloorPockets.Length - 1; i++)
                ConnectPocketPair(i, i + 1);
        }

        // Connects two caves nexts to each other
        private static void ConnectPocketPair(int aIndex, int bIndex)
        {
            var a = SeafloorPockets[aIndex];
            var b = SeafloorPockets[bIndex];

            int ax = (int)(_width * a.centerXPercent);
            int bx = (int)(_width * b.centerXPercent);
            int ay = FloorAt(ax) + (int)(a.centerYPercent * SeafloorSolidBuffer);
            int bY = FloorAt(bx) + (int)(b.centerYPercent * SeafloorSolidBuffer);

            int startX = Math.Min(ax, bx);
            int endX = Math.Max(ax, bx);
            int steps = Math.Max(1, endX - startX);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = startX + (int)((endX - startX) * t);
                int centerY = ax <= bx ? (int)MathHelper.Lerp(ay, bY, t) : (int)MathHelper.Lerp(bY, ay, t);

                float wobble = FBM(x * 0.04f, 600f, _worldSeed, 2) * 5f;
                int channelY = centerY + (int)wobble;

                float widthNoise = FBM(x * 0.06f, 650f, _worldSeed, 2) * ChannelWidthNoiseAmplitude;
                int channelHalfHeight = ChannelBaseHalfHeight + (int)widthNoise;

                for (int y = channelY - channelHalfHeight; y <= channelY + channelHalfHeight; y++)
                    SetLiquid(x, y);
            }
        }

        private static void CarveCaveNetwork()
        {
            int caveZoneEndX = Math.Max(20, _width - _shoreWidth - 20);

            int minStartSpacing = Math.Max(10, (caveZoneEndX - 20) / (CaveWormCount * 2));
            var usedStartXs = new List<int>();

            for (int i = 0; i < CaveWormCount; i++)
            {
                int startX;
                int attempts = 0;
                do
                {
                    startX = WorldGen.genRand.Next(20, caveZoneEndX);
                    attempts++;
                }
                while (attempts < 10 && usedStartXs.Exists(x => Math.Abs(x - startX) < minStartSpacing));

                usedStartXs.Add(startX);

                int floorY = FloorAt(startX);
                int startY = floorY + WorldGen.genRand.Next(CaveNetworkMinDepthBelowFloor, CaveNetworkMaxDepthBelowFloor);

                CarveCaveWorm(startX, startY);
            }
        }

        private const int WormFloorSafetyMargin = 30;

        private static void CarveCaveWorm(int startX, int startY)
        {
            int length = WorldGen.genRand.Next(CaveWormMinLength, CaveWormMaxLength + 1);
            float angle = WorldGen.genRand.NextFloat(0f, MathHelper.TwoPi);
            float x = startX;
            float y = startY;
            int wormSeed = WorldGen.genRand.Next();

            for (int step = 0; step < length; step++)
            {
                float t = step / (float)length;

                float turn = FBM(step * 0.05f, 0f, wormSeed, 2) * 0.6f;
                angle += turn;

                x += (float)Math.Cos(angle);
                y += (float)Math.Sin(angle) * 0.6f;

                int ix = (int)x;
                int iy = (int)y;

                if (ix < 5 || ix >= _width - 5 || !InBounds(ix, iy))
                    break;

                // keep worms under the seafloor instead of letting them tunnel out through the beach
                int safeMinY = FloorAt(ix) + CaveNetworkMinDepthBelowFloor;
                if (iy < safeMinY)
                {
                    iy = safeMinY;
                    y = iy;
                    angle = Math.Cos(angle) >= 0 ? 0.4f : MathHelper.Pi - 0.4f; // keep the x direction, head back down
                }

                int safeMaxY = FloorAt(ix) + CaveNetworkMaxDepthBelowFloor + WormFloorSafetyMargin;
                if (iy > safeMaxY)
                {
                    iy = safeMaxY;
                    y = iy;
                    angle = -Math.Abs(angle);
                }

                float endTaper = Math.Min(1f, Math.Min(t, 1f - t) * 3f);
                float radiusNoise = FBM(step * 0.08f, 300f, wormSeed, 2) * 0.5f + 0.5f;
                float radius = MathHelper.Lerp(CaveWormMinRadius, CaveWormMaxRadius, radiusNoise) * endTaper;

                int r = Math.Max(1, (int)radius);

                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (dx * dx + dy * dy > r * r)
                            continue;

                        SetLiquid(ix + dx, iy + dy);
                    }
                }
            }
        }

        // Smooths solid tiles within the biome
        private static void SmoothPass()
        {
            int maxX = Math.Min(_width - 2, Main.maxTilesX - 5);
            int minY = Math.Max(5, _surfaceY - 70);
            int maxY = Math.Min(_affectedBottomY, Main.maxTilesY - 5);

            for (int x = 2; x < maxX; x++)
            {
                for (int y = minY; y < maxY - 2; y++)
                {
                    if (Main.tile[x, y].HasTile)
                        Tile.SmoothSlope(x, y, false);
                }
            }
        }

        private static void SuppressLavaFringe()
        {
            for (int x = 0; x < _width; x++)
            {
                int bandTop = Math.Max(_clearTopY, _convertBottomByX[x] - LavaFringeBandTop);
                int bandBottom = Math.Min(Main.maxTilesY - 5, _convertBottomByX[x] + LavaFringeBandBottom);

                for (int y = bandTop; y < bandBottom; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidType != LiquidID.Lava || tile.LiquidAmount == 0)
                        continue;

                    if (tile.HasTile && Main.tileSolid[tile.TileType])
                        SetSolid(x, y, VolcanicStoneTile, VolcanicStoneWall);
                    else
                        SetLiquid(x, y);
                }
            }
        }

        // The old version nulled every chest/sign in a rectangle. Now only the tiles in the decoration band get wiped,
        // and chests deeper down are left alone, so this removes the chest/sign DATA only where the tile is actually gone.
        private static void PurgeOrphanedChestsAndSigns()
        {
            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null || chest.x < 0 || chest.x >= _width || chest.y < 5 || chest.y >= _affectedBottomY)
                    continue;

                Tile tile = Main.tile[chest.x, chest.y];
                if (!tile.HasTile || !Main.tileContainer[tile.TileType])
                    Main.chest[i] = null;
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                Sign sign = Main.sign[i];
                if (sign == null || sign.x < 0 || sign.x >= _width || sign.y < 5 || sign.y >= _affectedBottomY)
                    continue;

                Tile tile = Main.tile[sign.x, sign.y];
                if (!tile.HasTile || !Main.tileSign[tile.TileType])
                    Main.sign[i] = null;
            }
        }

        // Runs as its own pass near the END of worldgen. Vanilla passes that come after "Settle Liquids Again"
        // (Micro Biomes: cabins, booby-trapped chests, etc.) can place chests into the finished ocean/shore.
        public static void CleanupLateGeneration()
        {
            if (_seafloorTopByX == null)
                return;

            try
            {
                RemoveStrayChests();
            }
            finally
            {
                _seafloorTopByX = null;
            }
        }

        private static void RemoveStrayChests()
        {
            int removed = 0;

            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null || chest.x < 0 || chest.x >= _width || chest.y < _clearTopY || chest.y >= _affectedBottomY)
                    continue;

                if (!InBounds(chest.x, chest.y))
                    continue;

                Tile origin = Main.tile[chest.x, chest.y];
                int w = 2;
                int h = 2;

                if (origin.HasTile)
                {
                    TileObjectData data = TileObjectData.GetTileData(origin);
                    if (data != null)
                    {
                        w = data.Width;
                        h = data.Height;
                    }
                }

                bool inWater = false;
                for (int dx = 0; dx < w && !inWater; dx++)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        if (InBounds(chest.x + dx, chest.y + dy) && Main.tile[chest.x + dx, chest.y + dy].LiquidAmount > 0)
                        {
                            inWater = true;
                            break;
                        }
                    }
                }

                bool onShore = chest.y <= FloorAt(chest.x) + ShoreChestClearDepth;
                if (!inWater && !onShore)
                    continue;

                for (int dx = 0; dx < w; dx++)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        int cx = chest.x + dx;
                        int cy = chest.y + dy;
                        if (!InBounds(cx, cy))
                            continue;

                        Tile cell = Main.tile[cx, cy];
                        if (cell.HasTile && Main.tileContainer[cell.TileType])
                            cell.ClearTile();
                    }
                }

                Main.chest[i] = null;
                removed++;
            }

            Log($"removed {removed} stray chests");
        }

        // tiles are written directly with no per-tile framing, so re-frame the whole area once at the end
        private static void FrameAffectedArea()
        {
            int topY = Math.Max(1, _clearTopY - 2);
            int bottomY = Math.Min(Main.maxTilesY - 2, _affectedBottomY + 200);

            WorldGen.RangeFrame(0, topY, _width + 1, bottomY);
        }

        #endregion

        #region Tile helpers

        private static bool InBounds(int x, int y) => WorldGen.InWorld(x, y, 5);

        private static int FloorAt(int x)
        {
            int clamped = Math.Max(0, Math.Min(x, _seafloorTopByX.Length - 1));
            return _seafloorTopByX[clamped];
        }

        private static void SetSolid(int x, int y, ushort tileType, ushort wallType)
        {
            if (!InBounds(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = tileType;
            tile.IsHalfBlock = false;
            tile.Slope = SlopeType.Solid;
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
            tile.TileColor = 0;
            tile.WallType = wallType;
            tile.LiquidAmount = 0;
        }

        // clears the tile AND the wall, fills with water
        private static void SetLiquid(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.ClearTile();
            tile.WallType = 0;
            tile.LiquidType = LiquidID.Water;
            tile.LiquidAmount = 255;
        }

        private static void ClearTileKeepWall(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            Main.tile[x, y].ClearTile();
        }

        #endregion

        #region Noise
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

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float sx = SmoothStep(x - x0);
            float sy = SmoothStep(y - y0);

            float n00 = Hash(x0, y0, seed);
            float n10 = Hash(x1, y0, seed);
            float n01 = Hash(x0, y1, seed);
            float n11 = Hash(x1, y1, seed);

            float ix0 = MathHelper.Lerp(n00, n10, sx);
            float ix1 = MathHelper.Lerp(n01, n11, sx);
            return MathHelper.Lerp(ix0, ix1, sy);
        }

        private static float FBM(float x, float y, int seed, int octaves, float gain = 0.5f, float lacunarity = 2f)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float sum = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = ValueNoise(x * frequency, y * frequency, seed + i * 101) * 2f - 1f;
                sum += n * amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum;
        }

        #endregion
    }

    // registers the pass, runs before "Settle Liquids Again" so our water actually settles
    public class AshenShoreWorldGen : ModSystem
    {
        public override void PostSetupContent()
        {
            AshenShore.Init();
        }

        // Remember where the generated shore ends, so the biome area matches the real terrain
        public override void SaveWorldData(TagCompound tag)
        {
            tag["ShoreBottomY"] = AshenShore.GeneratedShoreBottomY;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            AshenShore.GeneratedShoreBottomY = tag.GetInt("ShoreBottomY");
        }

        public override void ClearWorld()
        {
            AshenShore.GeneratedShoreBottomY = 0;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(AshenShore.GeneratedShoreBottomY);
        }

        public override void NetReceive(BinaryReader reader)
        {
            AshenShore.GeneratedShoreBottomY = reader.ReadInt32();
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int settleLiquidsIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Settle Liquids Again"));
            int insertAt = settleLiquidsIndex == -1 ? tasks.Count : settleLiquidsIndex;

            tasks.Insert(insertAt, new AshenShoreGenPass());

            // second pass at the very end, after every vanilla pass that can drop chests into our biome
            int finalCleanupIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Final Cleanup"));
            tasks.Insert(finalCleanupIndex == -1 ? tasks.Count : finalCleanupIndex, new AshenShoreCleanupGenPass());
        }

        private class AshenShoreCleanupGenPass : GenPass
        {
            public AshenShoreCleanupGenPass() : base("Ashen Shore Cleanup", 0.1f)
            {
            }

            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
            {
                progress.Message = "Cleaning up Ashen Shore";
                AshenShore.CleanupLateGeneration();
            }
        }

        private class AshenShoreGenPass : GenPass
        {
            public AshenShoreGenPass() : base("Ashen Shore", 1f)
            {
            }

            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
            {
                progress.Message = "Creating Ashen Shore";
                AshenShore.Generate(progress);
            }
        }
    }
}