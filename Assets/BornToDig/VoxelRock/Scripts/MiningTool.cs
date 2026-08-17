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

        [Header("Feedback")]
        [SerializeField] private bool showCrosshair = true;
        [Tooltip("Prevents the click used to recapture an FPS cursor from mining.")]
        [SerializeField] private bool requirePreviouslyLockedCursor;

        private GUIStyle crosshairStyle;
        private bool cursorWasLocked;

        public void Configure(
            Camera cameraToUse,
            float distance = 4f,
            float radius = 0.2f,
            float strength = 0.75f,
            bool displayCrosshair = true,
            bool requireCursorLock = false)
        {
            playerCamera = cameraToUse;
            miningDistance = Mathf.Max(0.1f, distance);
            miningRadius = Mathf.Max(0.02f, radius);
            miningStrength = Mathf.Max(0.01f, strength);
            showCrosshair = displayCrosshair;
            requirePreviouslyLockedCursor = requireCursorLock;
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
            if (playerCamera == null || !WasPrimaryButtonPressed(out pointerPosition))
            {
                return;
            }

            if (requirePreviouslyLockedCursor && !cursorWasLocked)
            {
                return;
            }

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

        private static bool WasPrimaryButtonPressed(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                pointerPosition = Input.mousePosition;
                return true;
            }
#endif

            pointerPosition = Vector2.zero;
            return false;
        }

        private void OnValidate()
        {
            miningDistance = Mathf.Max(0.1f, miningDistance);
            miningRadius = Mathf.Max(0.02f, miningRadius);
            miningStrength = Mathf.Max(0.01f, miningStrength);
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
