using UnityEngine;
using UnityEngine.EventSystems;

namespace Numu
{
    /// <summary>
    /// Makes a 3D collider tappable through the EventSystem (works for mouse and touch alike, and is
    /// automatically blocked by UI panels). Level markers open their level; everything else focuses its island.
    /// </summary>
    public class NumuClickable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.08f;

        Vector3 _baseScale;
        bool _hover;
        NumuLevelMarker _marker;
        NumuIslandLink _link;

        void Awake()
        {
            _baseScale = transform.localScale;
            _marker = GetComponent<NumuLevelMarker>();
            _link = GetComponent<NumuIslandLink>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var map = NumuMapController.Instance;
            if (map == null) return;
            if (_marker != null) map.OpenLevel(_marker.islandId, _marker.level);
            else if (_link != null) map.FocusIsland(_link.islandId);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        void Update()
        {
            if (_marker == null) return;
            float target = _hover && !NumuSettings.ReducedMotion ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * target, Time.unscaledDeltaTime * 12f);
        }
    }
}
