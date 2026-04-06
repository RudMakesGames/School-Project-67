using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Cinemachine;

namespace VS
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class MultiPersonController : MonoBehaviour
    {   
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("Single shared camera root / follow target used by BOTH TP and FP cameras.")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up (TP mode)")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down (TP mode)")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degrees to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        [Header("View Mode (TP/FP)")]
        [Tooltip("If enabled, movement uses FirstPersonMotor-style body-relative movement (WASD relative to character forward/right).")]
        [SerializeField] private bool useFirstPersonMovement = false;

        [Tooltip("Optional: toggles the view mode when pressing the key below (uses virtual camera priorities).")]
        [SerializeField] private bool allowToggleViewMode = true;

        [Tooltip("Key used to toggle between TP/FP modes.")]
        [SerializeField] private KeyCode toggleViewModeKey = KeyCode.V;

        [Tooltip("Third-person Cinemachine Virtual Camera.")]
        [SerializeField] private CinemachineCamera thirdPersonCamera;

        [Tooltip("First-person Cinemachine Virtual Camera.")]
        [SerializeField] private CinemachineCamera firstPersonCamera;

        [Tooltip("Priority used by the active camera (inactive is active-1).")]
        [SerializeField] private int activeCameraPriority = 20;

        [Tooltip("If enabled, both vcams are bound to `CinemachineCameraTarget` for Follow and LookAt.")]
        [SerializeField] private bool autoBindCameraTargets = true;

        [Header("Camera Optimization")]
        [Tooltip("If enabled, disables the inactive vcam GameObject immediately when switching view mode.")]
        [SerializeField] private bool disableInactiveCameraImmediately = true;

        [Header("First Person Look")]
        [Tooltip("Clamp for FP pitch (look up/down).")]
        [SerializeField] private float firstPersonPitchTopClamp = 80.0f;

        [Tooltip("Clamp for FP pitch (look up/down).")]
        [SerializeField] private float firstPersonPitchBottomClamp = -80.0f;

        [Tooltip("If enabled, FP yaw rotates the player (body). If disabled, yaw is applied to the camera target.")]
        [SerializeField] private bool firstPersonRotateBodyYaw = true;

        [Header("First Person Body Culling (Main Camera)")]
        [Tooltip("Layer(s) that represent the player body. These layers will be hidden from the MAIN CAMERA while in FP mode.")]
        [SerializeField] private LayerMask playerBodyLayers = 0;

        [Tooltip("Delay (seconds) before applying the culling mask when ENTERING FP mode. Exiting FP is immediate.")]
        [Min(0f)]
        [SerializeField] private float enterFirstPersonCullingDelay = 0.1f;

        [Header("Camera Switch Cooldown")]
        [Tooltip("Cooldown (seconds) after pressing the toggle key before switching is allowed again.")]
        [Min(0f)]
        [SerializeField] private float viewToggleCooldown = 0.25f;

        // cached main camera mask
        private bool _cachedMainCameraMask;
        private int _mainCameraBaseMask;

        // pending apply for entering FP mode
        private float _applyFpCullingAtTime = -1f;

        // cooldown / anti-spam
        private float _nextAllowedToggleTime = 0f;

        // TP look state
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // FP look state
        private float _firstPersonYaw;
        private float _firstPersonPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private VSInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;
        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Start()
        {
            CacheMainCameraMask();

            if (CinemachineCameraTarget != null)
            {
                var euler = CinemachineCameraTarget.transform.rotation.eulerAngles;

                _cinemachineTargetYaw = euler.y;
                _cinemachineTargetPitch = euler.x;

                _firstPersonYaw = euler.y;
                _firstPersonPitch = euler.x;
                if (_firstPersonPitch > 180f) _firstPersonPitch -= 360f;
            }
            else
            {
                _cinemachineTargetYaw = transform.eulerAngles.y;
                _cinemachineTargetPitch = 0f;
                _firstPersonYaw = transform.eulerAngles.y;
                _firstPersonPitch = 0f;
            }

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<VSInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            EnsureCameraBindings();

            ApplyViewMode(useFirstPersonMovement);
            ApplyCameraGameObjectState(useFirstPersonMovement);

            // Apply initial mask state (no delay on startup).
            ApplyMainCameraCullingImmediate(useFirstPersonMovement);
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            HandleViewModeToggle();
            TickPendingFirstPersonCullingApply();

            // Important: update grounded state before processing jump/gravity so
            // we don't clear jump input the same frame the character becomes grounded.
            GroundedCheck();
            JumpAndGravity();
            Move();
        }

        private bool IsToggleOnCooldown()
        {
            return viewToggleCooldown > 0f && Time.time < _nextAllowedToggleTime;
        }

        private void StartToggleCooldown()
        {
            if (viewToggleCooldown <= 0f)
                return;

            _nextAllowedToggleTime = Time.time + viewToggleCooldown;
        }

        private void TickPendingFirstPersonCullingApply()
        {
            if (_applyFpCullingAtTime < 0f)
                return;

            if (Time.time < _applyFpCullingAtTime)
                return;

            _applyFpCullingAtTime = -1f;

            // Only apply if we are still in FP at the time the delay completes.
            if (useFirstPersonMovement)
                ApplyMainCameraCullingImmediate(firstPerson: true);
        }

        private void LateUpdate()
        {
            EnsureCameraBindings();

            if (useFirstPersonMovement)
                FirstPersonCameraRotation();
            else
                ThirdPersonCameraRotation();
        }

        private void CacheMainCameraMask()
        {
            if (_cachedMainCameraMask)
                return;

            var cam = GetMainCameraComponent();
            if (cam == null)
                return;

            _mainCameraBaseMask = cam.cullingMask;
            _cachedMainCameraMask = true;
        }

        private Camera GetMainCameraComponent()
        {
            if (_mainCamera == null)
                return null;

            if (_mainCamera.TryGetComponent<Camera>(out var cam))
                return cam;

            return _mainCamera.GetComponentInChildren<Camera>(true);
        }

        private void ApplyMainCameraCullingImmediate(bool firstPerson)
        {
            CacheMainCameraMask();

            var cam = GetMainCameraComponent();
            if (cam == null || !_cachedMainCameraMask)
                return;

            cam.cullingMask = firstPerson
                ? (_mainCameraBaseMask & ~playerBodyLayers.value)
                : _mainCameraBaseMask;
        }

        private void ScheduleMainCameraCullingForEnterFirstPerson()
        {
            if (enterFirstPersonCullingDelay <= 0f)
            {
                ApplyMainCameraCullingImmediate(firstPerson: true);
                return;
            }

            _applyFpCullingAtTime = Time.time + enterFirstPersonCullingDelay;
        }

        private void ApplyCameraGameObjectState(bool firstPerson)
        {
            if (!disableInactiveCameraImmediately)
                return;

            if (thirdPersonCamera != null)
                thirdPersonCamera.gameObject.SetActive(!firstPerson);

            if (firstPersonCamera != null)
                firstPersonCamera.gameObject.SetActive(firstPerson);
        }

        private void EnsureCameraBindings()
        {
            if (!autoBindCameraTargets)
                return;

            if (CinemachineCameraTarget == null)
                return;

            var t = CinemachineCameraTarget.transform;

            if (thirdPersonCamera != null)
            {
                if (thirdPersonCamera.Follow == null) thirdPersonCamera.Follow = t;
                if (thirdPersonCamera.LookAt == null) thirdPersonCamera.LookAt = t;
            }

            if (firstPersonCamera != null)
            {
                if (firstPersonCamera.Follow == null) firstPersonCamera.Follow = t;
                if (firstPersonCamera.LookAt == null) firstPersonCamera.LookAt = t;
            }
        }

        private void HandleViewModeToggle()
        {
            if (!allowToggleViewMode)
                return;

            if (IsToggleOnCooldown())
                return;

            if (Input.GetKeyDown(toggleViewModeKey))
            {
                StartToggleCooldown();

                useFirstPersonMovement = !useFirstPersonMovement;
                ApplyViewMode(useFirstPersonMovement);
                ApplyCameraGameObjectState(useFirstPersonMovement);

                if (useFirstPersonMovement)
                {
                    // Enter FP: delay hiding the body (kept as-is).
                    ScheduleMainCameraCullingForEnterFirstPerson();
                }
                else
                {
                    // Exit FP: cancel any pending apply and restore immediately.
                    _applyFpCullingAtTime = -1f;
                    ApplyMainCameraCullingImmediate(firstPerson: false);
                }
            }
        }

        private void ApplyViewMode(bool firstPerson)
        {
            if (thirdPersonCamera != null && firstPersonCamera != null)
            {
                var active = activeCameraPriority;
                var inactive = activeCameraPriority - 1;

                thirdPersonCamera.Priority = firstPerson ? inactive : active;
                firstPersonCamera.Priority = firstPerson ? active : inactive;
            }

            if (CinemachineCameraTarget == null)
                return;

            var euler = CinemachineCameraTarget.transform.rotation.eulerAngles;

            _cinemachineTargetYaw = euler.y;
            _cinemachineTargetPitch = euler.x;

            _firstPersonYaw = euler.y;
            _firstPersonPitch = euler.x;
            if (_firstPersonPitch > 180f) _firstPersonPitch -= 360f;
            _firstPersonPitch = ClampAngle(_firstPersonPitch, firstPersonPitchBottomClamp, firstPersonPitchTopClamp);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (_hasAnimator)
                _animator.SetBool(_animIDGrounded, Grounded);
        }

        private void ThirdPersonCameraRotation()
        {
            if (CinemachineCameraTarget == null)
                return;

            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw,
                0.0f
            );
        }

        private void FirstPersonCameraRotation()
        {
            if (CinemachineCameraTarget == null)
                return;

            if (_input.look.sqrMagnitude < _threshold || LockCameraPosition)
                return;

            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _firstPersonYaw += _input.look.x * deltaTimeMultiplier;
            _firstPersonPitch += _input.look.y * deltaTimeMultiplier;

            _firstPersonYaw = ClampAngle(_firstPersonYaw, float.MinValue, float.MaxValue);
            _firstPersonPitch = ClampAngle(_firstPersonPitch, firstPersonPitchBottomClamp, firstPersonPitchTopClamp);

            if (firstPersonRotateBodyYaw)
            {
                transform.rotation = Quaternion.Euler(0.0f, _firstPersonYaw, 0.0f);
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_firstPersonPitch + CameraAngleOverride, 0.0f, 0.0f);
            }
            else
            {
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                    _firstPersonPitch + CameraAngleOverride,
                    _firstPersonYaw,
                    0.0f
                );
            }
        }

        private void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;
            Vector3 moveDirectionWorld;

            if (useFirstPersonMovement)
            {
                moveDirectionWorld = transform.right * _input.move.x + transform.forward * _input.move.y;
                _targetRotation = transform.eulerAngles.y;
            }
            else
            {
                if (_input.move != Vector2.zero)
                {
                    var cameraYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;

                    _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw;
                    float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }

                moveDirectionWorld = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            }

            _controller.Move(
                moveDirectionWorld.normalized * (_speed * Time.deltaTime) +
                new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime
            );

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                    _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator) _animator.SetBool(_animIDJump, true);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                    _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                    _fallTimeoutDelta -= Time.deltaTime;
                else if (_hasAnimator)
                    _animator.SetBool(_animIDFreeFall, true);

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Gravity * Time.deltaTime;
        }

        /// <summary>
        /// External systems (jump pads, trampolines, explosions) can call this to set controller vertical velocity.
        /// Velocity is applied directly to the internal vertical velocity used by CharacterController movement.
        /// </summary>
        /// <param name="velocity">World-space velocity to apply (Y component is what matters for upward launch).</param>
        public void Launch(Vector3 velocity)
        {
            // Apply only Y component for jump impulse (controller uses _verticalVelocity for vertical movement).
            _verticalVelocity = velocity.y;

            // Consider the player airborne.
            Grounded = false;

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, true);
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = Grounded ? transparentGreen : transparentRed;

            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius
            );
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(
                        FootstepAudioClips[index],
                        transform.TransformPoint(_controller.center),
                        FootstepAudioVolume
                    );
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(
                    LandingAudioClip,
                    transform.TransformPoint(_controller.center),
                    FootstepAudioVolume
                );
            }
        }
    }
}