using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Numu.EditorTools.World
{
    /// <summary>Renders the open scene from an arbitrary viewpoint to a PNG (Editor QA screenshots).</summary>
    public static class NumuViewRender
    {
        public static string Render(Vector3 position, Vector3 lookAt, float fov, string path, int width = 1600, int height = 900)
        {
            var go = new GameObject("QA Camera");
            var cam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.cullingMask = ~(1 << 5);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return path;
        }
    }
}
