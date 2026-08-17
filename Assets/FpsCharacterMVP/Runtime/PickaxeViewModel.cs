using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BornToDig.CharacterMVP
{
    [DisallowMultipleComponent]
    public sealed class PickaxeViewModel : MonoBehaviour
    {
        [SerializeField] private FpsCharacterController characterController;
        [SerializeField] private Transform pickaxeRoot;
        [SerializeField, Min(0.1f)] private float swingInterval = 0.48f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float nextSwingTime;
        private Coroutine swingRoutine;
        private Material runtimeWoodMaterial;
        private Material runtimeMetalMaterial;

        public bool IsSwinging => swingRoutine != null;

        private void Awake()
        {
            EnsureRuntimeVisual();
            StoreRestPose();
        }

        private void Update()
        {
            if (characterController == null || !characterController.InputCaptured || IsSwinging)
            {
                return;
            }

            bool swingHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (Gamepad.current != null)
            {
                swingHeld |= Gamepad.current.rightTrigger.isPressed;
            }

            if (swingHeld && Time.unscaledTime >= nextSwingTime)
            {
                nextSwingTime = Time.unscaledTime + swingInterval;
                swingRoutine = StartCoroutine(AnimateSwing());
            }
        }

        private void OnDisable()
        {
            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                swingRoutine = null;
            }

            RestoreRestPose();
        }

        private void OnDestroy()
        {
            if (runtimeWoodMaterial != null) Destroy(runtimeWoodMaterial);
            if (runtimeMetalMaterial != null) Destroy(runtimeMetalMaterial);
        }

        public void Configure(FpsCharacterController controller, Transform root)
        {
            characterController = controller;
            pickaxeRoot = root;
            StoreRestPose();
        }

        private IEnumerator AnimateSwing()
        {
            Vector3 backPosition = restPosition + new Vector3(0.05f, 0.04f, -0.09f);
            Quaternion backRotation = restRotation * Quaternion.Euler(-18f, 4f, 11f);
            Vector3 strikePosition = restPosition + new Vector3(-0.08f, -0.13f, 0.12f);
            Quaternion strikeRotation = restRotation * Quaternion.Euler(58f, -12f, -22f);

            yield return AnimatePose(restPosition, restRotation, backPosition, backRotation, 0.11f);
            yield return AnimatePose(backPosition, backRotation, strikePosition, strikeRotation, 0.09f);
            yield return AnimatePose(strikePosition, strikeRotation, restPosition, restRotation, 0.22f);

            RestoreRestPose();
            swingRoutine = null;
        }

        private IEnumerator AnimatePose(
            Vector3 fromPosition,
            Quaternion fromRotation,
            Vector3 toPosition,
            Quaternion toRotation,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);

                if (pickaxeRoot != null)
                {
                    pickaxeRoot.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, eased);
                    pickaxeRoot.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, eased);
                }

                yield return null;
            }
        }

        private void StoreRestPose()
        {
            if (pickaxeRoot == null)
            {
                return;
            }

            restPosition = pickaxeRoot.localPosition;
            restRotation = pickaxeRoot.localRotation;
        }

        private void EnsureRuntimeVisual()
        {
            if (pickaxeRoot == null || pickaxeRoot.childCount > 0)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            runtimeWoodMaterial = new Material(shader) { color = new Color32(102, 62, 34, 255) };
            runtimeMetalMaterial = new Material(shader) { color = new Color32(112, 127, 137, 255) };

            CreateRuntimePart(
                "Handle",
                new Vector3(0f, -0.22f, 0f),
                new Vector3(0.075f, 0.78f, 0.075f),
                Quaternion.Euler(0f, 0f, -7f),
                runtimeWoodMaterial);
            CreateRuntimePart(
                "Metal Head",
                new Vector3(0.015f, 0.19f, 0f),
                new Vector3(0.52f, 0.12f, 0.13f),
                Quaternion.identity,
                runtimeMetalMaterial);
            CreateRuntimePart(
                "Left Tip",
                new Vector3(-0.31f, 0.19f, 0f),
                new Vector3(0.22f, 0.075f, 0.09f),
                Quaternion.Euler(0f, 0f, -18f),
                runtimeMetalMaterial);
        }

        private void CreateRuntimePart(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(pickaxeRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Destroy(partCollider);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void RestoreRestPose()
        {
            if (pickaxeRoot == null)
            {
                return;
            }

            pickaxeRoot.localPosition = restPosition;
            pickaxeRoot.localRotation = restRotation;
        }

        private void OnValidate()
        {
            swingInterval = Mathf.Max(0.1f, swingInterval);
        }
    }
}
