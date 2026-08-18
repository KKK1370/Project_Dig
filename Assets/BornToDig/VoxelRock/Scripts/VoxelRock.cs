using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace BornToDig.VoxelMining
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelRock : MonoBehaviour
    {
        [Header("Source Model")]
        [Tooltip("MeshFilter on the imported BORN_TO_DIG rock model.")]
        [SerializeField] private MeshFilter sourceModel;
        [SerializeField] private Material rockMaterial;
        [SerializeField] private bool hideSourceAfterInitialization = true;

        [Header("Voxel Grid")]
        [SerializeField, Range(16, 96)] private int resolution = 48;
        [SerializeField, Range(0.05f, 0.95f)] private float isoLevel = 0.5f;
        [SerializeField, Min(0.001f)] private float boundsPadding = 0.06f;

        [Header("Runtime Mesh")]
        [SerializeField] private bool updateMeshCollider = true;
        [SerializeField] private bool logBuildTimes = true;

        private MeshFilter outputMeshFilter;
        private MeshRenderer outputMeshRenderer;
        private MeshCollider outputMeshCollider;
        private Mesh runtimeMesh;
        private VoxelGrid grid;

        public bool IsInitialized => grid != null;
        public MeshCollider RockCollider => outputMeshCollider;
        public float TotalDensityRemoved { get; private set; }
        public event System.Action<float> DensityRemoved;

        private void Awake()
        {
            Initialize();
        }

        public void Configure(MeshFilter model, Material material, int gridResolution = 48)
        {
            sourceModel = model;
            rockMaterial = material;
            resolution = Mathf.Clamp(gridResolution, 16, 96);
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            outputMeshFilter = GetComponent<MeshFilter>();
            outputMeshRenderer = GetComponent<MeshRenderer>();
            outputMeshCollider = GetComponent<MeshCollider>();

            if (sourceModel == null || sourceModel.sharedMesh == null)
            {
                Debug.LogError(
                    "VoxelRock needs the imported rock MeshFilter in Source Model.",
                    this);
                enabled = false;
                return;
            }

            if (sourceModel == outputMeshFilter)
            {
                Debug.LogError(
                    "Source Model must be a separate child object, not this object's MeshFilter.",
                    this);
                enabled = false;
                return;
            }

            Mesh sourceMesh = sourceModel.sharedMesh;
            if (!sourceMesh.isReadable)
            {
                Debug.LogError(
                    "The source rock mesh is not readable. Enable Read/Write on its Model Import Settings.",
                    sourceModel);
                enabled = false;
                return;
            }

            Stopwatch timer = Stopwatch.StartNew();
            Vector3[] vertices = sourceMesh.vertices;
            int[] triangles = sourceMesh.triangles;
            Matrix4x4 sourceToRock = transform.worldToLocalMatrix *
                                      sourceModel.transform.localToWorldMatrix;

            Bounds bounds = new Bounds(sourceToRock.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = sourceToRock.MultiplyPoint3x4(vertices[i]);
                bounds.Encapsulate(vertices[i]);
            }

            bounds.Expand(boundsPadding * 2f);
            grid = new VoxelGrid(resolution, bounds);
            VoxelMeshVoxelizer.FillFromMesh(grid, vertices, triangles);

            PrepareMaterial();
            SetSourceVisible(!hideSourceAfterInitialization);
            RebuildMesh();
            timer.Stop();

            if (logBuildTimes)
            {
                Debug.Log(
                    $"VoxelRock initialized: {resolution}³ grid in {timer.ElapsedMilliseconds} ms.",
                    this);
            }
        }

        /// <summary>
        /// Mines a world-space sphere and rebuilds only when density actually changes.
        /// </summary>
        public bool Mine(Vector3 worldHitPoint, float worldRadius, float strength)
        {
            if (!IsInitialized)
            {
                return false;
            }

            float removedDensity = grid.CarveSphereAmount(
                transform,
                worldHitPoint,
                worldRadius,
                strength);
            if (removedDensity <= 0f)
            {
                return false;
            }

            RebuildMesh();
            TotalDensityRemoved += removedDensity;
            DensityRemoved?.Invoke(removedDensity);
            return true;
        }

        private void RebuildMesh()
        {
            Stopwatch timer = Stopwatch.StartNew();
            MarchingCubes.MeshData meshData = MarchingCubes.Generate(grid, isoLevel);

            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh
                {
                    name = "BORN TO DIG Runtime Voxel Rock",
                    indexFormat = IndexFormat.UInt32
                };
                runtimeMesh.MarkDynamic();
            }
            else
            {
                runtimeMesh.Clear();
            }

            runtimeMesh.SetVertices(meshData.Vertices);
            runtimeMesh.SetTriangles(meshData.Triangles, 0, true);
            runtimeMesh.RecalculateNormals();
            runtimeMesh.RecalculateBounds();
            outputMeshFilter.sharedMesh = runtimeMesh;

            if (updateMeshCollider)
            {
                outputMeshCollider.sharedMesh = null;
                outputMeshCollider.sharedMesh = runtimeMesh;
            }

            timer.Stop();
            if (logBuildTimes)
            {
                Debug.Log(
                    $"VoxelRock mesh rebuilt: {meshData.Vertices.Count} vertices, " +
                    $"{meshData.Triangles.Count / 3} triangles, {timer.ElapsedMilliseconds} ms.",
                    this);
            }
        }

        private void PrepareMaterial()
        {
            if (rockMaterial == null)
            {
                MeshRenderer sourceRenderer = sourceModel.GetComponent<MeshRenderer>();
                if (sourceRenderer != null)
                {
                    rockMaterial = sourceRenderer.sharedMaterial;
                }
            }

            if (rockMaterial != null)
            {
                outputMeshRenderer.sharedMaterial = rockMaterial;
            }
        }

        private void SetSourceVisible(bool visible)
        {
            Renderer[] renderers = sourceModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }

            Collider[] colliders = sourceModel.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = visible;
            }
        }

        private void OnValidate()
        {
            resolution = Mathf.Clamp(resolution, 16, 96);
            isoLevel = Mathf.Clamp(isoLevel, 0.05f, 0.95f);
            boundsPadding = Mathf.Max(0.001f, boundsPadding);
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null)
            {
                Destroy(runtimeMesh);
            }
        }
    }
}
