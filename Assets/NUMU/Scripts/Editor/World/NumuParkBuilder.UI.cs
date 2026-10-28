using UnityEngine;
using Numu.World;

namespace Numu.EditorTools.World
{
    public static partial class NumuParkBuilder
    {
        static void BuildUI(GameplayRefs refs)
        {
            var ui = NumuWorldUIBuilder.Build(_root, new NumuWorldUIBuilder.Spec
            {
                canvasName = "Park UI",
                title = "NUMU Park",
                subtitle = "Identify emotions",
                chipIcon = NumuTextureKit.Sprite("Icon_Park"),
                chipBg = NumuColor.Hex("#fde3ef"),
                chipInk = NumuColor.Hex("#a8186a"),
                accent = NumuColor.Hex("#e0589c"),
                prompt = "Say hi to Lina",
                speaker = "Lina",
                question = "How is your friend feeling?",
            }, refs.interactions, refs.player);
            refs.scenario.ui = ui;
            _log.AppendLine("UI: HUD (Map + world chip), interaction prompt, touch joystick, subtitles, emotion question (4 faces), feedback, toast.");
        }
    }
}
