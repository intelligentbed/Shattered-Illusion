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
    //
    //  Fixes in this revision:
    //   - existing-surface heights are outlier-filtered, so a Corruption/Crimson chasm shaft can't pull a deep pit into the seam
    //   - the neighbour cap tile / wall is cleaned of evil variants so evil tiles aren't placed into the biome
    //   - evil purge starts almost at the seam, covers jungle grass + more walls, and clears evil decoration deep in chasms
    //   - the water scenery pass no longer excludes nearly the whole ocean (the old pocket check did)
    //   - the open ocean now has relief (ridges / trenches), tall pillars, arches, chimneys, floating rock and floor patches
    //
    //  Layout changes (so the biome is easy to move around and easy to extend downward):
    //   - the seafloor is terraced: wide flat shelves joined by short ramps instead of one long diagonal slope
    //   - the whole biome shares ONE flat base (GeneratedShoreBottomY): the dirt -> volcanic stone handoff and the
    //     end of the conversion no longer follow the floor shape, so the next layer can attach to a level boundary
    //   - a few wide descent shafts run from the seafloor down to landing chambers just above that base
    public static class AshenShore
    {
        #region Tile / wall types

        public static ushort ShoreSandTile { get; private set; }
        public static ushort ShoreSandWall { get; private set; } = WallID.None; // TODO: real wall for this layer eventually

        public static ushort SeafloorTile { get; private set; }
        public static ushort SeafloorWall { get; private set; } = WallID.None;

        public static ushort VolcanicStoneTile { get; private set; }
        public static ushort VolcanicStoneWall { get; private set; } = WallID.None;

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
            TileID.JungleGrass, TileID.CorruptJungleGrass, TileID.CrimsonJungleGrass,
            TileID.MushroomGrass, TileID.Mud, TileID.ClayBlock,
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
            { TileID.CorruptJungleGrass, TileID.Mud }, { TileID.CrimsonJungleGrass, TileID.Mud },
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
            WallID.CorruptHardenedSand, WallID.CrimsonHardenedSand, WallID.CorruptSandstone, WallID.CrimsonSandstone,
        };

        #endregion

        #region Logging

        private static Mod? ModInstance => ModContent.GetInstance<AshenShoreWorldGen>()?.Mod;

        private static void Log(string message) => ModInstance?.Logger.InfoFormat("[AshenShore] {0}", message);

        private static void LogWarn(string message) => ModInstance?.Logger.WarnFormat("[AshenShore] {0}", message);

        #endregion

        #region Tunable constants

        public const int BiomeWidth = 700;

        public const int ShoreSurfaceOffset = 0;
        public const int WaterlineOffset = 10;
        public const int SeafloorDepth = 280;
        public const int SeafloorSolidBuffer = 100;

        public const int SeafloorRampWidth = 320;
        public const int ShallowSeafloorDepth = 110;

        public const int ShoreWidthMin = 220;
        public const int ShoreWidthMax = 280;

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

        // Broad low-frequency relief (ridges and trenches) that fades in out in the deep, so the open ocean floor isn't one smooth ramp.
        public const float ReliefMagnification = 0.011f;
        public const float ReliefAmplitude = 55f;

        // Terraces: below TerraceStart the floor is snapped to flat shelves TerraceStep tiles apart. TerraceFlatFraction is how much of
        // each step is flat (the rest is the ramp down), TerraceStrength blends between the old smooth slope (0) and full terraces (1).
        public const float TerraceStart = 30f;
        public const float TerraceStep = 38f;
        public const float TerraceFlatFraction = 0.6f;
        public const float TerraceStrength = 0.85f;

        // Descent shafts: wide water shafts from the seafloor down to the base of the biome, each ending in a landing chamber.
        public static readonly float[] DescentShaftPercents = { 0.20f, 0.42f };
        public const int DescentShaftHalfWidth = 7;
        public const int DescentShaftMouthFlare = 6;
        public const int DescentShaftBottomMargin = 6;
        public const int DescentChamberHalfWidth = 24;
        public const int DescentChamberHeight = 16;

        // How thick the sand layer is (it wobbles by +-ShoreSandCapNoise along the beach so it isn't a uniform skin)
        public const int ShoreSandCapDepth = 32;
        public const int ShoreSandCapNoise = 10;
        public const int ShoreSandTransitionDepth = 12;
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

        // Tile conversion runs down to the shared biome base (GeneratedShoreBottomY) + ConvertMargin, then fades back out to
        // untouched vanilla terrain over ConvertFadeWidth. The depth is absolute, so the bottom edge is level across the biome.
        public const int ConvertMargin = 60;
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
        // (was 0.25, which left evil tiles untouched for the first ~60 columns from the seam)
        public const float EvilForceMinWeight = 0.05f;

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
        public static int GeneratedShoreBottomY { get; private set; }

        // ModSystem hooks are instance methods, so they go through this instead of writing the static property directly
        internal static void SetGeneratedShoreBottomY(int value) => GeneratedShoreBottomY = value;

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
        private static int[] _existingTopByX = Array.Empty<int>();

        // Final ground height for each column (beach or seafloor), blended with the existing terrain near the seam
        private static int[] _seafloorTopByX = Array.Empty<int>();

        private static bool[] _isDryBeachByX = Array.Empty<bool>();
        private static bool[] _sandCapByX = Array.Empty<bool>();
        private static int[] _dirtEndDepthByX = Array.Empty<int>();
        private static int[] _convertBottomByX = Array.Empty<int>();

        // Shared flat base of the biome (where the dirt layer has fully handed over to volcanic stone) and where the conversion starts to fade
        private static int _layerBottomY;
        private static int _convertFadeStartY;

        // Column of each descent shaft, so the scenery pass can keep their mouths clear
        private static int[] _shaftCenters = Array.Empty<int>();

        // debug counters
        private static int _tilesDug;
        private static int _tilesFilled;
        private static int _tilesConverted;
        private static int _tilesPlaced;

        #endregion

        #region Entry point

        public static void Generate(GenerationProgress? progress = null)
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

                progress?.Set(0.62);
                CarveDescentShafts();

                progress?.Set(0.66);
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
                _existingTopByX = Array.Empty<int>();
                // _seafloorTopByX stays alive: the late cleanup pass still needs it (it empties it when done)
                _isDryBeachByX = Array.Empty<bool>();
                _sandCapByX = Array.Empty<bool>();
                _dirtEndDepthByX = Array.Empty<int>();
                _convertBottomByX = Array.Empty<int>();
                _shaftCenters = Array.Empty<int>();
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
            _seafloorTopByX = Array.Empty<int>(); // never reuse a profile left over from a previous generation
            _shaftCenters = Array.Empty<int>();

            if (!TryFindNeighborSurface(out int neighborSurfaceY, out _neighborCapTileType, out _neighborCapWallType))
                return false;

            // the neighbour might be Corruption / Crimson, don't paste evil tiles or walls into our biome
            if (EvilToVanilla.TryGetValue(_neighborCapTileType, out ushort cleanCapTile))
                _neighborCapTileType = cleanCapTile;
            if (EvilWalls.Contains(_neighborCapWallType))
                _neighborCapWallType = WallID.None;

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

            // chasm shafts / deep holes make the scan return the bottom of the hole, which would carve a pit into the seam
            RejectSurfaceOutliers(_existingTopByX, 20, 12);

            // 2. the designed profile, still with the hammered drop into the ocean
            var design = new int[_width];
            for (int x = 0; x < _width; x++)
                design[x] = ComputeDesignTop(x, ComputeWaterTop(x));

            SmoothHeightProfile(design, MaxHeightStepPerColumn);

            // 3. blend the design into the existing terrain. weight is 0 at the seam (=> existing height) and 1 inside the biome
            int deepestFloor = BlendGroundProfile(design);

            // 4. one flat base for the whole biome, so the next layer can attach to a level boundary
            SetLayerBase(deepestFloor);

            Log($"surfaceY = {_surfaceY}, cap tile = {_neighborCapTileType}, cap wall = {_neighborCapWallType}, shoreWidth = {_shoreWidth}, width = {_width}, deepestFloor = {deepestFloor}, shoreBottomY = {GeneratedShoreBottomY}, layerBottomY = {_layerBottomY}");
            return true;
        }

        // Final ground height per column: the designed profile blended into what was already there. Returns the deepest floor.
        private static int BlendGroundProfile(int[] design)
        {
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

                deepestFloor = Math.Max(deepestFloor, groundTop);
            }

            return deepestFloor;
        }

        // The shore ends where the dirt layer under the deepest seafloor hands over to Volcanic Stone, and that depth is the same
        // for every column. Stays out of the underworld (it starts ~200 tiles above the bottom of the world).
        private static void SetLayerBase(int deepestFloor)
        {
            _layerBottomY = Math.Min(Main.maxTilesY - 200, deepestFloor + SeafloorSolidBuffer + BackfillDepth);
            GeneratedShoreBottomY = _layerBottomY;

            _convertFadeStartY = _layerBottomY + ConvertMargin;
            int convertBottom = Math.Min(Main.maxTilesY - 5, _convertFadeStartY + ConvertFadeWidth);
            _affectedBottomY = Math.Min(Main.maxTilesY - 5, convertBottom + 1);

            for (int x = 0; x < _width; x++)
            {
                _dirtEndDepthByX[x] = ComputeDirtEndDepth(x, _seafloorTopByX[x]);
                _convertBottomByX[x] = convertBottom;
            }
        }

        // Replaces columns whose height is far from the local median (chasm shafts, ravines, one-column spikes) with that median.
        // Anything within the tolerance keeps its own height so real terrain detail survives.
        private static void RejectSurfaceOutliers(int[] heights, int radius, int tolerance)
        {
            var source = (int[])heights.Clone();
            var window = new int[radius * 2 + 1];

            for (int i = 0; i < heights.Length; i++)
            {
                int n = 0;
                for (int j = i - radius; j <= i + radius; j++)
                    window[n++] = source[Math.Max(0, Math.Min(source.Length - 1, j))];

                Array.Sort(window);
                int median = window[window.Length / 2];

                heights[i] = Math.Abs(source[i] - median) > tolerance ? median : source[i];
            }
        }

        // samples a few columns past the seam and takes the median height so one dumb tree/floating island/dungeon
        // hill can't fuck it up false if no ground found at all
        private static bool TryFindNeighborSurface(out int surfaceY, out ushort capTile, out ushort capWall)
        {
            surfaceY = 0;
            capTile = TileID.Dirt;
            capWall = WallID.None;

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
                if (wall != WallID.None)
                    return wall;
            }

            return WallID.None;
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

        // Everything we need to know about one column while reshaping it
        private readonly struct ColumnPlan
        {
            public int GroundTop { get; init; }
            public int EndY { get; init; }
            public int FillBelowY { get; init; }
            public int DecorBottomY { get; init; }
            public bool HasWater { get; init; }
            public bool KeepDecor { get; init; }
            public float Weight { get; init; }
            public float PlaceWeight { get; init; }
        }

        // The whole "replace instead of rebuild" idea lives here.
        private static void ReshapeColumn(int x)
        {
            int existingTop = _existingTopByX[x];
            int groundTop = _seafloorTopByX[x];
            int endY = _convertBottomByX[x];
            float weight = ConversionWeight(x);

            var plan = new ColumnPlan
            {
                GroundTop = groundTop,
                EndY = endY,
                FillBelowY = Math.Max(existingTop, groundTop + MinSurfaceCrust),
                DecorBottomY = Math.Min(endY, Math.Max(groundTop, existingTop) + DecorClearDepth),
                HasWater = !_isDryBeachByX[x],
                // only keep vanilla trees/plants if the ground under them is still where it was
                KeepDecor = Math.Abs(groundTop - existingTop) <= 1 && KeepDecorInColumn(x, weight),
                Weight = weight,
                PlaceWeight = PlaceWeight(x),
            };

            int startY = Math.Max(5, Math.Min(_clearTopY, existingTop));

            DigAboveSurface(x, startY, ComputeWaterTop(x), plan);
            ReshapeBelowSurface(x, plan);

            if (PurgeEvilBiome && weight >= EvilForceMinWeight)
                PurgeEvilBelow(x, endY);
        }

        // ---- above the new surface: dig out whatever is there, then set water / no water ----
        private static void DigAboveSurface(int x, int startY, int waterTop, ColumnPlan plan)
        {
            for (int y = startY; y < plan.GroundTop; y++)
            {
                if (InBounds(x, y))
                    DigTile(x, y, plan.KeepDecor, plan.HasWater && y >= waterTop);
            }
        }

        private static void DigTile(int x, int y, bool keepDecor, bool underWater)
        {
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && (!keepDecor || !IsDecoration(tile.TileType)))
            {
                tile.ClearTile();
                _tilesDug++;
            }

            if (tile.WallType != WallID.None && !Main.wallDungeon[tile.WallType])
                tile.WallType = WallID.None;

            if (underWater)
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
        private static void ReshapeBelowSurface(int x, ColumnPlan plan)
        {
            for (int y = plan.GroundTop; y < plan.EndY; y++)
            {
                if (InBounds(x, y))
                    ReshapeTile(x, y, plan);
            }
        }

        private static void ReshapeTile(int x, int y, ColumnPlan plan)
        {
            Tile tile = Main.tile[x, y];
            int depth = y - plan.GroundTop;

            if (TryPlaceOrFill(x, y, depth, tile, plan))
                return;

            HandleExistingTile(x, y, depth, tile, plan);
            ConvertWallInPlace(x, y, depth, plan.Weight);
        }

        // true if the tile was (re)built from the designed terrain, false if the existing tile should be kept / converted
        private static bool TryPlaceOrFill(int x, int y, int depth, Tile tile, ColumnPlan plan)
        {
            bool open = !tile.HasTile || IsDecoration(tile.TileType);

            // ocean: seal vanilla caves under the seafloor, regardless of how far the convert -> place handoff has got
            if (plan.HasWater && depth < OceanSealDepth && open)
            {
                FillTile(x, y, depth);
                _tilesPlaced++;
                return true;
            }

            // interior: place the designed terrain over whatever was there (caves, ores, water and all).
            // placeWeight ramps 0 -> 1 so vanilla caves/ores fade out gradually instead of stopping at a line
            if (ShouldPlaceDesigned(x, y, plan))
            {
                FillTile(x, y, depth);
                _tilesPlaced++;
                return true;
            }

            // new ground is higher than the old one (or a cave opening right under the surface): this is the only place we add tiles
            if (y < plan.FillBelowY && open)
            {
                FillTile(x, y, depth);
                _tilesFilled++;
                return true;
            }

            return false;
        }

        private static bool ShouldPlaceDesigned(int x, int y, ColumnPlan plan)
        {
            if (plan.PlaceWeight <= 0f || !ShouldConvert(x, y, plan.Weight))
                return false;

            return plan.PlaceWeight >= 0.999f || DitherPastThreshold(x, y, plan.PlaceWeight, 1400);
        }

        private static void HandleExistingTile(int x, int y, int depth, Tile tile, ColumnPlan plan)
        {
            if (!tile.HasTile)
                return;

            if (IsDecoration(tile.TileType))
                HandleDecoration(y, tile, plan);
            else
                ConvertSolidInPlace(x, y, depth, plan.Weight);
        }

        private static void HandleDecoration(int y, Tile tile, ColumnPlan plan)
        {
            if (!plan.KeepDecor && y < plan.DecorBottomY)
            {
                tile.ClearTile();
                return;
            }

            // thorns / vines / orbs deep in a chasm sit in front of evil walls and are below the normal decor band
            if (PurgeEvilBiome && plan.Weight >= EvilForceMinWeight && EvilWalls.Contains(tile.WallType))
                tile.ClearTile();
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

                if (tile.WallType != WallID.None && EvilWalls.Contains(tile.WallType))
                    tile.WallType = WallID.None;

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

        // fades the conversion out below the biome base (absolute Y, so the bottom edge is level) instead of stopping at a hard line
        private static float ConversionDepthFade(int y)
        {
            if (y <= _convertFadeStartY)
                return 1f;

            float t = (y - _convertFadeStartY) / (float)Math.Max(1, ConvertFadeWidth);
            return 1f - SmoothStep(MathHelper.Clamp(t, 0f, 1f));
        }

        private static bool ShouldConvert(int x, int y, float weight)
        {
            float chance = weight * ConversionDepthFade(y);

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

            // the sand layer converts much more eagerly than the rest (sqrt pushes it up fast but stays 0 at the seam),
            // otherwise the beach is just dirt with a few sand specks dithered in
            bool inCap = _sandCapByX[x] && depth < ShoreCapDepthAt(x);
            float convertWeight = inCap ? (float)Math.Sqrt(weight) : weight;

            if (!forcedEvil && !ShouldConvert(x, y, convertWeight))
                return;

            // only the type changes, so slopes / half blocks / the cave shape that is already there stay as they are
            tile.TileType = PickMaterial(x, y, depth, stone);
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
            tile.TileColor = PaintID.None;

            _tilesConverted++;
        }

        private static void ConvertWallInPlace(int x, int y, int depth, float weight)
        {
            Tile tile = Main.tile[x, y];
            ushort wall = tile.WallType;

            if (wall == WallID.None || Main.wallDungeon[wall])
                return;

            bool forcedEvil = PurgeEvilBiome && weight >= EvilForceMinWeight && EvilWalls.Contains(wall);
            if (!forcedEvil && !ShouldConvert(x, y, weight))
                return;

            // while the biome walls are still WallID.None this just removes the old wall
            tile.WallType = WallForTile(PickMaterial(x, y, depth, false));
        }

        // decides what a tile becomes based on how deep it is: sand cap -> dirt -> dithers into volcanic stone
        private static ushort PickMaterial(int x, int y, int depth, bool stoneLike)
        {
            // sand goes first so stone near the surface doesn't poke through the beach as grey
            int capDepth = ShoreCapDepthAt(x);
            if (_sandCapByX[x] && depth < capDepth)
                return PickShoreCapMaterial(x, y, depth, capDepth);

            // natural rock stays rock, it just turns volcanic
            if (stoneLike)
                return VolcanicStoneTile;

            if (TryPickSeafloorPatch(x, y, depth, out ushort patchTile))
                return patchTile;

            return PickSoilMaterial(x, y, depth);
        }

        private static int ShoreCapDepthAt(int x)
        {
            float wobble = FBM(x * 0.035f, 55f, _worldSeed + 4300, 2) * 2f * ShoreSandCapNoise;
            return Math.Max(14, ShoreSandCapDepth + (int)wobble);
        }

        private static ushort PickShoreCapMaterial(int x, int y, int depth, int capDepth)
        {
            int transitionStart = capDepth - ShoreSandTransitionDepth;
            if (depth < transitionStart)
                return ShoreSandTile;

            float p = (depth - transitionStart) / (float)Math.Max(1, ShoreSandTransitionDepth);
            return DitherPastThreshold(x, y, p, 900) ? SeafloorTile : ShoreSandTile;
        }

        // open seafloor: volcanic outcrops and patches of ash sand so the bottom isn't one flat colour
        private static bool TryPickSeafloorPatch(int x, int y, int depth, out ushort tile)
        {
            tile = SeafloorTile;

            if (depth >= 5 || _isDryBeachByX[x] || _sandCapByX[x])
                return false;

            if (FBM(x * 0.06f, y * 0.06f, _worldSeed + 4200, 3) > 0.2f)
            {
                tile = VolcanicStoneTile;
                return true;
            }

            if (depth < 4 && FBM(x * 0.03f, 11f, _worldSeed + 4100, 3) > 0.1f)
            {
                tile = ShoreSandTile;
                return true;
            }

            return false;
        }

        // dirt that dithers into volcanic stone below the dirt layer
        private static ushort PickSoilMaterial(int x, int y, int depth)
        {
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
            float floorNoise = FBM(x * 0.025f, 300f, _worldSeed, 3) * 6f;

            // broad ridges and trenches, only out past the shelf
            float reliefMask = SmoothStep(MathHelper.Clamp((rampX - ShelfRampWidth * 0.5f) / 160f, 0f, 1f));
            float reliefNoise = FBM(x * ReliefMagnification, 700f, _worldSeed, 4) * ReliefAmplitude * reliefMask;

            // snap the broad shape into wide flat shelves joined by short ramps, then add the fine noise on top
            float baseDepth = shelfDepth + deepDepth + reliefNoise;
            float terraced = MathHelper.Lerp(baseDepth, TerraceDepth(baseDepth), TerraceStrength);

            int fullDepth = (int)terraced
                + (int)floorNoise + (int)(rippleNoise * 0.4f) + (int)(duneNoise * 0.35f);

            int seafloorDepth = Math.Max(0, (int)(fullDepth * rightFade));

            return waterTop + seafloorDepth;
        }

        // Snaps a smooth depth to flat shelves TerraceStep apart. Each step is flat for TerraceFlatFraction of the way and then ramps down.
        private static float TerraceDepth(float depth)
        {
            if (depth <= TerraceStart)
                return depth;

            float t = (depth - TerraceStart) / TerraceStep;
            float whole = (float)Math.Floor(t);
            float ramp = SmoothStep(MathHelper.Clamp((t - whole - TerraceFlatFraction) / (1f - TerraceFlatFraction), 0f, 1f));

            return TerraceStart + (whole + ramp) * TerraceStep;
        }

        // Depth below this column's floor where dirt starts dithering into volcanic stone. It is anchored to the shared base,
        // so the handoff is a level (slightly wavy) band across the whole biome instead of following the floor.
        private static int ComputeDirtEndDepth(int x, int groundTop)
        {
            float depthWobble = FBM(x * DirtDepthWobbleMagnification, 900f, _worldSeed, 3) * DirtDepthWobbleAmplitude;
            int transitionStartY = _layerBottomY - TransitionDepth + (int)depthWobble;
            return Math.Max(0, transitionStartY - groundTop);
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

        #region Descent shafts

        // Wide vertical shafts from the seafloor down to the base of the biome. They are the way down to the next layer:
        // each one ends in a landing chamber just above GeneratedShoreBottomY.
        private static void CarveDescentShafts()
        {
            _shaftCenters = new int[DescentShaftPercents.Length];

            for (int i = 0; i < DescentShaftPercents.Length; i++)
            {
                int cx = (int)(_width * DescentShaftPercents[i]);
                _shaftCenters[i] = cx;
                CarveShaft(cx);
            }
        }

        private static void CarveShaft(int cx)
        {
            int topY = FloorAt(cx) - 2;
            int bottomY = _layerBottomY - DescentShaftBottomMargin;
            int seed = WorldGen.genRand.Next();

            for (int y = topY; y < bottomY; y++)
            {
                int below = y - topY;
                float settle = Math.Min(1f, below / 40f);   // dead centre at the mouth, then it starts to meander
                float flare = Math.Max(0f, 1f - below / 25f) * DescentShaftMouthFlare;
                float wobble = FBM(y * 0.025f, 0f, seed, 2) * 10f * settle;
                float widthNoise = FBM(y * 0.06f, 40f, seed, 2) * 3f;

                int halfWidth = Math.Max(4, (int)(DescentShaftHalfWidth + flare + widthNoise));
                CarveRow((int)(cx + wobble), y, halfWidth);
            }

            CarveLandingChamber(cx, bottomY);
        }

        private static void CarveRow(int centerX, int y, int halfWidth)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
                SetLiquid(centerX + dx, y);
        }

        // a wide, flat-bottomed room at the base of a shaft: the spot where the next layer will connect
        private static void CarveLandingChamber(int cx, int floorY)
        {
            for (int dx = -DescentChamberHalfWidth; dx <= DescentChamberHalfWidth; dx++)
            {
                float u = dx / (float)DescentChamberHalfWidth;
                int ceiling = (int)(DescentChamberHeight * Math.Sqrt(Math.Max(0f, 1f - u * u)));

                for (int y = floorY - ceiling; y <= floorY; y++)
                    SetLiquid(cx + dx, y);
            }
        }

        #endregion

        #region Seafloor scenery

        private const int SceneryStep = 11;
        private const float SceneryBaseChance = 0.30f;
        private const float SceneryDensityBonus = 0.60f;
        private const int SceneryEdgeMargin = 25;
        private const int ShaftClearance = 26;
        // features claim this fraction of their full reach, so neighbours can overlap a little (looks more natural than even spacing)
        private const float SceneryClaimFraction = 0.75f;

        private enum Scenery
        {
            Spikes,
            Rubble,
            Spire,
            Chimney,
            Arch,
            FloatingRock,
        }

        // Cumulative chance per water-depth band, in SceneryOrder order. Entries that repeat the previous value are never picked.
        private static readonly Scenery[] SceneryOrder =
        {
            Scenery.Spikes, Scenery.Rubble, Scenery.Spire, Scenery.Chimney, Scenery.Arch, Scenery.FloatingRock,
        };

        // shallows: small stuff only
        private static readonly float[] ShallowOdds = { 0.45f, 0.80f, 1.00f, 1.00f, 1.00f, 1.00f };

        // mid water
        private static readonly float[] MidOdds = { 0.20f, 0.35f, 0.58f, 0.78f, 0.90f, 1.00f };

        // deep water: the big set pieces
        private static readonly float[] DeepOdds = { 0.10f, 0.20f, 0.40f, 0.58f, 0.74f, 1.00f };

        // Fills the open ocean with scenery. A low-frequency density noise makes busy reefs and bare plains instead of an
        // even sprinkle, every feature claims a span of columns so nothing is stacked on top of something else, and the
        // feature type depends on how deep the water is (small stuff in the shallows, arches / floating rock out in the deep).
        private static void ScatterWaterClutter()
        {
            int waterCheckY = _surfaceY + WaterlineOffset + 5;
            int endX = _width - _shoreWidth - 30;

            var claimed = new List<(int min, int max)>();

            // keep the shaft mouths clear
            foreach (int shaftX in _shaftCenters)
                claimed.Add((shaftX - ShaftClearance, shaftX + ShaftClearance));

            for (int x = SceneryEdgeMargin; x < endX; x += SceneryStep)
            {
                int cx = x + WorldGen.genRand.Next(-4, 5);
                if (!ShouldRollSceneryAt(cx, waterCheckY))
                    continue;

                int floorY = FloorAt(cx);
                int waterDepth = floorY - waterCheckY;
                if (waterDepth < 40)
                    continue;

                Scenery type = RollScenery(waterDepth);
                if (!TryClaimSpan(claimed, cx, type))
                    continue;

                PlaceScenery(type, cx, floorY, waterCheckY, waterDepth);
            }
        }

        // open water here + the density noise says "something goes here"
        private static bool ShouldRollSceneryAt(int cx, int waterCheckY)
        {
            if (cx < 8 || cx >= _width - 8)
                return false;

            if (!InBounds(cx, waterCheckY) || Main.tile[cx, waterCheckY].LiquidAmount == 0)
                return false;

            float density = FBM(cx * 0.011f, 77f, _worldSeed + 3100, 3) * 0.5f + 0.5f;
            float chance = MathHelper.Clamp(SceneryBaseChance + density * SceneryDensityBonus, 0f, 0.95f);

            return WorldGen.genRand.NextFloat() <= chance;
        }

        // reserves the columns a feature needs, false if something already sits there
        private static bool TryClaimSpan(List<(int min, int max)> claimed, int cx, Scenery type)
        {
            int claim = (int)(SceneryReach(type) * SceneryClaimFraction);
            int claimMin = cx - claim;
            int claimMax = cx + claim;

            if (claimed.Exists(c => claimMax >= c.min && claimMin <= c.max))
                return false;

            claimed.Add((claimMin, claimMax));
            return true;
        }

        private static void PlaceScenery(Scenery type, int cx, int floorY, int waterCheckY, int waterDepth)
        {
            switch (type)
            {
                case Scenery.Spikes:
                    PlaceSpikeCluster(cx);
                    break;
                case Scenery.Rubble:
                    PlaceDebrisCluster(cx, waterCheckY);
                    break;
                case Scenery.Spire:
                    PlaceRockSpire(cx, floorY, waterDepth);
                    break;
                case Scenery.Chimney:
                    PlaceChimneyCluster(cx, waterDepth);
                    break;
                case Scenery.Arch:
                    PlaceArch(cx, waterCheckY);
                    break;
                default:
                    PlaceFloatingRock(cx, waterCheckY);
                    break;
            }
        }

        private static Scenery RollScenery(int waterDepth)
        {
            float[] odds = DeepOdds;
            if (waterDepth < 90)
                odds = ShallowOdds;
            else if (waterDepth < 150)
                odds = MidOdds;

            float roll = WorldGen.genRand.NextFloat();

            for (int i = 0; i < odds.Length; i++)
            {
                if (roll < odds[i])
                    return SceneryOrder[i];
            }

            return Scenery.FloatingRock;
        }

        // widest the feature can get (half width, in columns)
        private static int SceneryReach(Scenery type)
        {
            switch (type)
            {
                case Scenery.Spikes: return 12;
                case Scenery.Rubble: return 17;
                case Scenery.Spire: return 18;
                case Scenery.Chimney: return 16;
                case Scenery.Arch: return 32;
                default: return 24;
            }
        }

        // fills straight down from (x, y) until it meets existing ground, so scenery on a slope doesn't hover
        private static void AnchorDown(int x, int y, float seafloorChance)
        {
            for (int yy = y + 1; yy < y + 40 && InBounds(x, yy); yy++)
            {
                Tile below = Main.tile[x, yy];
                if (below.HasTile && Main.tileSolid[below.TileType])
                    break;

                PlaceSeafloorOrVolcanic(x, yy, seafloorChance);
            }
        }

        // filled circle of seafloor / volcanic tiles
        private static void FillDisc(int centerX, int centerY, int radius, float seafloorChance)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                        PlaceSeafloorOrVolcanic(centerX + dx, centerY + dy, seafloorChance);
                }
            }
        }

        // ---- spikes ----

        // a handful of thin rock spikes poking out of the floor
        private static void PlaceSpikeCluster(int centerX)
        {
            int count = WorldGen.genRand.Next(3, 8);

            for (int i = 0; i < count; i++)
            {
                int sx = centerX + WorldGen.genRand.Next(-9, 10);
                if (sx >= 3 && sx < _width - 3)
                    PlaceSpike(sx);
            }
        }

        private static void PlaceSpike(int sx)
        {
            int floorY = FloorAt(sx);
            int height = WorldGen.genRand.Next(3, 11);

            for (int h = 0; h < height; h++)
            {
                int y = floorY - h;
                PlaceSeafloorOrVolcanic(sx, y, 0.5f);
                PlaceSpikeFoot(sx, y, h, height);
            }
        }

        // only the bigger spikes get a thicker foot, so they read as cones instead of needles
        private static void PlaceSpikeFoot(int sx, int y, int h, int height)
        {
            if (height < 7 || h >= height / 2)
                return;

            PlaceFootTile(sx - 1, y, h == 0);

            if (h < height / 3)
                PlaceFootTile(sx + 1, y, h == 0);
        }

        private static void PlaceFootTile(int x, int y, bool anchorDown)
        {
            PlaceSeafloorOrVolcanic(x, y, 0.5f);

            if (anchorDown)
                AnchorDown(x, y, 0.5f);
        }

        // ---- pillars ----

        // Places a tall tapered rock pillar rising from the seafloor. Leans a little, flares at the foot, bumpy along the shaft.
        private static void PlaceRockSpire(int centerX, int floorY, int waterDepth)
        {
            int maxHeight = waterDepth >= 150 ? 85 : 55;
            int height = Math.Min(waterDepth - 22, WorldGen.genRand.Next(22, maxHeight));
            if (height < 12)
                return;

            int baseHalfWidth = WorldGen.genRand.Next(3, 7);
            int lean = WorldGen.genRand.Next(-7, 8);
            int seed = WorldGen.genRand.Next();

            for (int i = 0; i < height; i++)
            {
                float t = i / (float)height;

                float taper = (float)Math.Pow(1f - t, 0.8f);
                float flare = i < 8 ? (8 - i) * 0.35f : 0f;
                float bump = FBM(i * 0.25f, 0f, seed, 2) * 1.6f;

                int halfWidth = Math.Max(0, (int)Math.Round(baseHalfWidth * taper + flare + bump));
                int shift = (int)Math.Round(lean * t * t);

                PlaceSpireRow(centerX + shift, floorY - i, halfWidth, i < 3);
            }
        }

        private static void PlaceSpireRow(int rowCenterX, int y, int halfWidth, bool anchorDown)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                int x = rowCenterX + dx;
                float edgeNoise = FBM(x * 0.1f, y * 0.1f, _worldSeed + 800, 2);

                if (halfWidth > 1 && Math.Abs(dx) == halfWidth && edgeNoise < -0.2f)
                    continue;

                PlaceSeafloorOrVolcanic(x, y, 0.6f);

                if (anchorDown)
                    AnchorDown(x, y, 0.6f);
            }
        }

        // ---- chimneys ----

        // 1-3 tall thin volcanic chimneys on a low mound, each with a hollow mouth at the top
        private static void PlaceChimneyCluster(int centerX, int waterDepth)
        {
            int count = WorldGen.genRand.Next(1, 4);

            for (int c = 0; c < count; c++)
            {
                int cx = centerX + (int)((c - (count - 1) / 2f) * 7f) + WorldGen.genRand.Next(-1, 2);
                if (cx >= 8 && cx < _width - 8)
                    PlaceChimney(cx, waterDepth);
            }
        }

        private static void PlaceChimney(int cx, int waterDepth)
        {
            int floorY = FloorAt(cx);
            int height = Math.Min(waterDepth - 25, WorldGen.genRand.Next(10, 30));
            if (height < 8)
                return;

            int baseHalf = WorldGen.genRand.Next(2, 4);

            PlaceChimneyMound(cx, floorY, baseHalf + 4);
            PlaceChimneyShaft(cx, floorY, height, baseHalf);
        }

        // low mound under the chimney
        private static void PlaceChimneyMound(int cx, int floorY, int moundRadius)
        {
            for (int dx = -moundRadius; dx <= moundRadius; dx++)
            {
                int moundHeight = (int)Math.Round(4f * (1f - Math.Abs(dx) / (float)(moundRadius + 1)));

                for (int h = 0; h < moundHeight; h++)
                    PlaceSeafloorOrVolcanic(cx + dx, floorY - h, 0.85f);

                AnchorDown(cx + dx, floorY, 0.85f);
            }
        }

        private static void PlaceChimneyShaft(int cx, int floorY, int height, int baseHalf)
        {
            for (int i = 0; i < height; i++)
            {
                float t = i / (float)height;
                float flare = i < 4 ? (4 - i) * 0.4f : 0f;
                int halfWidth = Math.Max(0, (int)Math.Round(baseHalf * (1f - 0.55f * t) + flare));
                bool mouthRow = halfWidth >= 1 && i >= height - 5;

                PlaceChimneyRow(cx, floorY - i, halfWidth, mouthRow);
            }
        }

        private static void PlaceChimneyRow(int cx, int y, int halfWidth, bool mouthRow)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                if (mouthRow && dx == 0)
                    SetLiquid(cx + dx, y);
                else
                    PlaceSeafloorOrVolcanic(cx + dx, y, 0.2f);
            }
        }

        // ---- arches ----

        // a natural rock arch: two legs standing on the floor joined by a thick curved span
        private static void PlaceArch(int centerX, int waterCheckY)
        {
            int span = WorldGen.genRand.Next(12, 24);   // half width of the opening
            int legW = WorldGen.genRand.Next(4, 8);
            int outer = span + legW;

            if (centerX - outer < 4 || centerX + outer >= _width - 4)
                return;

            int baseY = Math.Min(FloorAt(centerX - outer), FloorAt(centerX + outer));
            int maxHeight = baseY - waterCheckY - 25;
            int height = Math.Min(WorldGen.genRand.Next(18, 36), maxHeight);
            int thickness = WorldGen.genRand.Next(4, 8);

            if (height < thickness + 10)
                return;

            var shape = new ArchShape
            {
                CenterX = centerX,
                Outer = outer,
                Span = span,
                BaseY = baseY,
                Height = height,
                Thickness = thickness,
                Seed = WorldGen.genRand.Next(),
            };

            for (int dx = -outer; dx <= outer; dx++)
                PlaceArchColumn(shape, dx);
        }

        private readonly struct ArchShape
        {
            public int CenterX { get; init; }
            public int Outer { get; init; }
            public int Span { get; init; }
            public int BaseY { get; init; }
            public int Height { get; init; }
            public int Thickness { get; init; }
            public int Seed { get; init; }
        }

        private static void PlaceArchColumn(ArchShape shape, int dx)
        {
            int x = shape.CenterX + dx;
            float u = dx / (float)shape.Outer;

            float outerTop = shape.BaseY - shape.Height * (float)Math.Sqrt(Math.Max(0f, 1f - u * u));
            outerTop += FBM(x * 0.12f, 3f, shape.Seed, 2) * 3f;
            int topY = (int)Math.Round(outerTop);

            int bottomY = FloorAt(x) + 1; // legs: solid all the way down to the floor

            if (Math.Abs(dx) < shape.Span)
            {
                // under the span: solid only down to the inner curve, so there is an opening
                float v = dx / (float)shape.Span;
                float innerTop = shape.BaseY - (shape.Height - shape.Thickness) * (float)Math.Sqrt(Math.Max(0f, 1f - v * v));
                innerTop += FBM(x * 0.12f, 9f, shape.Seed, 2) * 2f;
                bottomY = (int)Math.Round(innerTop);
            }

            for (int y = topY; y < bottomY; y++)
                PlaceSeafloorOrVolcanic(x, y, 0.45f);
        }

        // ---- floating rock ----

        // a chunk of rock hanging in open water, with a few stalactite tails and loose satellite rocks
        private static void PlaceFloatingRock(int centerX, int waterCheckY)
        {
            int rx = WorldGen.genRand.Next(6, 14);
            int ry = WorldGen.genRand.Next(3, 7);
            int tail = WorldGen.genRand.Next(6, 16);

            if (centerX - rx - 10 < 4 || centerX + rx + 10 >= _width - 4)
                return;

            int localFloor = HighestFloorBetween(centerX - rx - 8, centerX + rx + 8);
            int minY = waterCheckY + 30 + ry;
            int maxY = localFloor - 40 - ry - tail;
            if (maxY <= minY)
                return;

            int cy = WorldGen.genRand.Next(minY, maxY + 1);

            FillRockBody(centerX, cy, rx, ry);
            PlaceRockTails(centerX, cy, rx, ry, tail);
            PlaceRockSatellites(centerX, cy, rx);
        }

        // smallest floor Y (= highest ground) over a span of columns
        private static int HighestFloorBetween(int minX, int maxX)
        {
            int highest = int.MaxValue;

            for (int x = minX; x <= maxX; x += 4)
                highest = Math.Min(highest, FloorAt(x));

            return highest;
        }

        private static void FillRockBody(int centerX, int cy, int rx, int ry)
        {
            int seed = WorldGen.genRand.Next();

            for (int dx = -rx - 2; dx <= rx + 2; dx++)
            {
                for (int dy = -ry - 2; dy <= ry + 2; dy++)
                {
                    float normalizedDistance = (float)Math.Sqrt((dx * dx) / (float)(rx * rx) + (dy * dy) / (float)(ry * ry));
                    float wobble = 1f + FBM((centerX + dx) * 0.18f, (cy + dy) * 0.18f, seed, 2) * 0.7f;

                    if (normalizedDistance * wobble <= 1f)
                        PlaceSeafloorOrVolcanic(centerX + dx, cy + dy, 0.3f);
                }
            }
        }

        // stalactite tails hanging underneath (they start inside the body so there is no gap)
        private static void PlaceRockTails(int centerX, int cy, int rx, int ry, int tail)
        {
            int tails = WorldGen.genRand.Next(1, 4);

            for (int t = 0; t < tails; t++)
            {
                int tx = centerX + WorldGen.genRand.Next(-rx / 2, rx / 2 + 1);
                int length = WorldGen.genRand.Next(tail / 2, tail + 1);

                PlaceStalactite(tx, cy + (int)(ry * 0.5f), length);
            }
        }

        private static void PlaceStalactite(int tx, int startY, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int halfWidth = i < length / 3 ? 1 : 0;

                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                    PlaceSeafloorOrVolcanic(tx + dx, startY + i, 0.3f);
            }
        }

        // small loose rocks drifting nearby
        private static void PlaceRockSatellites(int centerX, int cy, int rx)
        {
            int satellites = WorldGen.genRand.Next(0, 4);

            for (int s = 0; s < satellites; s++)
            {
                int side = WorldGen.genRand.Next(2) == 0 ? -1 : 1;
                int sx = centerX + side * (rx + WorldGen.genRand.Next(3, 8));
                int sy = cy + WorldGen.genRand.Next(-7, 8);

                FillDisc(sx, sy, WorldGen.genRand.Next(1, 3), 0.3f);
            }
        }

        // ---- rubble ----

        // Places a few small rubble mounds on the seafloor
        private static void PlaceDebrisCluster(int centerX, int waterCheckY)
        {
            int clusters = WorldGen.genRand.Next(2, 5);

            for (int c = 0; c < clusters; c++)
                PlaceRubbleMound(centerX + WorldGen.genRand.Next(-12, 13), waterCheckY);
        }

        private static void PlaceRubbleMound(int cx, int waterCheckY)
        {
            if (cx < 0 || cx >= _width)
                return;

            int floorY = FloorAt(cx);
            int waterDepth = floorY - waterCheckY;
            int radius = WorldGen.genRand.Next(2, 5);
            int riseHeight = Math.Min(waterDepth - 15, WorldGen.genRand.Next(3, 10));

            if (riseHeight < 2)
                return;

            FillDisc(cx, floorY - WorldGen.genRand.Next(0, riseHeight), radius, 0.8f);
        }

        private static void PlaceSeafloorOrVolcanic(int x, int y, float seafloorChance)
        {
            bool seafloor = WorldGen.genRand.NextFloat() < seafloorChance;
            SetSolid(x, y, seafloor ? SeafloorTile : VolcanicStoneTile, seafloor ? SeafloorWall : VolcanicStoneWall);
        }

        #endregion

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

                angle += FBM(step * 0.05f, 0f, wormSeed, 2) * 0.6f;

                x += (float)Math.Cos(angle);
                y += (float)Math.Sin(angle) * 0.6f;

                int ix = (int)x;
                int iy = (int)y;

                if (ix < 5 || ix >= _width - 5 || !InBounds(ix, iy))
                    break;

                KeepWormUnderFloor(ix, ref iy, ref y, ref angle);

                float endTaper = Math.Min(1f, Math.Min(t, 1f - t) * 3f);
                float radiusNoise = FBM(step * 0.08f, 300f, wormSeed, 2) * 0.5f + 0.5f;
                float radius = MathHelper.Lerp(CaveWormMinRadius, CaveWormMaxRadius, radiusNoise) * endTaper;

                CarveLiquidDisc(ix, iy, Math.Max(1, (int)radius));
            }
        }

        // keep worms under the seafloor instead of letting them tunnel out through the beach
        private static void KeepWormUnderFloor(int ix, ref int iy, ref float y, ref float angle)
        {
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
        }

        private static void CarveLiquidDisc(int centerX, int centerY, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                        SetLiquid(centerX + dx, centerY + dy);
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
            if (_seafloorTopByX.Length == 0)
                return;

            try
            {
                RemoveStrayChests();
            }
            finally
            {
                _seafloorTopByX = Array.Empty<int>();
            }
        }

        private static void RemoveStrayChests()
        {
            int removed = 0;

            for (int i = 0; i < Main.chest.Length; i++)
            {
                if (!TryRemoveChest(Main.chest[i]))
                    continue;

                Main.chest[i] = null;
                removed++;
            }

            Log($"removed {removed} stray chests");
        }

        // clears the chest's tiles if it sits in water or on the shore, true if it was removed
        private static bool TryRemoveChest(Chest? chest)
        {
            if (chest == null || !IsInsideChestClearZone(chest))
                return false;

            GetContainerFootprint(chest.x, chest.y, out int width, out int height);

            bool onShore = chest.y <= FloorAt(chest.x) + ShoreChestClearDepth;
            if (!onShore && !FootprintHasLiquid(chest.x, chest.y, width, height))
                return false;

            ClearContainerTiles(chest.x, chest.y, width, height);
            return true;
        }

        private static bool IsInsideChestClearZone(Chest chest)
        {
            if (chest.x < 0 || chest.x >= _width || chest.y < _clearTopY || chest.y >= _affectedBottomY)
                return false;

            return InBounds(chest.x, chest.y);
        }

        // size of the object whose origin is at (x, y), falls back to a 2x2 chest
        private static void GetContainerFootprint(int x, int y, out int width, out int height)
        {
            width = 2;
            height = 2;

            Tile origin = Main.tile[x, y];
            if (!origin.HasTile)
                return;

            TileObjectData? data = TileObjectData.GetTileData(origin);
            if (data == null)
                return;

            width = data.Width;
            height = data.Height;
        }

        private static bool FootprintHasLiquid(int x, int y, int width, int height)
        {
            for (int dx = 0; dx < width; dx++)
            {
                for (int dy = 0; dy < height; dy++)
                {
                    if (InBounds(x + dx, y + dy) && Main.tile[x + dx, y + dy].LiquidAmount > 0)
                        return true;
                }
            }

            return false;
        }

        private static void ClearContainerTiles(int x, int y, int width, int height)
        {
            for (int dx = 0; dx < width; dx++)
            {
                for (int dy = 0; dy < height; dy++)
                    ClearIfContainer(x + dx, y + dy);
            }
        }

        private static void ClearIfContainer(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            Tile cell = Main.tile[x, y];
            if (cell.HasTile && Main.tileContainer[cell.TileType])
                cell.ClearTile();
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
            tile.TileColor = PaintID.None;
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
            tile.WallType = WallID.None;
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
            AshenShore.SetGeneratedShoreBottomY(tag.GetInt("ShoreBottomY"));
        }

        public override void ClearWorld()
        {
            AshenShore.SetGeneratedShoreBottomY(0);
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(AshenShore.GeneratedShoreBottomY);
        }

        public override void NetReceive(BinaryReader reader)
        {
            AshenShore.SetGeneratedShoreBottomY(reader.ReadInt32());
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