#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CriticalVR.EditorTools
{
    // Records the actual variants after pipeline stripping; configuration alone is not evidence.
    public sealed class CriticalVRShaderBuildCheck : IPreprocessShaders
    {
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name != "CriticalVR/PassthroughTrip") return;
            foreach (var variant in data)
            {
                string keywords = string.Join(",", variant.shaderKeywordSet.GetShaderKeywords().Select(keyword => keyword.name));
                Debug.Log($"[CriticalVR.Shader] {snippet.shaderType} {variant.shaderCompilerPlatform}: {keywords}");
            }
            if (data.Count == 0)
                throw new UnityEditor.Build.BuildFailedException("CriticalVR shader has no retained variants.");
        }
    }
}
#endif
