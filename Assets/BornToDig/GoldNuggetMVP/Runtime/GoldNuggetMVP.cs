using System;
using BornToDig.VoxelMining;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BornToDig.GoldMVP
{
    [DisallowMultipleComponent]
    public sealed class GoldNuggetMVP : MonoBehaviour
    {
        private static readonly Vector3[] ExposureDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
            new Vector3(1f, 1f, 1f).normalized,
            new Vector3(1f, 1f, -1f).normalized,
            new Vector3(1f, -1f, 1f).normalized,
            new Vector3(1f, -1f, -1f).normalized,
            new Vector3(-1f, 1f, 1f).normalized,
            new Vector3(-1f, 1f, -1f).normalized,
            new Vector3(-1f, -1f, 1f).normalized,
            new Vector3(-1f, -1f, -1f).normalized
        };

        [Header("Scene References")]
        [SerializeField] private VoxelRock voxelRock;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Collider interactionCollider;

        [Header("Exposure")]
        [SerializeField, Range(0.4f, 0.6f)] private float requiredExposedFraction = 0.5f;
        [SerializeField, Min(0f)] private float exposurePadding = 0.07f;

        [Header("Pickup")]
        [SerializeField, Range(2f, 3f)] private float pickupDistance = 2.75f;

        private bool isExposed;
        private bool isCollected;
        private bool isPickupTargeted;
        private float exposedFraction;

        public bool IsExposed => isExposed;
        public bool IsCollected => isCollected;
        public bool IsPickupTargeted => isPickupTargeted;
        public float ExposedFraction => exposedFraction;
        public float PickupDistance => pickupDistance;
        public Collider InteractionCollider => interactionCollider;

        public event Action<bool> PickupTargetChanged;
        public event Action Exposed;
        public event Action Collected;

        public void Configure(
            VoxelRock rock,
            Camera cameraToUse,
            Collider pickupCollider,
            float exposureFraction = 0.5f,
            float samplePadding = 0.07f,
            float maximumPickupDistance = 2.75f)
        {
            voxelRock = rock;
            playerCamera = cameraToUse;
            interactionCollider = pickupCollider;
            requiredExposedFraction = Mathf.Clamp(exposureFraction, 0.4f, 0.6f);
            exposurePadding = Mathf.Max(0f, samplePadding);
            pickupDistance = Mathf.Clamp(maximumPickupDistance, 2f, 3f);
        }

        private void OnEnable()
        {
            if (voxelRock != null)
            {
                voxelRock.DensityRemoved += HandleDensityRemoved;
            }
        }

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            EvaluateExposureNow();
        }

        private void Update()
        {
            if (isCollected || !isExposed)
            {
                SetPickupTargeted(false);
                return;
            }

            bool targeted = IsCameraTargetingPickup();
            SetPickupTargeted(targeted);

            if (targeted && WasPickupPressedThisFrame())
            {
                CollectIfAvailable();
            }
        }

        private void OnDisable()
        {
            if (voxelRock != null)
            {
                voxelRock.DensityRemoved -= HandleDensityRemoved;
            }

            SetPickupTargeted(false);
        }

        public bool EvaluateExposureNow()
        {
            if (isCollected || voxelRock == null || !voxelRock.IsInitialized ||
                interactionCollider == null)
            {
                return isExposed;
            }

            Bounds bounds = interactionCollider.bounds;
            int exposedSamples = 0;

            for (int i = 0; i < ExposureDirections.Length; i++)
            {
                Vector3 direction = ExposureDirections[i];
                float surfaceDistance = DistanceToBoundsSurface(bounds.extents, direction);
                Vector3 samplePosition = bounds.center +
                                         direction * (surfaceDistance + exposurePadding);

                if (!voxelRock.IsSolidAtWorldPoint(samplePosition))
                {
                    exposedSamples++;
                }
            }

            exposedFraction = exposedSamples / (float)ExposureDirections.Length;
            if (!isExposed && exposedFraction >= requiredExposedFraction)
            {
                isExposed = true;
                Exposed?.Invoke();
            }

            return isExposed;
        }

        public bool IsCameraTargetingPickup()
        {
            if (!isExposed || isCollected || playerCamera == null ||
                interactionCollider == null || !interactionCollider.enabled)
            {
                return false;
            }

            Vector3 closestPoint = interactionCollider.ClosestPoint(playerCamera.transform.position);
            if (Vector3.Distance(playerCamera.transform.position, closestPoint) > pickupDistance)
            {
                return false;
            }

            Ray centerRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Physics.Raycast(
                       centerRay,
                       out RaycastHit hit,
                       pickupDistance,
                       ~0,
                       QueryTriggerInteraction.Collide) &&
                   hit.collider == interactionCollider;
        }

        public bool CollectIfAvailable()
        {
            if (isCollected || !isExposed || !IsCameraTargetingPickup())
            {
                return false;
            }

            isCollected = true;
            SetPickupTargeted(false);

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            Collected?.Invoke();
            return true;
        }

        private void HandleDensityRemoved(float removedDensity)
        {
            if (removedDensity > 0f && !isExposed)
            {
                EvaluateExposureNow();
            }
        }

        private void SetPickupTargeted(bool targeted)
        {
            if (isPickupTargeted == targeted)
            {
                return;
            }

            isPickupTargeted = targeted;
            PickupTargetChanged?.Invoke(targeted);
        }

        private static bool WasPickupPressedThisFrame()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (Gamepad.current != null)
            {
                pressed |= Gamepad.current.buttonWest.wasPressedThisFrame;
            }

            return pressed;
        }

        private static float DistanceToBoundsSurface(Vector3 extents, Vector3 direction)
        {
            float distance = float.PositiveInfinity;

            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                distance = Mathf.Min(distance, extents.x / Mathf.Abs(direction.x));
            }

            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                distance = Mathf.Min(distance, extents.y / Mathf.Abs(direction.y));
            }

            if (Mathf.Abs(direction.z) > 0.0001f)
            {
                distance = Mathf.Min(distance, extents.z / Mathf.Abs(direction.z));
            }

            return float.IsInfinity(distance) ? 0f : distance;
        }

        private void OnValidate()
        {
            requiredExposedFraction = Mathf.Clamp(requiredExposedFraction, 0.4f, 0.6f);
            exposurePadding = Mathf.Max(0f, exposurePadding);
            pickupDistance = Mathf.Clamp(pickupDistance, 2f, 3f);
        }
    }
}
