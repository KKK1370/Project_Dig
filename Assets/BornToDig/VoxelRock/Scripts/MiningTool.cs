using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BornToDig.VoxelMining
{
    [DisallowMultipleComponent]
    public sealed class MiningTool : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float miningDistance = 4f;
        [SerializeField] private LayerMask mineableLayers = ~0;
        [SerializeField] private bool rayFromScreenCenter = true;

        [Header("Mining")]
        [SerializeField, Range(0.02f, 1f)] private float miningRadius = 0.2f;
        [SerializeField, Range(0.01f, 2f)] private float miningStrength = 0.75f;
        [SerializeField, Min(0.1f)] private float miningInterval = 0.48f;

        [Header("Feedback")]
        [SerializeField] private bool showCrosshair = true;
        [Tooltip("Prevents the click used to recapture an FPS cursor from mining.")]
        [SerializeField] private bool requirePreviouslyLockedCursor;

        private GUIStyle crosshairStyle;
        private bool cursorWasLocked;
        private float nextMiningTime;

        public float MiningInterval => miningInterval;

        public void Configure(
            Camera cameraToUse,
            float distance = 4f,
            float radius = 0.2f,
            float strength = 0.75f,
            bool displayCrosshair = true,
            bool requireCursorLock = false,
            float interval = 0.48f)
        {
            playerCamera = cameraToUse;
            miningDistance = Mathf.Max(0.1f, distance);
            miningRadius = Mathf.Max(0.02f, radius);
            miningStrength = Mathf.Max(0.01f, strength);
            showCrosshair = displayCrosshair;
            requirePreviouslyLockedCursor = requireCursorLock;
            miningInterval = Mathf.Max(0.1f, interval);
        }

        public void SetMiningInterval(float seconds)
        {
            miningInterval = Mathf.Max(0.1f, seconds);
            nextMiningTime = Mathf.Min(nextMiningTime, Time.unscaledTime + miningInterval);
        }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponent<Camera>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        private void Update()
        {
            Vector2 pointerPosition;
            bool pressedThisFrame;
            bool held;
            ReadPrimaryButton(out pointerPosition, out pressedThisFrame, out held);
            if (playerCamera == null || !held)
            {
                return;
            }

            if (requirePreviouslyLockedCursor && !cursorWasLocked)
            {
                return;
            }

            if (!pressedThisFrame && Time.unscaledTime < nextMiningTime)
            {
                return;
            }

            nextMiningTime = Time.unscaledTime + miningInterval;

            Ray ray = rayFromScreenCenter
                ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : playerCamera.ScreenPointToRay(pointerPosition);

            RaycastHit hit;
            if (!Physics.Raycast(
                    ray,
                    out hit,
                    miningDistance,
                    mineableLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            VoxelRock rock = hit.collider.GetComponentInParent<VoxelRock>();
            if (rock != null && hit.collider == rock.RockCollider)
            {
                rock.Mine(hit.point, miningRadius, miningStrength);
            }
        }

        private void LateUpdate()
        {
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        private static void ReadPrimaryButton(
            out Vector2 pointerPosition,
            out bool pressedThisFrame,
            out bool held)
        {
            pointerPosition = Vector2.zero;
            pressedThisFrame = false;
            held = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
                held = Mouse.current.leftButton.isPressed;
            }

            if (Gamepad.current != null)
            {
                pressedThisFrame |= Gamepad.current.rightTrigger.wasPressedThisFrame;
                held |= Gamepad.current.rightTrigger.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0))
            {
                pointerPosition = Input.mousePosition;
                pressedThisFrame |= Input.GetMouseButtonDown(0);
                held = true;
            }
#endif
        }

        private void OnValidate()
        {
            miningDistance = Mathf.Max(0.1f, miningDistance);
            miningRadius = Mathf.Max(0.02f, miningRadius);
            miningStrength = Mathf.Max(0.01f, miningStrength);
            miningInterval = Mathf.Max(0.1f, miningInterval);
        }

        private void OnGUI()
        {
            if (!showCrosshair || playerCamera == null || !playerCamera.enabled)
            {
                return;
            }

            if (crosshairStyle == null)
            {
                crosshairStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22
                };
                crosshairStyle.normal.textColor = Color.white;
            }

            const float size = 28f;
            GUI.Label(
                new Rect(
                    Screen.width * 0.5f - size * 0.5f,
                    Screen.height * 0.5f - size * 0.5f,
                    size,
                    size),
                "+",
                crosshairStyle);
        }
    }
}
