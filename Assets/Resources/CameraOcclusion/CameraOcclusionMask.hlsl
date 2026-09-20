#ifndef ERUMPEREM_CAMERA_OCCLUSION
#define ERUMPEREM_CAMERA_OCCLUSION

float _OcclusionAmount;
float _CameraOcclusionActive;
float4 _CameraOcclusionTarget;
float4 _CameraOcclusionSettings;

float OcclusionHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float OcclusionNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(OcclusionHash(cell), OcclusionHash(cell + float2(1, 0)), f.x),
                lerp(OcclusionHash(cell + float2(0, 1)), OcclusionHash(cell + 1), f.x), f.y);
}

void ApplyCameraOcclusion(float4 positionCS)
{
    if (_CameraOcclusionActive < 0.5 || _OcclusionAmount < 0.001) return;
    float depth = positionCS.z;
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
    #endif
    float3 positionWS = ComputeWorldSpacePosition(positionCS.xy / _ScaledScreenParams.xy, depth, UNITY_MATRIX_I_VP);
    float3 cameraPosition = GetCameraPositionWS();
    float3 toTarget = _CameraOcclusionTarget.xyz - cameraPosition;
    float targetDistance = length(toTarget);
    float3 axis = toTarget / max(targetDistance, 0.001);
    float3 toPixel = positionWS - cameraPosition;
    float along = dot(toPixel, axis);
    if (along <= 0 || along >= targetDistance - _CameraOcclusionSettings.w) return;

    float radial = length(toPixel - axis * along);
    float radius = _CameraOcclusionSettings.x * smoothstep(0, 1, _OcclusionAmount);
    float softness = max(_CameraOcclusionSettings.y * _OcclusionAmount, 0.001);
    float2 noiseUV = (positionWS.xz + positionWS.y * 0.37) * 3.0;
    noiseUV += float2(0.13, -0.19) * _Time.y * _CameraOcclusionSettings.z;
    float smoke = OcclusionNoise(noiseUV) * 0.7 + OcclusionNoise(noiseUV * 2.1) * 0.3;
    float coverage = smoothstep(radius - softness, radius + softness, radial + (smoke - 0.5) * softness * 1.8);
    float dither = frac(52.9829189 * frac(dot(floor(positionCS.xy), float2(0.06711056, 0.00583715))));
    clip(coverage - max(dither, 0.0001));
}
#endif
