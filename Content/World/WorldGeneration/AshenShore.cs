using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding; 
using ShatteredIllusion.Content.Tiles.BoilingOcean;

namespace ShatteredIllusion.Content.World
{
    /// <summary>
    /// Sand shoreline sloping into water with two seafloor pockets, bounded by a cliff
    /// on the right. Hugs the left world edge and fully overwrites the vanilla ocean there.
    /// </summary>
    public static class AshenShore
    {
        #region Tile / Wall types - swap these for your custom tiles

        // TODO: replace these with your actual ModContent.TileType<T>() calls.
        //
        // Populated by Init(), not field initializers - a static field initializer can
        // run before content autoloading finishes, which throws, faults the static
        // constructor, and permanently bricks this class for the rest of the process
        // (silent "nothing generates" with no crash). Call Init() from PostSetupContent()
        // so failures show up at load time instead.

        public static ushort ShoreSandTile;
        public static ushort ShoreSandWall = WallID.Sandstone;

        public static ushort SeafloorTile;
        public static ushort SeafloorWall = WallID.HardenedSand;

        public static ushort CliffRockTile;
        public static ushort CliffRockWall = WallID.Cave3Unsafe;

        // The next layer down. We dither the bottom of the backfill into this so
        // Ashen Shore doesn't end in a hard seam wherever the next biome picks up.
        public static ushort VolcanicStoneTile;
        public static ushort VolcanicStoneWall = WallID.Cave6Unsafe; // TODO: swap for a proper wall once that layer has one

        private static bool _initialized;

        // Idempotent. Also called from Generate() as a fallback, but call it from
        // PostSetupContent() too so failures surface at load time.
        public static void Init()
        {
            if (_initialized)
                return;

            ShoreSandTile = (ushort)ModContent.TileType<AshenSand>();
            SeafloorTile = (ushort)ModContent.TileType<AshenDirt>();
            VolcanicStoneTile = (ushort)ModContent.TileType<VolcanicStone>();

            // TODO: Give the cliff its own tile when you have one.
            CliffRockTile = SeafloorTile;

            _initialized = true;
        }

        #endregion

        #region Logging

        // TODO: "AshenShoreMod" here is the mod's INTERNAL NAME (from build.txt).
        private static Mod ModInstance =>
            ModLoader.TryGetMod("AshenShoreMod", out Mod mod) ? mod : null;

        #endregion

        #region Tunable constants

        // How many tiles wide the whole biome is, measured from the left world edge.
        public const int BiomeWidth = 400;

        // Vertical shape.
        public const int ShoreCapThickness = 6;
        public const int ShoreSurfaceOffset = 0;
        public const int WaterlineOffset = 8;
        public const int SeafloorDepth = 120;
        public const int SeafloorSolidBuffer = 50;

        // How far the shallow shelf stretches before reaching the full seafloor depth.
        public const int SeafloorRampWidth = 150;
        public const int ShallowSeafloorDepth = 35;

        // Surface waviness - small ripples.
        public const float SurfaceWaveMagnification = 0.02f;
        public const float SurfaceWaveAmplitude = 7f;

        // Surface waviness - big, slow dunes layered under the ripples above so the
        // shore reads as rolling terrain instead of one flat sand slab.
        public const float DuneMagnification = 0.006f;
        public const float DuneAmplitude = 16f;

        // How far below the seafloor buffer we keep backfilling solid ground before
        // opening up into empty space, so the next layer down (Volcanic Stone) has
        // room to generate into instead of hitting a solid floor.
        public const int BackfillDepth = 60;

        // The tail end of that backfill dithers from Ashen Dirt into Volcanic Stone,
        // so the two layers blend at the seam instead of meeting in a hard line.
        public const int TransitionDepth = 40;
        public const float TransitionNoiseMagnification = 0.08f;

        // Waterline waviness.
        public const float WaterlineWaveMagnification = 0.017f;
        public const float WaterlineWaveAmplitude = 6f;

        // Cliff (right-hand boundary).
        public const float CliffZoneStartPercent = 0.68f;
        public const float CliffEdgeMagnification = 0.05f;
        public const float CliffEdgeJaggedness = 14f;

        // Ocean entrance.
        public const int OceanEntranceWidth = 135;
        public const int OceanEntranceHeight = 70;
        public const int OceanEntranceDepth = 40;
        public const float OceanEntranceNoiseMagnification = 0.035f;
        public const float OceanEntranceNoiseStrength = 11f;

        // Seafloor cavern pockets.
        public static readonly (float centerXPercent, float centerYPercent, float radius)[] SeafloorPockets =
        {
            (0.34f, 0.35f, 34f),
            (0.52f, 0.30f, 32f),
        };

        public const float PocketEdgeNoiseMagnification = 0.05f;
        public const float PocketEdgeNoiseStrength = 10f;

        #endregion

        private static int _worldSeed;
        private static int _surfaceY;

        // Shared lower bound for clearing/filling/smoothing, computed once in Setup().
        private static int _clearedBottomY;

        // Per-column seafloor top, captured during GenerateShoreAndSeafloor so later
        // steps carve relative to the real terrain instead of a flat assumed depth.
        private static int[] _seafloorTopByX;

        public static void Setup()
        {
            ModInstance?.Logger.Info("AshenShore.Setup() called");

            Init();

            _worldSeed = WorldGen.genRand.Next();

            // GenVars.waterLine is NOT the row of the ocean's surface - it's a much
            // deeper threshold used for underground/cavern water flooding. Using it
            // here was the reason the whole biome was generating far underground
            // instead of at the beach. Instead, find the real ground height by
            // searching downward from the surface layer, exactly like Calamity's
            // SulphurousSea.DetermineYStart() does for its own sea biome.
            int surfaceCheckX =
                Math.Min(
                    BiomeWidth + 1,
                    Main.maxTilesX - 5
                );

            WorldUtils.Find(
                new Point(surfaceCheckX, (int)GenVars.worldSurfaceLow - 20),
                Searches.Chain(new Searches.Down(3000), new Conditions.IsSolid()),
                out Point groundPoint
            );

            _surfaceY =
                Math.Max(
                    50,
                    groundPoint.Y +
                    ShoreSurfaceOffset
                );

            _clearedBottomY =
                Math.Min(
                    Main.maxTilesY - 5,
                    _surfaceY +
                    SeafloorDepth +
                    SeafloorSolidBuffer +
                    100
                );

            int maxX =
                Math.Min(
                    BiomeWidth,
                    Main.maxTilesX - 5
                );

            _seafloorTopByX = new int[maxX];

            ModInstance?.Logger.Info(
                $"AshenShore groundPoint = {groundPoint} (worldSurfaceLow = {GenVars.worldSurfaceLow})"
            );

            ModInstance?.Logger.Info(
                $"AshenShore surfaceY = {_surfaceY}"
            );
        }

        public static void Generate()
        {
            Setup();
            ReplaceVanillaOcean();
            GenerateShoreAndSeafloor();
            GenerateOceanEntrance();
            GenerateCliffWall();
            CarveSeafloorPockets();
            SmoothPass();
        }

        #region Steps

        // Clears the vanilla ocean and all terrain in the Ashen Shore region first.
        private static void ReplaceVanillaOcean()
        {
            int maxX =
                Math.Min(
                    BiomeWidth,
                    Main.maxTilesX - 5
                );

            int topY =
                Math.Max(
                    5,
                    _surfaceY - 80
                );

            int bottomY = _clearedBottomY;

            for (int x = 0; x < maxX; x++)
            {
                for (int y = topY; y < bottomY; y++)
                    ClearTile(x, y);
            }
        }

        // Builds the shore and seafloor; the floor ramps from shallow to deep near the entrance.
        private static void GenerateShoreAndSeafloor()
        {
            int maxX =
                Math.Min(
                    BiomeWidth,
                    Main.maxTilesX - 5
                );

            for (int x = 0; x < maxX; x++)
            {
                float shoreNoise =
                    FBM(
                        x * SurfaceWaveMagnification,
                        0f,
                        _worldSeed,
                        4
                    ) *
                    SurfaceWaveAmplitude;

                // Big slow dunes on top of the small ripples above, so the beach
                // rolls instead of running flat for the whole biome width.
                float duneNoise =
                    FBM(
                        x * DuneMagnification,
                        50f,
                        _worldSeed,
                        3
                    ) *
                    DuneAmplitude;

                int shoreTop =
                    _surfaceY +
                    (int)shoreNoise +
                    (int)duneNoise;

                float waterNoise =
                    FBM(
                        x * WaterlineWaveMagnification,
                        100f,
                        _worldSeed,
                        4
                    ) *
                    WaterlineWaveAmplitude;

                int waterTop =
                    _surfaceY +
                    WaterlineOffset +
                    (int)waterNoise;

                if (waterTop <= shoreTop)
                    waterTop = shoreTop + ShoreCapThickness;

                float rampProgress =
                    MathHelper.Clamp(
                        x /
                        (float)Math.Max(
                            1,
                            SeafloorRampWidth
                        ),
                        0f,
                        1f
                    );

                float smoothRamp =
                    SmoothStep(rampProgress);

                float floorNoise =
                    FBM(
                        x * 0.025f,
                        300f,
                        _worldSeed,
                        3
                    ) *
                    8f;

                int seafloorDepth =
                    (int)MathHelper.Lerp(
                        ShallowSeafloorDepth,
                        SeafloorDepth,
                        smoothRamp
                    ) +
                    (int)floorNoise;

                int seafloorTop =
                    waterTop +
                    seafloorDepth;

                int seafloorBottom =
                    seafloorTop +
                    SeafloorSolidBuffer;

                if (x < _seafloorTopByX.Length)
                    _seafloorTopByX[x] = seafloorTop;

                // Sand shoreline.
                for (int y = shoreTop; y < waterTop; y++)
                    SetSolid(
                        x,
                        y,
                        ShoreSandTile,
                        ShoreSandWall
                    );

                // Ashen water.
                for (int y = waterTop; y < seafloorTop; y++)
                    SetLiquid(x, y);

                // Ashen Dirt seafloor (the "soft" buffer layer).
                for (int y = seafloorTop; y < seafloorBottom; y++)
                    SetSolid(
                        x,
                        y,
                        SeafloorTile,
                        SeafloorWall
                    );

                // Backfill solid ground for a limited distance below the seafloor
                // buffer - not all the way down to _clearedBottomY. The tail of that
                // backfill dithers from Ashen Dirt into Volcanic Stone; past it we
                // leave the space open (ReplaceVanillaOcean already cleared it) so
                // the next layer down can generate straight into it instead of
                // meeting a solid floor.
                int backfillBottom =
                    Math.Min(
                        _clearedBottomY,
                        seafloorBottom + BackfillDepth
                    );

                int transitionStart =
                    Math.Max(
                        seafloorBottom,
                        backfillBottom - TransitionDepth
                    );

                for (int y = seafloorBottom; y < backfillBottom; y++)
                {
                    ushort tileHere = SeafloorTile;
                    ushort wallHere = SeafloorWall;

                    if (y >= transitionStart)
                    {
                        float transitionProgress =
                            (y - transitionStart) /
                            (float)Math.Max(
                                1,
                                backfillBottom - transitionStart
                            );

                        float dither =
                            FBM(
                                x * TransitionNoiseMagnification,
                                y * TransitionNoiseMagnification,
                                _worldSeed + 700,
                                2
                            ) *
                            0.5f +
                            0.5f;

                        if (dither < transitionProgress)
                        {
                            tileHere = VolcanicStoneTile;
                            wallHere = VolcanicStoneWall;
                        }
                    }

                    SetSolid(
                        x,
                        y,
                        tileHere,
                        wallHere
                    );
                }
            }
        }

        // Carves a large irregular opening at the left edge into the Ashen water.
        private static void GenerateOceanEntrance()
        {
            int maxX =
                Math.Min(
                    OceanEntranceWidth,
                    Main.maxTilesX - 5
                );

            for (int x = 0; x < maxX; x++)
            {
                float progress =
                    x /
                    (float)Math.Max(
                        1,
                        maxX - 1
                    );

                float widthFactor =
                    1f -
                    SmoothStep(progress);

                float noise =
                    FBM(
                        x * OceanEntranceNoiseMagnification,
                        450f,
                        _worldSeed,
                        4
                    ) *
                    OceanEntranceNoiseStrength;

                int openingHeight =
                    Math.Max(
                        8,
                        (int)(
                            OceanEntranceHeight *
                            widthFactor
                        ) +
                        (int)noise
                    );

                int entranceTop =
                    _surfaceY -
                    openingHeight;

                int entranceBottom =
                    _surfaceY +
                    OceanEntranceDepth;

                for (int y = entranceTop; y < entranceBottom; y++)
                {
                    float verticalProgress =
                        (y - entranceTop) /
                        (float)Math.Max(
                            1,
                            entranceBottom - entranceTop
                        );

                    float narrowing =
                        MathHelper.Lerp(
                            1f,
                            0.70f,
                            verticalProgress
                        );

                    float center =
                        maxX *
                        0.35f;

                    float horizontalDistance =
                        Math.Abs(
                            x -
                            center
                        );

                    float horizontalLimit =
                        maxX *
                        0.35f *
                        narrowing;

                    if (horizontalDistance <= horizontalLimit)
                    {
                        if (y < _surfaceY + WaterlineOffset)
                            ClearTile(x, y);
                        else
                            SetLiquid(x, y);
                    }
                }
            }

            // The mouth is completely open to the left world edge.
            int edgeBottom =
                Math.Min(
                    Main.maxTilesY - 5,
                    _surfaceY +
                    OceanEntranceDepth
                );

            for (int x = 0; x < 10; x++)
            {
                for (int y = _surfaceY - 20; y < edgeBottom; y++)
                {
                    if (y < _surfaceY + WaterlineOffset)
                        ClearTile(x, y);
                    else
                        SetLiquid(x, y);
                }
            }
        }

        /// <summary>
        /// Carves the tall jagged rock wall on the right side of the biome.
        /// </summary>
        private static void GenerateCliffWall()
        {
            int cliffZoneStart =
                (int)(
                    BiomeWidth *
                    CliffZoneStartPercent
                );

            int maxX =
                Math.Min(
                    BiomeWidth,
                    Main.maxTilesX - 5
                );

            for (int x = cliffZoneStart; x < maxX; x++)
            {
                // Cap the cliff face at the same open-ended depth as the seafloor
                // backfill next to it, instead of running solid all the way to
                // _clearedBottomY - otherwise the cliff would wall off the open
                // bottom the seafloor just left for the next layer.
                int columnSeafloorTop =
                    x < _seafloorTopByX.Length
                        ? _seafloorTopByX[x]
                        : _surfaceY + SeafloorDepth;

                int seafloorBottom =
                    Math.Min(
                        _clearedBottomY,
                        columnSeafloorTop +
                        SeafloorSolidBuffer +
                        BackfillDepth
                    );

                float zoneProgress =
                    (x - cliffZoneStart) /
                    (float)Math.Max(
                        1,
                        BiomeWidth -
                        cliffZoneStart
                    );

                float edgeNoise =
                    FBM(
                        x * CliffEdgeMagnification,
                        200f,
                        _worldSeed,
                        4
                    ) *
                    CliffEdgeJaggedness;

                int rockFaceTop =
                    _surfaceY -
                    (int)(
                        zoneProgress *
                        40f
                    );

                int rockFaceDepth =
                    (int)(
                        zoneProgress *
                        SeafloorDepth *
                        0.5f +
                        edgeNoise
                    );

                int rockFaceY =
                    _surfaceY +
                    rockFaceDepth;

                int startY =
                    Math.Max(
                        5,
                        rockFaceTop
                    );

                int endY =
                    Math.Min(
                        Main.maxTilesY - 5,
                        seafloorBottom
                    );

                for (int y = startY; y < endY; y++)
                {
                    if (y < rockFaceY)
                        continue;

                    SetSolid(
                        x,
                        y,
                        CliffRockTile,
                        CliffRockWall
                    );
                }
            }
        }

        // Carves two noise-perturbed circular pockets into the seafloor.
        private static void CarveSeafloorPockets()
        {
            foreach (var pocket in SeafloorPockets)
            {
                int centerX =
                    (int)(
                        BiomeWidth *
                        pocket.centerXPercent
                    );

                // Pockets sit inside the shallow-water ramp, so use the real depth
                // recorded for that column instead of the flat SeafloorDepth constant.
                int clampedCenterX =
                    Math.Max(
                        0,
                        Math.Min(
                            centerX,
                            _seafloorTopByX.Length - 1
                        )
                    );

                int seafloorTop = _seafloorTopByX[clampedCenterX];

                int centerY =
                    seafloorTop +
                    (int)(
                        pocket.centerYPercent *
                        SeafloorSolidBuffer
                    );

                int radius =
                    (int)pocket.radius;

                int minX =
                    Math.Max(
                        5,
                        centerX -
                        radius -
                        10
                    );

                int maxX =
                    Math.Min(
                        Main.maxTilesX - 5,
                        centerX +
                        radius +
                        10
                    );

                int minY =
                    Math.Max(
                        5,
                        centerY -
                        radius -
                        10
                    );

                int maxY =
                    Math.Min(
                        Main.maxTilesY - 5,
                        centerY +
                        radius +
                        10
                    );

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        float edgeNoise =
                            FBM(
                                x * PocketEdgeNoiseMagnification,
                                y * PocketEdgeNoiseMagnification,
                                _worldSeed + 500,
                                3
                            ) *
                            PocketEdgeNoiseStrength;

                        // Modulate the radius by angle so the pocket reads as an
                        // irregular lobed cavern instead of a near-perfect circle -
                        // sampling noise around a small ring (via cos/sin) keeps it
                        // continuous all the way around instead of jumping at the
                        // angle wrap.
                        float angle =
                            (float)Math.Atan2(
                                y - centerY,
                                x - centerX
                            );

                        float angularNoise =
                            FBM(
                                (float)Math.Cos(angle) * 2.5f + 40f,
                                (float)Math.Sin(angle) * 2.5f + 40f,
                                _worldSeed + 500,
                                3
                            ) *
                            (radius * 0.4f);

                        float effectiveRadius =
                            radius +
                            angularNoise;

                        float dist =
                            Vector2.Distance(
                                new Vector2(
                                    x,
                                    y
                                ),
                                new Vector2(
                                    centerX,
                                    centerY
                                )
                            ) +
                            edgeNoise;

                        if (dist <= effectiveRadius)
                            SetLiquid(x, y);
                    }
                }
            }

            ConnectSeafloorPockets();
        }

        // Carves a winding channel between the two seafloor pockets so they read as
        // one irregular cavern system instead of two circles that happen to touch.
        private static void ConnectSeafloorPockets()
        {
            if (SeafloorPockets.Length < 2)
                return;

            var a = SeafloorPockets[0];
            var b = SeafloorPockets[1];

            int ax = (int)(BiomeWidth * a.centerXPercent);
            int bx = (int)(BiomeWidth * b.centerXPercent);

            int clampedAx =
                Math.Max(0, Math.Min(ax, _seafloorTopByX.Length - 1));

            int clampedBx =
                Math.Max(0, Math.Min(bx, _seafloorTopByX.Length - 1));

            int ay =
                _seafloorTopByX[clampedAx] +
                (int)(a.centerYPercent * SeafloorSolidBuffer);

            int by =
                _seafloorTopByX[clampedBx] +
                (int)(b.centerYPercent * SeafloorSolidBuffer);

            int startX = Math.Min(ax, bx);
            int endX = Math.Max(ax, bx);
            int steps = Math.Max(1, endX - startX);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = startX + (int)((endX - startX) * t);

                int centerY =
                    ax <= bx
                        ? (int)MathHelper.Lerp(ay, by, t)
                        : (int)MathHelper.Lerp(by, ay, t);

                float wobble =
                    FBM(
                        x * 0.04f,
                        600f,
                        _worldSeed,
                        2
                    ) *
                    5f;

                int channelY = centerY + (int)wobble;

                float widthNoise =
                    FBM(
                        x * 0.06f,
                        650f,
                        _worldSeed,
                        2
                    ) *
                    2f;

                int channelHalfHeight = 4 + (int)widthNoise;

                for (int y = channelY - channelHalfHeight; y <= channelY + channelHalfHeight; y++)
                    SetLiquid(x, y);
            }
        }

        // Smooths slopes along solid tiles within the biome bounds.
        private static void SmoothPass()
        {
            int maxX =
                Math.Min(
                    BiomeWidth - 2,
                    Main.maxTilesX - 5
                );

            int minY =
                Math.Max(
                    5,
                    _surfaceY - 70
                );

            int maxY = _clearedBottomY;

            for (int x = 2; x < maxX; x++)
            {
                for (int y = minY; y < maxY - 2; y++)
                {
                    if (Main.tile[x, y].HasTile)
                    {
                        Tile.SmoothSlope(
                            x,
                            y,
                            false
                        );
                    }
                }
            }
        }

        #endregion

        #region Tile helpers

        private static void SetSolid(
            int x,
            int y,
            ushort tileType,
            ushort wallType)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return;

            Tile tile = Main.tile[x, y];

            tile.TileType = tileType;
            tile.WallType = wallType;
            tile.LiquidAmount = 0;
            tile.HasTile = true;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
        }

        private static void SetLiquid(
            int x,
            int y)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return;

            Tile tile = Main.tile[x, y];

            tile.HasTile = false;
            tile.WallType = WallID.None;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            tile.Get<LiquidData>().LiquidType = LiquidID.Water;
            tile.LiquidAmount = byte.MaxValue;
        }

        private static void ClearTile(
            int x,
            int y)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return;

            Tile tile = Main.tile[x, y];

            tile.HasTile = false;
            tile.WallType = WallID.None;
            tile.LiquidAmount = 0;
            tile.LiquidType = LiquidID.Water;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
        }

        #endregion

        #region Noise

        // Small hand-rolled value-noise + FBM implementation so this file has zero
        // dependency on CalamityMod's NoiseHelper/CalamityUtils.

        private static float Hash(
            int x,
            int y,
            int seed)
        {
            unchecked
            {
                int h =
                    x * 374761393 +
                    y * 668265263 +
                    seed * 1274126177;

                h =
                    (h ^ (h >> 13)) *
                    1274126177;

                h ^= h >> 16;

                return (h & 0x7fffffff) /
                       (float)int.MaxValue;
            }
        }

        private static float SmoothStep(float t) =>
            t * t * (3f - 2f * t);

        private static float ValueNoise(
            float x,
            float y,
            int seed)
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

            float ix0 =
                MathHelper.Lerp(
                    n00,
                    n10,
                    sx
                );

            float ix1 =
                MathHelper.Lerp(
                    n01,
                    n11,
                    sx
                );

            return MathHelper.Lerp(
                ix0,
                ix1,
                sy
            );
        }

        /// <summary>
        /// Fractal brownian motion; returns roughly -1..1.
        /// </summary>
        private static float FBM(
            float x,
            float y,
            int seed,
            int octaves,
            float gain = 0.5f,
            float lacunarity = 2f)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float sum = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n =
                    ValueNoise(
                        x * frequency,
                        y * frequency,
                        seed + i * 101
                    ) * 2f - 1f;

                sum += n * amplitude;

                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum;
        }

        #endregion

        #region World Generation

        // Runs before "Settle Liquids Again" so vanilla still settles/frames our water,
        // and before Micro Biomes/Water Plants/Stalac/Final Cleanup so they see finished
        // terrain instead of half-settled liquid.
        public class AshenShoreWorldGen : ModSystem
        {
            public override void PostSetupContent()
            {
                AshenShore.Init();
            }

            public override void ModifyWorldGenTasks(
                List<GenPass> tasks,
                ref double totalWeight)
            {
                int settleLiquidsIndex =
                    tasks.FindIndex(
                        genPass =>
                            genPass.Name.Equals(
                                "Settle Liquids Again"
                            )
                    );

                int insertAt =
                    settleLiquidsIndex == -1
                        ? tasks.Count
                        : settleLiquidsIndex;

                tasks.Insert(
                    insertAt,
                    new AshenShoreGenPass()
                );
            }

            private class AshenShoreGenPass : GenPass
            {
                public AshenShoreGenPass()
                    : base(
                        "Ashen Shore",
                        1f
                    )
                {
                }

                protected override void ApplyPass(
                    GenerationProgress progress,
                    GameConfiguration configuration)
                {
                    progress.Message =
                        "Creating Ashen Shore";

                    ModInstance?.Logger.Info(
                        "Ashen Shore: generation starting."
                    );

                    AshenShore.Generate();

                    ModInstance?.Logger.Info(
                        "Ashen Shore: generation finished."
                    );
                }
            }
        }

        #endregion
    }
}