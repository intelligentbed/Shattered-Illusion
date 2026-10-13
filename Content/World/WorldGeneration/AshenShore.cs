using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using ShatteredIllusion.Content.Tiles.BoilingOcean;

namespace ShatteredIllusion.Content.World.WorldGeneration
{
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

        public const int BeachMaxHeightMin = 40;

 
        public const float BeachDescentSmoothness = 0.5f;

        public const float BeachNoiseMagnification = 0.03f;
        public const float BeachNoiseAmplitude = 6f;

        public const int ShelfRampWidth = 120;

        // the low taper fade with the other biomes but its lwk kinda buns might scrap or rework it
        public const int RightEdgeTransitionWidth = 150;

        // How far below the seafloor the dirt layer runs before it transitions into Volcanic Stone
        public const int BackfillDepth = 90;

        public const float SurfaceWaveMagnification = 0.02f;
        public const float SurfaceWaveAmplitude = 7f;
        public const float DuneMagnification = 0.0045f;
        public const float DuneAmplitude = 16f;

        public const int ShoreSandCapDepth = 12;
        public const int ShoreSandTransitionDepth = 6;
        public const int ShoreSandScanAboveSurface = 130;
        public const int ShoreSandScanBelowSurface = 60;

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

        public const int MaxHeightStepPerColumn = 1;

        #endregion

        #region Per-generation state (reset every time Setup() runs)

        private static int _worldSeed;

        // Number of columns actually generated 
        private static int _width;

        // Surface height of the neighboring biome at the seam
        private static int _surfaceY;

        // Neighboring biome material at the seam
        private static ushort _neighborCapTileType;
        private static ushort _neighborCapWallType;

        // Randomized once per world
        private static int _shoreWidth;

        // Vertical extent of the cleared region
        private static int _clearTopY;
        private static int _clearedBottomY;

        private static int[] _clearBottomByX;

        // Seafloor (or beach) height for each column.
        private static int[] _seafloorTopByX;

        private static bool[] _isDryBeachByX;

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
                ReplaceVanillaOcean();

                progress?.Set(0.25);
                GenerateShoreAndSeafloor();

                progress?.Set(0.50);
                CarveSeafloorPockets();

                progress?.Set(0.58);
                CarveCaveNetwork();

                progress?.Set(0.65);
                PaintShoreline();
                ScatterWaterClutter();

                progress?.Set(0.75);
                SmoothPass();

                progress?.Set(0.80);
                SuppressLavaFringe();
                FrameAffectedArea();

                progress?.Set(1.00);
            }
            finally
            {
                _seafloorTopByX = null;
                _clearBottomByX = null;
                _isDryBeachByX = null;
            }
        }

        private static bool Setup()
        {
            Init();

            _worldSeed = WorldGen.genRand.Next();
            _shoreWidth = WorldGen.genRand.Next(ShoreWidthMin, ShoreWidthMax + 1);
            _width = Math.Min(BiomeWidth, Main.maxTilesX - 5);

            if (!TryFindNeighborSurface(out int neighborSurfaceY, out _neighborCapTileType, out _neighborCapWallType))
                return false;

            _surfaceY = Math.Max(50, neighborSurfaceY + ShoreSurfaceOffset);
            _clearTopY = Math.Max(5, _surfaceY - 80);
            _clearedBottomY = Math.Min(Main.maxTilesY - 5, _surfaceY + SeafloorDepth + SeafloorSolidBuffer + 100);

            // always clear at least the near-surface band; deeper clearing fades out toward the seam
            int guaranteedBottomY = Math.Min(_clearedBottomY, _surfaceY + WaterlineOffset + ShallowSeafloorDepth + 40);

            const int localFillMargin = 60;
            int localFillDepth = SeafloorSolidBuffer + BackfillDepth + TransitionDepth + localFillMargin;

            _clearBottomByX = new int[_width];
            _seafloorTopByX = new int[_width];
            _isDryBeachByX = new bool[_width];

            for (int x = 0; x < _width; x++)
            {
                int waterTop = ComputeWaterTop(x);
                int groundTop = ComputeGroundTop(x, waterTop, out bool isDryBeach);
                _seafloorTopByX[x] = groundTop;
                _isDryBeachByX[x] = isDryBeach;
            }

            // the fall of into the ocean its hammered rn cause i like it like that but might change based off others
            SmoothHeightProfile(_seafloorTopByX, MaxHeightStepPerColumn);

            for (int x = 0; x < _width; x++)
            {
                int groundTop = _seafloorTopByX[x];

                int localBottomY = groundTop + localFillDepth;

                float fade = Math.Max(0.05f, RightEdgeFade(x));
                int fadedBottomY = _clearTopY + (int)((_clearedBottomY - _clearTopY) * fade);

                int cappedBottomY = Math.Min(fadedBottomY, localBottomY);
                _clearBottomByX[x] = Math.Min(_clearedBottomY, Math.Max(guaranteedBottomY, cappedBottomY));
            }

            Log($"surfaceY = {_surfaceY}, cap tile = {_neighborCapTileType}, cap wall = {_neighborCapWallType}, shoreWidth = {_shoreWidth}, width = {_width}");
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

        #endregion

        #region Steps

        // rails/platforms/rope left behind by vanilla structures
        private static void RemoveLeftoverDebris()
        {
            int maxY = Math.Min(Main.maxTilesY - 5, _clearedBottomY + 200);

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

        //FUCK THE OCEAN WE GOT A worse one :(
        private static void ReplaceVanillaOcean()
        {
            PurgeChestsAndSigns(0, _width, _clearTopY, _clearedBottomY);

            for (int x = 0; x < _width; x++)
            {
                int bottomY = _clearBottomByX[x];

                for (int y = _clearTopY; y < bottomY; y++)
                    ResetTile(x, y);
            }
        }

        private static void GenerateShoreAndSeafloor()
        {
            for (int x = 0; x < _width; x++)
            {
                int groundTop = _seafloorTopByX[x];

                if (!_isDryBeachByX[x])
                {
                    int waterTop = ComputeWaterTop(x);
                    for (int y = waterTop; y < groundTop; y++)
                        SetLiquid(x, y);
                }

                FillColumnBelow(x, groundTop);
            }
        }

        private static int ComputeWaterTop(int x)
        {
            float waterNoise = FBM(x * WaterlineWaveMagnification, 100f, _worldSeed, 4) * WaterlineWaveAmplitude;
            return _surfaceY + WaterlineOffset + (int)waterNoise;
        }

        private static int ComputeGroundTop(int x, int waterTop, out bool isDryBeach)
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

            isDryBeach = mirroredX < _shoreWidth && beachTop < waterTop;
            if (isDryBeach)
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

        // clamps the diff between adjacent columns. runs both directions so a run of jumps gets spread out
        // instead of just clipping the first one and leaving the rest
        private static void SmoothHeightProfile(int[] heights, int maxStep)
        {
            for (int x = 1; x < heights.Length; x++)
                heights[x] = (int)MathHelper.Clamp(heights[x], heights[x - 1] - maxStep, heights[x - 1] + maxStep);

            for (int x = heights.Length - 2; x >= 0; x--)
                heights[x] = (int)MathHelper.Clamp(heights[x], heights[x + 1] - maxStep, heights[x + 1] + maxStep);
        }

        private static void FillColumnBelow(int x, int groundTop)
        {
            int bottomY = _clearBottomByX[x];

            float depthWobble = FBM(x * DirtDepthWobbleMagnification, 900f, _worldSeed, 3) * DirtDepthWobbleAmplitude;

            int dirtEnd = Math.Min(
                bottomY - TransitionDepth,
                groundTop + SeafloorSolidBuffer + BackfillDepth - TransitionDepth + (int)depthWobble
            );
            dirtEnd = Math.Max(groundTop, dirtEnd);

            for (int y = groundTop; y < bottomY; y++)
            {
                ushort tileHere = SeafloorTile;
                ushort wallHere = SeafloorWall;

                if (y >= dirtEnd)
                {
                    float progress = Math.Min(1f, (y - dirtEnd) / (float)Math.Max(1, TransitionDepth));

                    if (DitherPastThreshold(x, y, progress, 700))
                    {
                        tileHere = VolcanicStoneTile;
                        wallHere = VolcanicStoneWall;
                    }
                }

                ApplySeamBlend(x, y, ref tileHere, ref wallHere);

                SetSolid(x, y, tileHere, wallHere);
            }
        }

        private static bool DitherPastThreshold(int x, int y, float progress, int seedOffset)
        {
            float dither = FBM(x * TransitionNoiseMagnification, y * TransitionNoiseMagnification, _worldSeed + seedOffset, 2) * 0.5f + 0.5f;
            return dither < progress;
        }

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

        // Finds the first exposed solid tile in a column, near the neighboring surface height
        private static int FindSurfaceY(int x)
        {
            int scanTop = Math.Max(5, _surfaceY - ShoreSandScanAboveSurface);
            int scanBottom = Math.Min(Main.maxTilesY - 5, _surfaceY + ShoreSandScanBelowSurface);

            for (int y = scanTop; y < scanBottom; y++)
            {
                if (!Main.tile[x, y].HasTile)
                    continue;

                // only exposed tiles count
                if (y > 5 && Main.tile[x, y - 1].HasTile)
                    continue;

                return y;
            }

            return -1;
        }

        // Caps the actual coastline with sand also blending into the neighboring biome's material near the edge
        private static void PaintShoreline()
        {
            for (int x = 0; x < _width; x++)
            {
                int groundY = FindSurfaceY(x);
                if (groundY < 0)
                    continue;

                int capBottom = groundY + ShoreSandCapDepth;
                int transitionStart = capBottom - ShoreSandTransitionDepth;

                for (int y = groundY; y < capBottom; y++)
                {
                    if (!InBounds(x, y))
                        continue;

                    if (!Main.tile[x, y].HasTile)
                        break; 

                    ushort tileHere = ShoreSandTile;
                    ushort wallHere = ShoreSandWall;

                    if (y >= transitionStart)
                    {
                        float transitionProgress = (y - transitionStart) / (float)Math.Max(1, capBottom - transitionStart);

                        if (DitherPastThreshold(x, y, transitionProgress, 900))
                        {
                            tileHere = SeafloorTile;
                            wallHere = SeafloorWall;
                        }
                    }

                    if (x >= _width - SeamBlendWidth)
                        ApplySeamBlend(x, y, ref tileHere, ref wallHere);

                    SetSolid(x, y, tileHere, wallHere);
                }
            }
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
            int maxY = Math.Min(_clearedBottomY, Main.maxTilesY - 5);

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
                int bandTop = Math.Max(_clearTopY, _clearBottomByX[x] - LavaFringeBandTop);
                int bandBottom = Math.Min(Main.maxTilesY - 5, _clearBottomByX[x] + LavaFringeBandBottom);

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

        // tiles are written directly with no per-tile framing, so re-frame the whole area once at the end
        private static void FrameAffectedArea()
        {
            int topY = Math.Max(1, _clearTopY - 2);
            int bottomY = Math.Min(Main.maxTilesY - 2, _clearedBottomY + 200);

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

        private static void ResetTile(int x, int y)
        {
            if (!InBounds(x, y))
                return;

            Main.tile[x, y].ClearEverything();
        }


        private static void PurgeChestsAndSigns(int minX, int maxX, int minY, int maxY)
        {
            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest != null && chest.x >= minX && chest.x < maxX && chest.y >= minY && chest.y < maxY)
                    Main.chest[i] = null;
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                Sign sign = Main.sign[i];
                if (sign != null && sign.x >= minX && sign.x < maxX && sign.y >= minY && sign.y < maxY)
                    Main.sign[i] = null;
            }
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

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int settleLiquidsIndex = tasks.FindIndex(genPass => genPass.Name.Equals("Settle Liquids Again"));
            int insertAt = settleLiquidsIndex == -1 ? tasks.Count : settleLiquidsIndex;

            tasks.Insert(insertAt, new AshenShoreGenPass());
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