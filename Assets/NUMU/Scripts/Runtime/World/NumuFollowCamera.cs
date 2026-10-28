using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Numu.World
{
    /// <summary>
    /// The player's camera: one shared child with two views, third person (default; orbiting, collision-aware)
    /// and first person (at the child's eye height), plus framed "shots" for conversations. Built on Cinemachine:
    /// this component sits on the Main Camera, creates its virtual cameras at runtime and lets the brain blend
    /// between them. Look with a mouse/touch drag, the right mouse button or the right stick; zoom with the wheel;
    /// switch views with V, the gamepad's north button or the on-screen view button.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
    public class NumuFollowCamera : MonoBehaviour
    {
        public enum View { ThirdPerson, FirstPerson }

        [Header("Third person")]
        public Transform target;
        [Tooltip("Orbit radius in metres.")]
        public float distance = 4.8f;
        [Tooltip("Camera height above the target at the default pitch.")]
        public float height = 2.3f;
        [Tooltip("Height of the point the camera orbits and looks at.")]
        public float lookHeight = 1.0f;
        public float minDistance = 1.8f;
        public float maxDistance = 6.5f;
        [Tooltip("Layers that block the camera (walls). Furniture and people sit on Ignore Raycast so they don't.")]
        public LayerMask obstacleMask = ~0;
        public Vector2 thirdPersonPitch = new Vector2(-5f, 60f);

        [Header("First person")]
        public Vector2 firstPersonPitch = new Vector2(-60f, 70f);
        [Tooltip("Eye position ahead of the head bone, so the near plane never cuts through the face.")]
        public float eyeForward = 0.12f;

        [Header("Look")]
        public float degreesPerPixel = 0.16f;
        public float stickDegreesPerSecond = 120f;
        public float blendSeconds = 0.6f;

        public View CurrentView { get; private set; }
        public bool IsFirstPerson => CurrentView == View.FirstPerson && !_hasShot;
        /// <summary>Yaw the first-person camera is looking along (the body follows it).</summary>
        public float ViewYaw => _panTilt != null ? _panTilt.PanAxis.Value : transform.eulerAngles.y;
        public event Action<View> ViewChanged;

        CinemachineBrain _brain;
        CinemachineCamera _third, _first, _shot;
        CinemachineOrbitalFollow _orbit;
        CinemachinePanTilt _panTilt;
        Transform _orbitTarget, _eye;
        NumuPlayerController _player;
        Renderer[] _body;
        ShadowCastingMode[] _bodyShadows;
        bool _bodyHidden, _hasShot;
        float _eyeHeight = 1.2f, _hideBodyAt, _lastManualLook = -10f;

        void Awake()
        {
            _brain = GetComponent<CinemachineBrain>();
            if (_brain == null) _brain = gameObject.AddComponent<CinemachineBrain>();
            if (target == null) return;
            _player = target.GetComponent<NumuPlayerController>();
            // The camera's collision test ignores the child's own capsule.
            if (target.CompareTag("Untagged")) target.tag = "Player";
            var anim = target.GetComponent<Animator>();
            var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null) _eyeHeight = Mathf.Clamp(head.position.y - target.position.y + 0.08f, 0.8f, 1.8f);
            _body = target.GetComponentsInChildren<Renderer>();
            _bodyShadows = Array.ConvertAll(_body, r => r.shadowCastingMode);

            var cam = GetComponent<Camera>();
            var rig = new GameObject("CameraRig").transform;
            _orbitTarget = new GameObject("OrbitTarget").transform;
            _orbitTarget.SetParent(rig, false);
            _eye = new GameObject("EyePoint").transform;
            _eye.SetParent(rig, false);
            var lens = LensSettings.Default;
            lens.FieldOfView = cam.fieldOfView;
            lens.NearClipPlane = cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;

            _third = NewCamera("CM_ThirdPerson", rig, lens);
            _third.Follow = _orbitTarget;
            _third.LookAt = _orbitTarget;
            _orbit = _third.gameObject.AddComponent<CinemachineOrbitalFollow>();
            _orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            _orbit.Radius = distance;
            _orbit.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            _orbit.TrackerSettings.PositionDamping = Vector3.one * 0.15f;
            _orbit.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
            _orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            _orbit.HorizontalAxis.Wrap = true;
            _orbit.HorizontalAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 0.5f, Time = 1.5f };
            _orbit.VerticalAxis.Range = thirdPersonPitch;
            _orbit.VerticalAxis.Value = Mathf.Clamp(Mathf.Atan2(height - lookHeight, distance) * Mathf.Rad2Deg, thirdPersonPitch.x, thirdPersonPitch.y);
            _orbit.RadialAxis.Range = new Vector2(1f, 1f);
            _orbit.RadialAxis.Value = 1f;
            var composer = _third.gameObject.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.2f, 0.2f);
            var deoccluder = _third.gameObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = obstacleMask;
            deoccluder.IgnoreTag = "Player";
            // Small minimums: with the child pressed against a wall there may be only ~0.4 m of room.
            deoccluder.MinimumDistanceFromTarget = 0.1f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.12f;
            deoccluder.AvoidObstacles.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            deoccluder.AvoidObstacles.SmoothingTime = 0.2f;
            deoccluder.AvoidObstacles.Damping = 0.3f;
            deoccluder.AvoidObstacles.DampingWhenOccluded = 0.05f;
            deoccluder.ShotQualityEvaluation.Enabled = false;

            var fpLens = lens;
            fpLens.NearClipPlane = 0.05f;
            _first = NewCamera("CM_FirstPerson", rig, fpLens);
            _first.Follow = _eye;
            _first.gameObject.AddComponent<CinemachineHardLockToTarget>();
            _panTilt = _first.gameObject.AddComponent<CinemachinePanTilt>();
            _panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;
            _panTilt.PanAxis.Range = new Vector2(-180f, 180f);
            _panTilt.PanAxis.Wrap = true;
            _panTilt.TiltAxis.Range = firstPersonPitch;

            _shot = NewCamera("CM_Shot", rig, lens);

            CurrentView = NumuSettings.PreferFirstPerson ? View.FirstPerson : View.ThirdPerson;
            AlignToTarget();
            UpdateTargets();
            ApplyPriorities();
        }

        static CinemachineCamera NewCamera(string name, Transform parent, LensSettings lens)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CinemachineCamera>();
            c.Lens = lens;
            return c;
        }

        void Start() => SnapToTarget();

        void ApplyPriorities()
        {
            if (_third == null) return;
            SetPriority(_third, CurrentView == View.ThirdPerson ? 20 : 10);
            SetPriority(_first, CurrentView == View.FirstPerson ? 20 : 10);
            SetPriority(_shot, _hasShot ? 30 : 0);
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, NumuSettings.ReducedMotion ? 0f : blendSeconds);
        }

        static void SetPriority(CinemachineCamera c, int value)
        {
            var p = c.Priority;
            p.Enabled = true;
            p.Value = value;
            c.Priority = p;
        }

        // ------------------------------------------------------------------ views

        public void SetView(View view)
        {
            if (_third == null || view == CurrentView) return;
            float yaw = transform.eulerAngles.y;
            if (view == View.FirstPerson)
            {
                // Look the way the third-person camera was looking, level with the horizon.
                _panTilt.PanAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
                _panTilt.TiltAxis.Value = 5f;
                _hideBodyAt = Time.time + (NumuSettings.ReducedMotion ? 0f : blendSeconds * 0.5f);
            }
            else _orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            CurrentView = view;
            NumuSettings.PreferFirstPerson = view == View.FirstPerson;
            ApplyPriorities();
            ViewChanged?.Invoke(view);
        }

        public void ToggleView() => SetView(CurrentView == View.FirstPerson ? View.ThirdPerson : View.FirstPerson);

        /// <summary>Rotates the active view by a look delta in degrees (x = yaw right, y = pitch up).</summary>
        public void Look(Vector2 degrees)
        {
            if (_third == null || _hasShot || (_player != null && !_player.InputEnabled)) return;
            degrees *= NumuSettings.LookSensitivity;
            if (CurrentView == View.FirstPerson)
            {
                _panTilt.PanAxis.Value = Mathf.Repeat(_panTilt.PanAxis.Value + degrees.x + 180f, 360f) - 180f;
                _panTilt.TiltAxis.Value = Mathf.Clamp(_panTilt.TiltAxis.Value - degrees.y, firstPersonPitch.x, firstPersonPitch.y);
            }
            else
            {
                _orbit.HorizontalAxis.Value = Mathf.Repeat(_orbit.HorizontalAxis.Value + degrees.x + 180f, 360f) - 180f;
                _orbit.VerticalAxis.Value = Mathf.Clamp(_orbit.VerticalAxis.Value - degrees.y, thirdPersonPitch.x, thirdPersonPitch.y);
            }
            _lastManualLook = Time.time;
        }

        /// <summary>Look from a pointer drag in screen pixels (resolution independent).</summary>
        public void LookPixels(Vector2 pixels) => Look(pixels * degreesPerPixel * (1080f / Mathf.Max(1, Screen.height)));

        /// <summary>Moves the third-person camera closer (positive) or further away.</summary>
        public void Zoom(float metres)
        {
            if (_orbit == null || _hasShot) return;
            _orbit.Radius = Mathf.Clamp(_orbit.Radius - metres, minDistance, maxDistance);
        }

        // ------------------------------------------------------------------ conversation shots

        /// <summary>Moves the camera to a framed shot (e.g. a close-up on a face). The chosen view resumes afterwards.</summary>
        public void SetShot(Vector3 position, Vector3 lookAt)
        {
            if (_shot == null) return;
            _shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            _hasShot = true;
            ApplyPriorities();
        }

        public void ClearShot()
        {
            _hasShot = false;
            ApplyPriorities();
        }

        /// <summary>Jumps to the current view without blending.</summary>
        public void SnapToTarget()
        {
            if (_third == null) return;
            UpdateTargets();
            _third.PreviousStateIsValid = false;
            _first.PreviousStateIsValid = false;
            _shot.PreviousStateIsValid = false;
        }

        /// <summary>Re-aims behind the target's current facing and jumps there (after a teleport or respawn).</summary>
        public void SnapBehindTarget()
        {
            AlignToTarget();
            SnapToTarget();
        }

        void AlignToTarget()
        {
            if (target == null || _orbit == null) return;
            float yaw = Mathf.Repeat(target.eulerAngles.y + 180f, 360f) - 180f;
            _orbit.HorizontalAxis.Value = yaw;
            _panTilt.PanAxis.Value = yaw;
            _panTilt.TiltAxis.Value = 5f;
        }

        // ------------------------------------------------------------------ per frame

        void UpdateTargets()
        {
            float yaw = target.eulerAngles.y;
            _orbitTarget.SetPositionAndRotation(target.position + Vector3.up * lookHeight, Quaternion.Euler(0f, yaw, 0f));
            // A steady eye height (no head bob) slightly ahead of the face.
            _eye.position = target.position + Vector3.up * _eyeHeight + Quaternion.Euler(0f, ViewYaw, 0f) * Vector3.forward * eyeForward;
        }

        void LateUpdate()
        {
            if (target == null || _third == null) return;
            ReadInput();
            UpdateTargets();
            // Third person drifts back behind a walking child unless the player has just looked around.
            bool walking = _player != null && _player.CurrentSpeed > 0.2f;
            var rc = _orbit.HorizontalAxis.Recentering;
            rc.Enabled = CurrentView == View.ThirdPerson && walking && Time.time - _lastManualLook > 2.5f;
            _orbit.HorizontalAxis.Recentering = rc;
            _orbit.TrackerSettings.PositionDamping = Vector3.one * (NumuSettings.ReducedMotion ? 0f : 0.15f);
            // In first person the child's own body is hidden (it still casts its shadow), so nothing clips the view.
            // The same applies when a wall has pushed the third-person camera right up to the child's head.
            bool crowded = !_hasShot && CurrentView == View.ThirdPerson && Vector3.Distance(transform.position, _orbitTarget.position) < 0.7f;
            SetBodyHidden((IsFirstPerson && Time.time >= _hideBodyAt) || crowded);
        }

        void ReadInput()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool canAct = _player == null || _player.InputEnabled;
            if (canAct && ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))) ToggleView();
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed) LookPixels(mouse.delta.ReadValue());
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(Mathf.Sign(scroll) * 0.35f);
            }
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) Look(stick * stickDegreesPerSecond * Time.deltaTime);
            }
        }

        void SetBodyHidden(bool hidden)
        {
            if (hidden == _bodyHidden || _body == null) return;
            _bodyHidden = hidden;
            for (int i = 0; i < _body.Length; i++)
                if (_body[i] != null) _body[i].shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : _bodyShadows[i];
        }
    }
}
