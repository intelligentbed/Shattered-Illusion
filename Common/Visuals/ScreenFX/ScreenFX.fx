sampler uImage0 : register(s0);

float4 fxRipple[4];
float fxRippleWidth[4];
float fxAspect;
float fxTime;
float fxAberration;
float fxBlur;
float2 fxBlurCenter;
float fxWobble;
float fxGlitch;
float fxSaturation;
float fxContrast;
float3 fxTint;
float fxTintAmount;
float fxInvert;

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

float4 PS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 offset = 0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float2 d = (uv - fxRipple[i].xy) * float2(fxAspect, 1.0);
        float dist = max(length(d), 0.0001);
        float x = (dist - fxRipple[i].z) / fxRippleWidth[i];
        float k = x * exp(-x * x * 2.0) * fxRipple[i].w;
        offset += (d / dist) * k / float2(fxAspect, 1.0);
    }

    uv -= offset;

    uv.x += sin(uv.y * 40.0 + fxTime * 3.0) * fxWobble;
    uv.y += cos(uv.x * 32.0 + fxTime * 2.4) * fxWobble * 0.6;

    float band = floor(uv.y * 28.0);
    float tick = floor(fxTime * 14.0);
    float hit = step(1.0 - fxGlitch * 0.45, Hash(band * 12.9898 + tick * 78.233));
    uv.x += (Hash(band * 4.1 + tick * 9.7) - 0.5) * 0.14 * hit;
    float ca = fxAberration + hit * 0.012;

    float2 dir = (fxBlurCenter - uv) * fxBlur;
    float3 c = Tap(uv, ca);

    [unroll]
    for (int j = 1; j <= 4; j++)
        c += Tap(uv + dir * (j * 0.25), ca);

    c /= 5.0;

    float luma = dot(c, float3(0.299, 0.587, 0.114));
    c = lerp(float3(luma, luma, luma), c, fxSaturation);
    c = (c - 0.5) * fxContrast + 0.5;
    c = lerp(c, c * fxTint, fxTintAmount);
    c = lerp(c, 1.0 - c, fxInvert);

    return float4(saturate(c), 1.0);
}

technique Technique1
{
    pass ScreenFX
    {
        PixelShader = compile ps_3_0 PS();
    }
}
