using UnityEngine;

namespace BornToDig.CharacterMVP
{
    [DisallowMultipleComponent]
    public sealed class DwarfVisualSlot : MonoBehaviour
    {
        [Header("Drop the finished dwarf Prefab or FBX here later")]
        [SerializeField] private GameObject dwarfVisualPrefab;
        [SerializeField] private Transform modelRoot;

        [Header("Visual alignment")]
        [SerializeField] private Vector3 localPosition = Vector3.zero;
        [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;

        private GameObject spawnedVisual;

        private void Start()
        {
            RebuildVisual();
        }

        public void ConfigureGeneratedReferences(Transform root, GameObject visualPrefab)
        {
            modelRoot = root;
            dwarfVisualPrefab = visualPrefab;
        }

        public void RebuildVisual()
        {
            if (spawnedVisual != null)
            {
                Destroy(spawnedVisual);
                spawnedVisual = null;
            }

            if (dwarfVisualPrefab == null || modelRoot == null)
            {
                return;
            }

            spawnedVisual = Instantiate(dwarfVisualPrefab, modelRoot);
            spawnedVisual.name = dwarfVisualPrefab.name + "_Visual";
            spawnedVisual.transform.localPosition = localPosition;
            spawnedVisual.transform.localRotation = Quaternion.Euler(localEulerAngles);
            spawnedVisual.transform.localScale = localScale;
        }

        private void OnValidate()
        {
            localScale.x = Mathf.Approximately(localScale.x, 0f) ? 1f : localScale.x;
            localScale.y = Mathf.Approximately(localScale.y, 0f) ? 1f : localScale.y;
            localScale.z = Mathf.Approximately(localScale.z, 0f) ? 1f : localScale.z;
        }
    }
}
