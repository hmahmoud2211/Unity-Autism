using System;
using UnityEngine;

namespace Numu.World
{
    /// <summary>Something the child can walk up to and interact with ("Say hi to Mia").</summary>
    public class NumuInteractable : MonoBehaviour
    {
        public string prompt = "Say hi";
        public float radius = 2.6f;
        public Transform focusPoint;
        public bool interactable = true;

        public event Action<NumuInteractable> Interacted;

        public Vector3 FocusPosition => focusPoint != null ? focusPoint.position : transform.position;

        public void Interact() => Interacted?.Invoke(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
