using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
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
