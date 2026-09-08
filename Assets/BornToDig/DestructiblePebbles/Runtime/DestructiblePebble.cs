using System;
using UnityEngine;

namespace BornToDig.Destructibles
{
    [DisallowMultipleComponent]
    public sealed class DestructiblePebble : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField, Min(0.01f)] private float hitPoints = 2.5f;

        [Header("Fracture")]
        [SerializeField] private GameObject fracturedPrefab;
        [SerializeField, Min(0f)] private float fragmentImpulse = 0.65f;
        [SerializeField, Min(0f)] private float fragmentTorque = 0.12f;
        [SerializeField, Range(2f, 4f)] private float fragmentLifetime = 3f;

        [Header("Variation")]
        [SerializeField, Range(0f, 0.02f)] private float positionJitter = 0.006f;
        [SerializeField, Range(0f, 5f)] private float rotationJitter = 2f;

        private float currentHitPoints;
        private bool isBroken;

        public float HitPoints => hitPoints;
        public float CurrentHitPoints => currentHitPoints;
        public float FragmentImpulse => fragmentImpulse;
        public float FragmentLifetime => fragmentLifetime;
        public GameObject FracturedPrefab => fracturedPrefab;
        public bool IsBroken => isBroken;

        public event Action Broken;

        public void Configure(
            float maximumHitPoints,
            GameObject fracturePrefab,
            float impulse = 0.65f,
            float torque = 0.12f,
            float lifetime = 3f,
            float spawnPositionJitter = 0.006f,
            float spawnRotationJitter = 2f)
        {
            hitPoints = Mathf.Max(0.01f, maximumHitPoints);
            fracturedPrefab = fracturePrefab;
            fragmentImpulse = Mathf.Max(0f, impulse);
            fragmentTorque = Mathf.Max(0f, torque);
            fragmentLifetime = Mathf.Clamp(lifetime, 2f, 4f);
            positionJitter = Mathf.Clamp(spawnPositionJitter, 0f, 0.02f);
            rotationJitter = Mathf.Clamp(spawnRotationJitter, 0f, 5f);
            currentHitPoints = hitPoints;
        }

        private void Awake()
        {
            currentHitPoints = hitPoints;
        }

        public bool TakeDamage(float damage, Vector3 hitPosition, Vector3 hitDirection)
        {
            if (isBroken || damage <= 0f)
            {
                return false;
            }

            currentHitPoints = Mathf.Max(0f, currentHitPoints - damage);
            if (currentHitPoints <= 0f)
            {
                Break(hitPosition, hitDirection);
            }

            return true;
        }

        public void Break(Vector3 hitPosition, Vector3 hitDirection)
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            if (fracturedPrefab == null)
            {
                Debug.LogError($"{name} cannot break because no Fractured Prefab is assigned.", this);
                isBroken = false;
                return;
            }

            GameObject fractured = Instantiate(fracturedPrefab, transform.position, transform.rotation);
            fractured.name = fracturedPrefab.name;
            fractured.transform.localScale = Vector3.Scale(
                fracturedPrefab.transform.localScale,
                transform.lossyScale);

            Rigidbody[] fragmentBodies = fractured.GetComponentsInChildren<Rigidbody>(true);
            Vector3 impactDirection = hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection.normalized
                : (transform.position - hitPosition).normalized;
            if (impactDirection.sqrMagnitude < 0.0001f)
            {
                impactDirection = transform.forward;
            }

            float scaleMagnitude = Mathf.Max(0.1f, transform.lossyScale.magnitude / Mathf.Sqrt(3f));
            for (int i = 0; i < fragmentBodies.Length; i++)
            {
                Rigidbody body = fragmentBodies[i];
                Transform bodyTransform = body.transform;
                bodyTransform.position += UnityEngine.Random.insideUnitSphere *
                                          positionJitter * scaleMagnitude;
                if (rotationJitter > 0f)
                {
                    bodyTransform.rotation = Quaternion.AngleAxis(
                        UnityEngine.Random.Range(-rotationJitter, rotationJitter),
                        UnityEngine.Random.onUnitSphere) * bodyTransform.rotation;
                }

                body.isKinematic = false;
                body.useGravity = true;
                body.WakeUp();

                Vector3 outward = body.worldCenterOfMass - fractured.transform.position;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.up;
                Vector3 impulseDirection =
                    impactDirection * 0.62f +
                    outward * 0.23f +
                    Vector3.up * 0.15f +
                    UnityEngine.Random.insideUnitSphere * 0.10f;
                impulseDirection.Normalize();

                float impulseVariation = UnityEngine.Random.Range(0.78f, 1.12f);
                body.AddForceAtPosition(
                    impulseDirection * fragmentImpulse * impulseVariation,
                    hitPosition,
                    ForceMode.Impulse);
                body.AddTorque(
                    UnityEngine.Random.insideUnitSphere * fragmentTorque,
                    ForceMode.Impulse);
            }

            FracturedPebbleInstance instance = fractured.GetComponent<FracturedPebbleInstance>();
            if (instance == null)
            {
                instance = fractured.AddComponent<FracturedPebbleInstance>();
            }
            instance.Initialize(fragmentLifetime, fragmentBodies.Length);

            Broken?.Invoke();
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            hitPoints = Mathf.Max(0.01f, hitPoints);
            fragmentImpulse = Mathf.Max(0f, fragmentImpulse);
            fragmentTorque = Mathf.Max(0f, fragmentTorque);
            fragmentLifetime = Mathf.Clamp(fragmentLifetime, 2f, 4f);
            positionJitter = Mathf.Clamp(positionJitter, 0f, 0.02f);
            rotationJitter = Mathf.Clamp(rotationJitter, 0f, 5f);
        }
    }
}
