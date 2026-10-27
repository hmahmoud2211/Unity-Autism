using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>One of the four learning islands (or the hub). Holds its focus pose and level markers.</summary>
    public class NumuIsland : MonoBehaviour
    {
        public string islandId;
        public string displayName;
        public string subtitle;
        public Color themeColor = Color.white;
        public Color themeDark = Color.gray;
        public bool hasLevels = true;
        public NumuCameraRig.Pose focusPose;
        public Transform focusPoint;
        public List<NumuLevelMarker> markers = new List<NumuLevelMarker>();
    }
}
