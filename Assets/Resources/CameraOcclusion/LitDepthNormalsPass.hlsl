#define DepthNormalsFragment OriginalDepthNormalsFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
#undef DepthNormalsFragment
#include "Assets/Resources/CameraOcclusion/CameraOcclusionMask.hlsl"
void DepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ApplyCameraOcclusion(input.positionCS);
    OriginalDepthNormalsFragment(input, outNormalWS
#ifdef _WRITE_RENDERING_LAYERS
, outRenderingLayers
#endif
);
}
