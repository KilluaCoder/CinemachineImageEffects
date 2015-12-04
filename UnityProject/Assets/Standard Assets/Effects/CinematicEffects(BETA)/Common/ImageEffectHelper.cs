using System.Collections.Generic;
using UnityEngine;

namespace UnityStandardAssets.ImageEffects
{
    public static class ImageEffectHelper
    {
        public static bool IsSupported(Shader s, bool needDepth, bool needHdr, MonoBehaviour effect)
        {
            if (s == null || !s.isSupported)
            {
                Debug.LogWarningFormat("Missing shader for image effect {0}", effect);
                return false;
            }

            if (!SystemInfo.supportsImageEffects || !SystemInfo.supportsRenderTextures)
                return false;

            if (needDepth && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
                return false;

            if (needHdr && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                return false;

            return true;
        }

        public static Material CheckShaderAndCreateMaterial(Shader s)
        {
            if (s == null || !s.isSupported)
                return null;

            var material = new Material(s);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        public static bool supportsDX11
        {
            get { return SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders; }
        }

        //Temporary render texture handling
        private static Dictionary<MonoBehaviour, List<RenderTexture>> s_TemporaryRTs = new Dictionary<MonoBehaviour, List<RenderTexture>>();

        public static RenderTexture GetTemporaryRenderTexture(MonoBehaviour target, int width, int height, RenderTextureFormat format = RenderTextureFormat.ARGBHalf, FilterMode filterMode = FilterMode.Bilinear)
        {
            if (target == null)
            {
                Debug.LogError("Null MonoBehaviour passed to GetTemporary");
                return null;
            }

            List<RenderTexture> textureList;
            if (!s_TemporaryRTs.TryGetValue(target, out textureList))
            {
                textureList = new List<RenderTexture>();
                s_TemporaryRTs.Add(target, textureList);
            }

            var rt = RenderTexture.GetTemporary(width, height, 0, format);
            rt.filterMode = filterMode;
            rt.wrapMode = TextureWrapMode.Clamp;
            textureList.Add(rt);
            return rt;
        }

        public static void ReleaseTemporaryRenderTexture(MonoBehaviour target, RenderTexture rt)
        {
            if (target == null)
                return;

            List<RenderTexture> textures;
            s_TemporaryRTs.TryGetValue(target, out textures);

            if (textures == null)
                return;

            if (!textures.Contains(rt))
            {
                Debug.LogErrorFormat("Attempting to remove texture that was not allocated by {0}", target);
                return;
            }

            textures.Remove(rt);
            RenderTexture.ReleaseTemporary(rt);

            if (textures.Count == 0)
                s_TemporaryRTs.Remove(target);
        }

        public static void ReleaseAllTemporyRenderTexutres(MonoBehaviour target)
        {
            if (target == null)
                return;

            List<RenderTexture> textures;
            s_TemporaryRTs.TryGetValue(target, out textures);

            if (textures == null)
                return;

            foreach (var rt in textures)
                RenderTexture.ReleaseTemporary(rt);

            textures.Clear();
            s_TemporaryRTs.Remove(target);
        }
    }
}
