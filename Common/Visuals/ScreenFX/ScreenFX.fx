sampler uImage0 : register(s0);

float4 fxPointA[4];
float4 fxPointB[4];
float2 fxResolution;
float2 fxBlurCenter;
float fxAspect;
float fxTime;

float fxAberration;
float fxZoomBlur;
float fxMotionX;
float fxMotionY;
float fxWobble;
float fxWobbleFreq;
float fxWobbleSpeed;
float fxGlitch;
float fxLens;
float fxPixelate;
float fxPosterize;
float fxScanlines;
float fxGrain;
float fxSaturation;
float fxContrast;
float fxExposure;
float fxBrightness;
float fxHue;
float fxTint;
float3 fxTintColor;
float fxInvert;
float fxVignette;
float3 fxVignetteColor;
float fxFade;
float3 fxFadeColor;

float Hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float4 Tex(float2 uv)
{
    return tex2D(uImage0, clamp(uv, 0.0005, 0.9995));
}

float3 Tap(float2 uv, float ca)
{
    float2 d = (uv - 0.5) * ca;
    return float3(Tex(uv + d).r, Tex(uv).g, Tex(uv - d).b);
}

float3 Hue(float3 c, float a)
{
    float s = sin(a);
    float co = cos(a);
    float3 k = float3(0.57735, 0.57735, 0.57735);
    return c * co + cross(k, c) * s + k * dot(k, c) * (1.0 - co);
}

// xy = warped uv, z = chromatic aberration amount
float3 Warp(float2 uv)
{
    float2 asp = float2(fxAspect, 1.0);
    float2 offset = 0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float4 a = fxPointA[i];
        float4 b = fxPointB[i];
        float2 d = (uv - a.xy) * asp;
        float dist = max(length(d), 0.0001);

        float x = (dist - a.z) / a.w;
        offset += (d / dist) * (x * exp(-x * x * 2.0) * b.x) / asp;

        float fall = saturate(1.0 - dist / a.z);
        fall *= fall;

        float s;
        float c;
        sincos(b.y * fall, s, c);
        float2 rd = float2(d.x * c - d.y * s, d.x * s + d.y * c);
        offset += (d - rd) / asp;
        offset += d * (b.z * fall) / asp;
    }

    uv -= offset;

    float2 lc = (uv - 0.5) * asp;
    uv = 0.5 + (uv - 0.5) * (1.0 + fxLens * dot(lc, lc));

    float wt = fxTime * fxWobbleSpeed;
    uv.x += sin(uv.y * 40.0 * fxWobbleFreq + wt * 3.0) * fxWobble;
    uv.y += cos(uv.x * 32.0 * fxWobbleFreq + wt * 2.4) * fxWobble * 0.6;

    float band = floor(uv.y * 28.0);
    float tick = floor(fxTime * 14.0);
    float hit = step(1.0 - fxGlitch * 0.45, Hash(band * 12.9898 + tick * 78.233));
    uv.x += (Hash(band * 4.1 + tick * 9.7) - 0.5) * 0.14 * hit;

    float2 cell = max(fxPixelate, 1.0) / fxResolution;
    float2 snapped = (floor(uv / cell) + 0.5) * cell;
    uv = lerp(uv, snapped, step(1.5, fxPixelate));

    return float3(uv, fxAberration + hit * 0.012);
}

// uv = the original screen position, so overlays stay fixed on screen while the picture warps.
float4 Finish(float3 c, float2 uv)
{
    c = c * fxExposure + fxBrightness;
    c = Hue(c, fxHue);

    float luma = dot(c, float3(0.299, 0.587, 0.114));
    c = lerp(float3(luma, luma, luma), c, fxSaturation);
    c = (c - 0.5) * fxContrast + 0.5;
    c = lerp(c, c * fxTintColor, fxTint);

    float lv = max(fxPosterize, 1.0);
    c = lerp(c, floor(c * lv + 0.5) / lv, step(1.5, fxPosterize));

    c *= 1.0 - fxScanlines * (0.5 + 0.5 * sin(uv.y * fxResolution.y * 3.14159));
    c += (Hash(dot(uv * fxResolution, float2(12.9898, 78.233)) + fxTime * 60.0) - 0.5) * fxGrain;

    float2 v = (uv - 0.5) * float2(fxAspect, 1.0);
    c = lerp(c, fxVignetteColor, smoothstep(0.35, 1.1, length(v)) * fxVignette);

    c = lerp(c, 1.0 - c, fxInvert);
    c = lerp(c, fxFadeColor, fxFade);

    return float4(saturate(c), 1.0);
}

// Everything, including zoom and motion blur (15 texture reads)
float4 PS_Full(float2 uv : TEXCOORD0) : COLOR0
{
    float3 w = Warp(uv);
    float2 dir = (fxBlurCenter - w.xy) * fxZoomBlur + float2(fxMotionX, fxMotionY);
    float3 c = Tap(w.xy, w.z);

    [unroll]
    for (int j = 1; j <= 4; j++)
        c += Tap(w.xy + dir * (j * 0.25), w.z);

    return Finish(c / 5.0, uv);
}

// Warping and aberration without blur (3 texture reads)
float4 PS_Distort(float2 uv : TEXCOORD0) : COLOR0
{
    float3 w = Warp(uv);
    return Finish(Tap(w.xy, w.z), uv);
}

// Color work and overlays only (1 texture read)
float4 PS_Grade(float2 uv : TEXCOORD0) : COLOR0
{
    return Finish(Tex(uv).rgb, uv);
}

technique Technique1
{
    pass ScreenFX
    {
        PixelShader = compile ps_3_0 PS_Full();
    }

    pass Distort
    {
        PixelShader = compile ps_3_0 PS_Distort();
    }

    pass Grade
    {
        PixelShader = compile ps_3_0 PS_Grade();
    }
}
