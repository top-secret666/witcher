using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Witcher.Visuals
{
    /// <summary>
    /// URP renderer feature: pixelate + dither + posterize for retro dark-fantasy look.
    /// Add via menu: Witcher → 1. Add Retro Look To URP Renderer
    /// </summary>
    public sealed class RetroLookFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            // Hybrid retro FPS: chunky pixels + dither (pair with URP renderScale ~0.35).
            [Range(1, 16)] public float pixelSize = 2.5f;
            [Range(4, 32)] public float colorSteps = 14f;
            [Range(0f, 1f)] public float ditherStrength = 0.28f;
            [Range(0.5f, 2f)] public float contrast = 1.1f;
            [Range(0f, 1.5f)] public float saturation = 1.0f;
            [Range(0f, 1f)] public float vignette = 0.22f;
            public Color tint = new Color(0.95f, 0.96f, 1f, 1f);
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public Settings settings = new Settings();

        Material _material;
        RetroLookPass _pass;

        public override void Create()
        {
            var shader = Shader.Find("Witcher/RetroLook");
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);

            _pass = new RetroLookPass();
            _pass.renderPassEvent = settings.injectionPoint;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            ApplyMaterialProperties();
            _pass.Setup(_material);
            _pass.renderPassEvent = settings.injectionPoint;
            renderer.EnqueuePass(_pass);
        }

        void ApplyMaterialProperties()
        {
            _material.SetFloat("_PixelSize", settings.pixelSize);
            _material.SetFloat("_ColorSteps", settings.colorSteps);
            _material.SetFloat("_DitherStrength", settings.ditherStrength);
            _material.SetFloat("_Contrast", settings.contrast);
            _material.SetFloat("_Saturation", settings.saturation);
            _material.SetFloat("_Vignette", settings.vignette);
            _material.SetColor("_Tint", settings.tint);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        sealed class RetroLookPass : ScriptableRenderPass
        {
            Material _material;

            public void Setup(Material material)
            {
                _material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                var source = resourceData.activeColorTexture;
                var destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-WitcherRetroLook";
                destinationDesc.clearBuffer = false;
                var destination = renderGraph.CreateTexture(destinationDesc);

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                renderGraph.AddBlitPass(blitParams, passName: "Witcher Retro Look");

                resourceData.cameraColor = destination;
            }
        }
    }
}
