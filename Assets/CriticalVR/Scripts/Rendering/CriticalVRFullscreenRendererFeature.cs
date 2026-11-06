using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace CriticalVR.Rendering
{
    /// <summary>
    /// URP full-screen pass for the Critical-VR effect material.
    /// Add this feature to the active URP Renderer Data asset and assign the same
    /// material that MotionToExperienceManager updates at runtime.
    /// </summary>
    public class CriticalVRFullscreenRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material material;
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;
        }

        private class FullscreenPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private readonly ProfilingSampler _profilingSampler = new("CriticalVR Fullscreen Pass");
            private RTHandle _temporaryColor;

            public FullscreenPass(Material material, RenderPassEvent injectionPoint)
            {
                _material = material;
                renderPassEvent = injectionPoint;
                ConfigureInput(ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                if (_material == null || camera.isPreviewCamera || resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "CriticalVR Effect Color";
                descriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(descriptor);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                renderGraph.AddBlitPass(parameters, passName: "CriticalVR Effect");
                resources.cameraColor = destination;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _temporaryColor, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CriticalVRTemp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || renderingData.cameraData.isPreviewCamera)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, source, _temporaryColor, _material, 0);
                    Blitter.BlitCameraTexture(cmd, _temporaryColor, source);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _temporaryColor?.Release();
            }
        }

        [SerializeField] private Settings settings = new();
        private FullscreenPass _pass;

        public override void Create()
        {
            _pass?.Dispose();
            _pass = settings.material == null ? null : new FullscreenPass(settings.material, settings.injectionPoint);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || settings.material == null)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _pass?.Dispose();
            _pass = null;
        }
    }
}
