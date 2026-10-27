using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Numu.EditorTools
{
    /// <summary>Editor conveniences: open the world scene and pin the Game view to the reference resolution.</summary>
    public static class NumuEditorUtil
    {
        [MenuItem("NUMU/Open World Scene")]
        public static void OpenWorld()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(NumuWorldBuilder.ScenePath);
        }

        [MenuItem("NUMU/Game View 1584x993 (reference)")]
        public static void ReferenceSize() => SetGameViewSize(1584, 993, "NUMU Reference 1584x993");

        [MenuItem("NUMU/Game View 1920x1080 (16:9)")]
        public static void WideSize() => SetGameViewSize(1920, 1080, "NUMU 1920x1080");

        /// <summary>Adds (once) and selects a fixed Game view resolution. Uses internal editor types.</summary>
        public static string SetGameViewSize(int width, int height, string label)
        {
            var asm = typeof(Editor).Assembly;
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var sizeKindType = asm.GetType("UnityEditor.GameViewSizeType");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            var groupType = group.GetType();

            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            var getSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < count; i++)
            {
                object s = getSize.Invoke(group, new object[] { i });
                int w = (int)sizeType.GetProperty("width").GetValue(s);
                int h = (int)sizeType.GetProperty("height").GetValue(s);
                string text = (string)sizeType.GetProperty("baseText").GetValue(s);
                if (w == width && h == height && text == label) { index = i; break; }
            }
            if (index < 0)
            {
                var ctor = sizeType.GetConstructor(new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
                object newSize = ctor.Invoke(new object[] { Enum.Parse(sizeKindType, "FixedResolution"), width, height, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var window = EditorWindow.GetWindow(gameViewType, false, null, false);
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            select.Invoke(window, new object[] { index, null });
            window.Repaint();
            return $"Game view set to {width}x{height} (index {index})";
        }
    }
}
