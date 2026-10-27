using UnityEngine;

namespace Numu
{
    /// <summary>
    /// Pushes the stylised lighting globals used by the NUMU shaders (hemisphere ambient,
    /// back-of-world haze and the motion scale honoured by wind, water and waterfalls).
    /// </summary>
    [ExecuteAlways]
    public class NumuLightingRig : MonoBehaviour
    {
        public Color skyAmbient = new Color(1.0f, 0.98f, 1.0f);
        public Color groundAmbient = new Color(0.88f, 0.82f, 0.80f);
        public Color hazeColor = new Color(0.96f, 0.88f, 0.87f);
        [Tooltip("World Z where the haze starts / reaches its maximum.")]
        public float hazeStartZ = 40f;
        public float hazeEndZ = 95f;
        [Range(0f, 1f)] public float hazeAmount = 0.85f;
        [Tooltip("Distance from the world centre line (|x|) where side haze starts / peaks.")]
        public float sideHazeStart = 44f;
        public float sideHazeEnd = 70f;
        [Range(0f, 1f)] public float sideHazeAmount = 0.7f;
        [Range(0f, 1f)] public float motion = 1f;
        [Range(0f, 1f)] public float reducedMotionScale = 0.12f;

        static readonly int SkyAmbientId = Shader.PropertyToID("_NumuSkyAmbient");
        static readonly int GroundAmbientId = Shader.PropertyToID("_NumuGroundAmbient");
        static readonly int HazeColorId = Shader.PropertyToID("_NumuHazeColor");
        static readonly int HazeParamsId = Shader.PropertyToID("_NumuHazeParams");
        static readonly int HazeSideId = Shader.PropertyToID("_NumuHazeSide");
        static readonly int MotionId = Shader.PropertyToID("_NumuMotion");

        void OnEnable()
        {
            NumuSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => NumuSettings.Changed -= Apply;

        void OnValidate() => Apply();

        void Update()
        {
            if (!Application.isPlaying) Apply();
        }

        public void Apply()
        {
            Shader.SetGlobalVector(SkyAmbientId, skyAmbient.linear);
            Shader.SetGlobalVector(GroundAmbientId, groundAmbient.linear);
            Shader.SetGlobalVector(HazeColorId, hazeColor.linear);
            Shader.SetGlobalVector(HazeParamsId, new Vector4(hazeStartZ, hazeEndZ, hazeAmount, 0f));
            Shader.SetGlobalVector(HazeSideId, new Vector4(sideHazeStart, sideHazeEnd, sideHazeAmount, 0f));
            Shader.SetGlobalFloat(MotionId, NumuSettings.ReducedMotion ? reducedMotionScale : motion);
        }
    }
}
