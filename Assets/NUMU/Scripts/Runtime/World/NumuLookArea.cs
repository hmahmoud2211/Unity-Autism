using UnityEngine;
using UnityEngine.EventSystems;

namespace Numu.World
{
    /// <summary>
    /// Invisible drag surface on the right of the screen: dragging a finger (or the mouse) here turns the camera.
    /// It sits behind every button, so taps on the HUD, prompts and the joystick are never stolen.
    /// </summary>
    public class NumuLookArea : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public NumuFollowCamera view;

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (view == null && Camera.main != null) view = Camera.main.GetComponent<NumuFollowCamera>();
            if (view != null) view.LookPixels(e.delta);
        }
    }
}
