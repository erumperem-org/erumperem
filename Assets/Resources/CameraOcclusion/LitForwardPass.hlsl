#define LitPassFragment OriginalLitPassFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef LitPassFragment
#include "Assets/Resources/CameraOcclusion/CameraOcclusionMask.hlsl"
void LitPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ApplyCameraOcclusion(input.positionCS);
    OriginalLitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
, outRenderingLayers
#endif
);
}
