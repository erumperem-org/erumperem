#define DepthOnlyFragment OriginalDepthOnlyFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
#undef DepthOnlyFragment
#include "Assets/Resources/CameraOcclusion/CameraOcclusionMask.hlsl"
half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ApplyCameraOcclusion(input.positionCS);
    return OriginalDepthOnlyFragment(input);
}
