using UnityEngine;

namespace BornToDig.Destructibles
{
    [DisallowMultipleComponent]
    public sealed class FracturedPebbleInstance : MonoBehaviour
    {
        public float Lifetime { get; private set; }
        public int FragmentCount { get; private set; }

        public void Initialize(float lifetime, int fragmentCount)
        {
            Lifetime = Mathf.Clamp(lifetime, 2f, 4f);
            FragmentCount = fragmentCount;
            Destroy(gameObject, Lifetime);
        }
    }
}
