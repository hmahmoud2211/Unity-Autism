using UnityEngine;
using UnityEngine.InputSystem;

namespace Numu.World
{
    /// <summary>
    /// The child avatar: camera-relative walking with keyboard, gamepad or the on-screen joystick, in third or
    /// first person. Movement is deliberately gentle (walk by default, jog only when pushed far) to keep play calm.
    /// In first person the body turns with the view and steps sideways instead of turning.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NumuPlayerController : MonoBehaviour
    {
        public Animator animator;
        public Transform cameraTransform;
        public NumuVirtualJoystick joystick;
        public float walkSpeed = 1.25f;
        public float runSpeed = 2.6f;
        public float turnSpeed = 540f;
        [Tooltip("Metres per second gained per second when starting to walk, and lost when stopping.")]
        public float acceleration = 6f;
        public float deceleration = 9f;
        public float gravity = -12f;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        CharacterController _cc;
        NumuFollowCamera _view;
        float _verticalVelocity;
        Vector3 _planarVelocity;
        float _animSpeed;

        /// <summary>When false the player stands still (used during conversations and questions).</summary>
        public bool InputEnabled { get; set; } = true;

        public float CurrentSpeed { get; private set; }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Start()
        {
            if (cameraTransform != null) _view = cameraTransform.GetComponent<NumuFollowCamera>();
        }

        Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (v.sqrMagnitude > 1f) v.Normalize();
                // Shift walks a bit faster on keyboards.
                if (kb.leftShiftKey.isPressed) v *= 1f; else v *= 0.6f;
            }
            var pad = Gamepad.current;
            if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > v.sqrMagnitude) v = pad.leftStick.ReadValue();
            if (joystick != null && joystick.Value.sqrMagnitude > v.sqrMagnitude) v = joystick.Value;
            return v;
        }

        void Update()
        {
            Vector2 input = InputEnabled ? ReadMove() : Vector2.zero;
            Vector3 fwd = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 move = fwd * input.y + right * input.x;
            float magnitude = Mathf.Clamp01(move.magnitude);
            // Up to 60% stick = walk; beyond that eases into a jog.
            float speed = magnitude <= 0.6f ? walkSpeed * (magnitude / 0.6f) : Mathf.Lerp(walkSpeed, runSpeed, (magnitude - 0.6f) / 0.4f);

            if (_view != null && _view.IsFirstPerson && InputEnabled)
            {
                // First person: the body always faces where the child looks.
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, _view.ViewYaw, 0f), turnSpeed * 2f * Time.deltaTime);
            }
            else if (magnitude > 0.05f)
            {
                Quaternion target = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            _verticalVelocity = _cc.isGrounded ? -1f : _verticalVelocity + gravity * Time.deltaTime;
            // Ease into and out of walking instead of starting and stopping instantly.
            Vector3 wanted = magnitude > 0.05f ? move.normalized * speed : Vector3.zero;
            float rate = wanted.sqrMagnitude >= _planarVelocity.sqrMagnitude ? acceleration : deceleration;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, wanted, rate * Time.deltaTime);
            _cc.Move((_planarVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
            CurrentSpeed = _planarVelocity.magnitude;

            _animSpeed = Mathf.MoveTowards(_animSpeed, CurrentSpeed, Time.deltaTime * 4f);
            if (animator != null) animator.SetFloat(SpeedId, _animSpeed);
        }

        /// <summary>Turns smoothly to face a world point (used when a conversation starts).</summary>
        public void Face(Vector3 point)
        {
            Vector3 d = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
