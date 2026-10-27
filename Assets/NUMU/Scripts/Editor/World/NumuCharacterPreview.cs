using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>Renders a character's face in every emotion to PNGs, for checking expressions and materials.</summary>
    public static class NumuCharacterPreview
    {
        public static string RenderExpressions(string avatar, string outDir, string poseClip = "f_idle_neutral_01")
        {
            Directory.CreateDirectory(outDir);
            var prefab = NumuCharacterSetup.Prefab(avatar);
            if (prefab == null) return $"No prefab for {avatar}";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(0, -400, 0);
            go.transform.rotation = Quaternion.Euler(0, 180, 0);
            var clip = NumuCharacterSetup.Clip(poseClip);
            if (clip != null) clip.SampleAnimation(go, 1.5f);
            var anim = go.GetComponent<Animator>();
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            var face = go.GetComponent<NumuFacialExpressions>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;

            var lightGo = new GameObject("PreviewKey");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.cullingMask = 1 << 30;
            lightGo.transform.rotation = Quaternion.Euler(25, 150, 0);

            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            cam.cullingMask = 1 << 30;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.86f, 0.9f, 1f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.05f;
            Vector3 headPos = head.position + Vector3.up * 0.06f;
            cam.transform.position = headPos + go.transform.forward * 0.85f;
            cam.transform.LookAt(headPos);
            var rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var sb = new System.Text.StringBuilder();
            // Skinning is evaluated once per editor frame, so bake each expression into a static mesh to render it.
            var smr = face.face;
            var baked = new GameObject("Baked");
            baked.layer = 30;
            baked.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            var bakedMesh = new Mesh();
            baked.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
            baked.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            smr.enabled = false;
            foreach (NumuEmotion e in System.Enum.GetValues(typeof(NumuEmotion)))
            {
                face.SetExpression(e, 1f);
                face.ApplyImmediately();
                smr.BakeMesh(bakedMesh, true);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"{outDir}/{avatar}_{e}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                sb.Append(e).Append(' ');
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(bakedMesh);
            Object.DestroyImmediate(go);
            return "rendered " + sb;
        }
    }
}
