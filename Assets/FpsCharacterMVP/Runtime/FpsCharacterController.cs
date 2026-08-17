using UnityEngine;
using UnityEngine.InputSystem;

namespace BornToDig.CharacterMVP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsCharacterController : MonoBehaviour
    {
        [Header("Existing camera")]
        [SerializeField] private Camera playerCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.1f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.05f;
        [SerializeField] private float gravity = -24f;

        [Header("Look")]
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.115f;
        [SerializeField, Min(1f)] private float gamepadLookSpeed = 145f;
        [SerializeField] private float minPitch = -82f;
        [SerializeField] private float maxPitch = 82f;

        private CharacterController characterController;
        private Transform cameraTransform;
        private float verticalVelocity;
        private float pitch;
        private bool inputCaptured;

        public bool InputCaptured => inputCaptured;
        public Camera PlayerCamera => playerCamera;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
                pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
            }
        }

        private void Start()
        {
            CaptureInput();
        }

        private void OnDisable()
        {
            ReleaseInput();
        }

        private void Update()
        {
            HandleCursorState();

            if (inputCaptured)
            {
                HandleLook();
                HandleMovement();
            }

        }

        public void Configure(Camera cameraReference)
        {
            playerCamera = cameraReference;
            cameraTransform = cameraReference != null ? cameraReference.transform : null;

            if (cameraTransform != null)
            {
                pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
            }
        }

        private void HandleCursorState()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseInput();
            }
            else if (!inputCaptured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureInput();
            }
        }

        private void CaptureInput()
        {
            inputCaptured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ReleaseInput()
        {
            inputCaptured = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleLook()
        {
            Vector2 lookDelta = Vector2.zero;

            if (Mouse.current != null)
            {
                lookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
            }

            if (Gamepad.current != null)
            {
                lookDelta += Gamepad.current.rightStick.ReadValue() * gamepadLookSpeed * Time.unscaledDeltaTime;
            }

            transform.Rotate(Vector3.up, lookDelta.x, Space.Self);
            pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            Vector2 moveInput = ReadMoveInput();
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            move = Vector3.ClampMagnitude(move, 1f);

            bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            if (Gamepad.current != null) sprinting |= Gamepad.current.leftStickButton.isPressed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (Gamepad.current != null) jumpPressed |= Gamepad.current.buttonSouth.wasPressedThisFrame;

            if (characterController.isGrounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            float speed = walkSpeed * (sprinting ? sprintMultiplier : 1f);
            characterController.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void OnValidate()
        {
            gravity = Mathf.Min(-0.01f, gravity);
            minPitch = Mathf.Clamp(minPitch, -89f, 0f);
            maxPitch = Mathf.Clamp(maxPitch, 0f, 89f);
        }
    }
}
