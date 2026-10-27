using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Numu.World
{
    /// <summary>
    /// Ground-level worlds need much tighter shadows than the far-away island map. This applies per-world
    /// shadow settings to the active URP asset while the scene is loaded and restores them afterwards.
    /// Values differ for the Mobile and PC quality tiers.
    /// </summary>
    [ExecuteAlways]
    public class NumuWorldRenderSettings : MonoBehaviour
    {
        public float pcShadowDistance = 60f;
        public float mobileShadowDistance = 35f;
        public int pcCascades = 3;
        public int mobileCascades = 2;
        public int mobileShadowResolution = 1024;

        UniversalRenderPipelineAsset _asset;
        float _savedDistance;
        int _savedCascades;
        int _savedResolution;
        bool _applied;

        void OnEnable()
        {
            _asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_asset == null) return;
            _savedDistance = _asset.shadowDistance;
            _savedCascades = _asset.shadowCascadeCount;
            _savedResolution = _asset.mainLightShadowmapResolution;
            bool mobile = Application.isMobilePlatform || _asset.name.Contains("Mobile") || QualitySettings.names[QualitySettings.GetQualityLevel()].Contains("Mobile");
            _asset.shadowDistance = mobile ? mobileShadowDistance : pcShadowDistance;
            _asset.shadowCascadeCount = mobile ? mobileCascades : pcCascades;
            if (mobile) _asset.mainLightShadowmapResolution = mobileShadowResolution;
            _applied = true;
        }

        void OnDisable()
        {
            if (!_applied || _asset == null) return;
            _asset.shadowDistance = _savedDistance;
            _asset.shadowCascadeCount = _savedCascades;
            _asset.mainLightShadowmapResolution = _savedResolution;
            _applied = false;
        }
    }
}
