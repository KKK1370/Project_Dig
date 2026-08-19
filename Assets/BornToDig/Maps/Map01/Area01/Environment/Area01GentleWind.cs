using UnityEngine;

namespace BornToDig.Map01
{
    /// <summary>
    /// Low-cost, scene-local breeze for low-poly vegetation that has no native wind shader.
    /// A single component updates the curated targets and does not allocate per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Area01GentleWind : MonoBehaviour
    {
        [SerializeField] private Transform[] treeTargets = System.Array.Empty<Transform>();
        [SerializeField] private Transform[] grassTargets = System.Array.Empty<Transform>();
        [SerializeField, Min(0.01f)] private float breezeSpeed = 0.42f;
        [SerializeField, Range(0f, 1f)] private float treeAngle = 0.22f;
        [SerializeField, Range(0f, 2f)] private float grassAngle = 0.62f;

        private Quaternion[] treeBaseRotations;
        private Quaternion[] grassBaseRotations;

        public int TreeTargetCount => treeTargets?.Length ?? 0;
        public int GrassTargetCount => grassTargets?.Length ?? 0;

        public void Configure(Transform[] trees, Transform[] grasses)
        {
            treeTargets = trees ?? System.Array.Empty<Transform>();
            grassTargets = grasses ?? System.Array.Empty<Transform>();
            CaptureBaseRotations();
        }

        private void Awake()
        {
            CaptureBaseRotations();
        }

        private void LateUpdate()
        {
            float time = Time.time * breezeSpeed;
            ApplySway(treeTargets, treeBaseRotations, time, treeAngle, 0.19f);
            ApplySway(grassTargets, grassBaseRotations, time, grassAngle, 0.31f);
        }

        private void OnDisable()
        {
            Restore(treeTargets, treeBaseRotations);
            Restore(grassTargets, grassBaseRotations);
        }

        private void CaptureBaseRotations()
        {
            treeBaseRotations = Capture(treeTargets);
            grassBaseRotations = Capture(grassTargets);
        }

        private static Quaternion[] Capture(Transform[] targets)
        {
            if (targets == null) return System.Array.Empty<Quaternion>();
            Quaternion[] rotations = new Quaternion[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                rotations[index] = targets[index] != null ? targets[index].localRotation : Quaternion.identity;
            }
            return rotations;
        }

        private static void ApplySway(
            Transform[] targets,
            Quaternion[] baseRotations,
            float time,
            float angle,
            float phaseStep)
        {
            if (targets == null || baseRotations == null) return;
            int count = Mathf.Min(targets.Length, baseRotations.Length);
            for (int index = 0; index < count; index++)
            {
                Transform target = targets[index];
                if (target == null) continue;
                float phase = time + index * phaseStep;
                float x = Mathf.Sin(phase) * angle;
                float z = Mathf.Sin(phase * 0.61f + 1.7f) * angle * 0.38f;
                target.localRotation = baseRotations[index] * Quaternion.Euler(x, 0f, z);
            }
        }

        private static void Restore(Transform[] targets, Quaternion[] baseRotations)
        {
            if (targets == null || baseRotations == null) return;
            int count = Mathf.Min(targets.Length, baseRotations.Length);
            for (int index = 0; index < count; index++)
            {
                if (targets[index] != null) targets[index].localRotation = baseRotations[index];
            }
        }
    }
}
