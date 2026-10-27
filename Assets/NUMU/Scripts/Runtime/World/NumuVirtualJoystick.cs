using UnityEngine;
using UnityEngine.EventSystems;

namespace Numu.World
{
    /// <summary>On-screen thumb stick for touch devices (also usable with a mouse in the Editor).</summary>
    public class NumuVirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform handle;
        public float radius = 90f;

        public Vector2 Value { get; private set; }

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, e.position, e.pressEventCamera, out var local)) return;
            Vector2 v = Vector2.ClampMagnitude(local / radius, 1f);
            Value = v;
            handle.anchoredPosition = v * radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            Value = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
        }
    }
}
