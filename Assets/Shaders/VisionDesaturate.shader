// Full-screen post-process shader, driven by VisionDesaturateFeature.cs (a hand-written
// ScriptableRendererFeature - NOT the built-in Full Screen Pass Renderer Feature, whose
// automatic color-buffer fetch did not work on this project's Renderer2D setup).
//
// Desaturates whatever's already on screen outside the player's vision, using a
// world-aligned alpha texture baked by VisionConeMask (see VisionCone.cs) - the
// same texture that carries the cone/circle shape, obstacle occlusion, and the
// soft edge fade.
//
// SETUP (in the Unity Editor, on Assets/Settings/Renderer2D.asset):
// 1. Add Renderer Feature -> Vision Desaturate Feature.
// 2. Create a Material using this shader, assign it to that feature's "Material" field.
Shader "FullScreen/VisionDesaturate"
{
    // _DarknessFloor: outside the vision cone, grayscale is multiplied by this before
    // display. 0 = pitch black, 1 = no darkening at all.
    Properties
    {
        _DarknessFloor("Darkness Floor", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VisionDesaturatePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_VisionTex);
            SAMPLER(sampler_VisionTex);

            float2 _VisionOrigin;
            float _VisionWorldDiameter;
            float2 _VisionCamWorldPos;
            float _VisionOrthoSize;
            float _DarknessFloor;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 ndc = uv * 2.0 - 1.0;
                float2 worldPos = _VisionCamWorldPos + ndc * float2(_VisionOrthoSize * aspect, _VisionOrthoSize);

                float2 localUV = (worldPos - _VisionOrigin) / _VisionWorldDiameter + 0.5;

                // Before VisionConeMask has run once (e.g. in the Editor outside Play mode),
                // _VisionWorldDiameter is 0. Treat that as "no effect yet" instead of
                // dividing by zero and desaturating everything.
                float visibility = 1.0;
                if (_VisionWorldDiameter > 0.0 &&
                    localUV.x >= 0.0 && localUV.x <= 1.0 && localUV.y >= 0.0 && localUV.y <= 1.0)
                {
                    visibility = SAMPLE_TEXTURE2D(_VisionTex, sampler_VisionTex, localUV).a;
                }
                else if (_VisionWorldDiameter > 0.0)
                {
                    visibility = 0.0;
                }

                half luminance = dot(sceneColor.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 grayscale = half3(luminance, luminance, luminance);
                half3 darkOutside = grayscale * _DarknessFloor;

                // visibility: 1 = fully seen (full color), 0 = fully hidden (dark grayscale).
                // Multiplying the floor in (rather than alpha-blending an overlay) means there's
                // nothing left to compound into haze.
                half3 finalColor = lerp(darkOutside, sceneColor.rgb, visibility);
                return half4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
