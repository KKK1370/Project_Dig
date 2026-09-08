using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class FlyCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;
    [SerializeField, Min(1f)] private float fastMultiplier = 3f;

    [Header("Mouse Look")]
    [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -89f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 89f;

    private float yaw;
    private float pitch;
    private bool ownsCursor;

    private void Awake()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        yaw = eulerAngles.y;
        pitch = NormalizeAngle(eulerAngles.x);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        UpdateWithInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
        UpdateWithLegacyInput();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void UpdateWithInputSystem()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (mouse.rightButton.wasReleasedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (!mouse.rightButton.isPressed)
        {
            return;
        }

        Rotate(mouse.delta.ReadValue());

        Vector3 input = new Vector3(
            ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
            ReadAxis(keyboard.eKey.isPressed, keyboard.qKey.isPressed),
            ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));

        bool fast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        Move(input, fast);
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
    private void UpdateWithLegacyInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            LockCursor();
        }

        if (Input.GetMouseButtonUp(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        if (!Input.GetMouseButton(1))
        {
            return;
        }

        Vector2 lookDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Rotate(lookDelta * 10f);

        Vector3 input = new Vector3(
            ReadAxis(Input.GetKey(KeyCode.D), Input.GetKey(KeyCode.A)),
            ReadAxis(Input.GetKey(KeyCode.E), Input.GetKey(KeyCode.Q)),
            ReadAxis(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.S)));

        bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Move(input, fast);
    }
#endif

    private void Rotate(Vector2 mouseDelta)
    {
        yaw += mouseDelta.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * lookSensitivity, minimumPitch, maximumPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Move(Vector3 input, bool fast)
    {
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 direction =
            transform.right * input.x +
            Vector3.up * input.y +
            transform.forward * input.z;

        float speed = moveSpeed * (fast ? fastMultiplier : 1f);
        transform.position += direction * speed * Time.deltaTime;
    }

    private static float ReadAxis(bool positive, bool negative)
    {
        return (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }

    private void LockCursor()
    {
        ownsCursor = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        if (!ownsCursor)
        {
            return;
        }

        ownsCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            UnlockCursor();
        }
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
