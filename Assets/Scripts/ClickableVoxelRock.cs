using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public sealed class ClickableVoxelRock : MonoBehaviour
{
    private const int Resolution = 32;

    [Header("Rock")]
    [SerializeField, Min(0.01f)] private float voxelSize = 0.1f;
    [SerializeField] private int seed = 12345;
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Color rockColor = new Color(0.34f, 0.30f, 0.26f, 1f);

    [Header("Click Dent")]
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.01f)] private float dentRadius = 0.35f;
    [SerializeField, Min(0.1f)] private float maximumClickDistance = 100f;

    private readonly bool[,,] voxels = new bool[Resolution, Resolution, Resolution];

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh rockMesh;
    private Material generatedMaterial;

    public event System.Action<int> VoxelsRemoved;
    public int TotalVoxelsRemoved { get; private set; }

    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int( 1,  0,  0),
        new Vector3Int(-1,  0,  0),
        new Vector3Int( 0,  1,  0),
        new Vector3Int( 0, -1,  0),
        new Vector3Int( 0,  0,  1),
        new Vector3Int( 0,  0, -1)
    };

    private static readonly Vector3[,] FaceVertices =
    {
        {
            new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f)
        },
        {
            new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f)
        },
        {
            new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f)
        },
        {
            new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f)
        },
        {
            new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f)
        },
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f)
        }
    };

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        PrepareMaterial();
        GenerateRockVoxels();
        RebuildMesh();
    }

    private void Update()
    {
        Vector2 screenPosition;
        if (targetCamera == null || !TryGetClickPosition(out screenPosition))
        {
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(
            new Vector3(screenPosition.x, screenPosition.y, 0f));

        TryMine(ray);
    }

    public int TryMine(Ray ray)
    {
        if (meshCollider == null)
        {
            return 0;
        }

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, maximumClickDistance) || hit.collider != meshCollider)
        {
            return 0;
        }

        return CarveSphere(hit.point);
    }

    private static bool TryGetClickPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private void GenerateRockVoxels()
    {
        Vector3 center = Vector3.one * ((Resolution - 1) * 0.5f);

        for (int x = 0; x < Resolution; x++)
        for (int y = 0; y < Resolution; y++)
        for (int z = 0; z < Resolution; z++)
        {
            Vector3 normalizedPosition = new Vector3(
                (x - center.x) / 13.6f,
                (y - center.y) / 11.8f,
                (z - center.z) / 12.8f);

            float noiseA = Mathf.PerlinNoise(
                x * 0.13f + seed * 0.017f,
                (y + z) * 0.09f + seed * 0.031f);

            float noiseB = Mathf.PerlinNoise(
                y * 0.13f + seed * 0.047f,
                (z + x) * 0.09f + seed * 0.073f);

            float surfaceRadius = Mathf.Lerp(0.88f, 1.08f, (noiseA + noiseB) * 0.5f);
            voxels[x, y, z] = normalizedPosition.sqrMagnitude <= surfaceRadius * surfaceRadius;
        }
    }

    private int CarveSphere(Vector3 worldHitPoint)
    {
        float radiusSquared = dentRadius * dentRadius;
        int removedCount = 0;

        for (int x = 0; x < Resolution; x++)
        for (int y = 0; y < Resolution; y++)
        for (int z = 0; z < Resolution; z++)
        {
            if (!voxels[x, y, z])
            {
                continue;
            }

            Vector3 worldVoxelCenter = transform.TransformPoint(GetVoxelCenter(x, y, z));
            if ((worldVoxelCenter - worldHitPoint).sqrMagnitude <= radiusSquared)
            {
                voxels[x, y, z] = false;
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            RebuildMesh();
            TotalVoxelsRemoved += removedCount;
            VoxelsRemoved?.Invoke(removedCount);
        }

        return removedCount;
    }

    private void RebuildMesh()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int x = 0; x < Resolution; x++)
        for (int y = 0; y < Resolution; y++)
        for (int z = 0; z < Resolution; z++)
        {
            if (!voxels[x, y, z])
            {
                continue;
            }

            for (int face = 0; face < Directions.Length; face++)
            {
                Vector3Int direction = Directions[face];
                if (IsSolid(x + direction.x, y + direction.y, z + direction.z))
                {
                    continue;
                }

                int firstVertex = vertices.Count;
                Vector3 center = GetVoxelCenter(x, y, z);

                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.Add(center + FaceVertices[face, corner] * voxelSize);
                    normals.Add(direction);
                }

                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 2);
                triangles.Add(firstVertex + 1);
                triangles.Add(firstVertex);
                triangles.Add(firstVertex + 3);
                triangles.Add(firstVertex + 2);
            }
        }

        if (rockMesh == null)
        {
            rockMesh = new Mesh
            {
                name = "32x32x32 Voxel Rock",
                indexFormat = IndexFormat.UInt32
            };
            rockMesh.MarkDynamic();
        }
        else
        {
            rockMesh.Clear();
        }

        rockMesh.SetVertices(vertices);
        rockMesh.SetNormals(normals);
        rockMesh.SetTriangles(triangles, 0);
        rockMesh.RecalculateBounds();

        meshFilter.sharedMesh = rockMesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = rockMesh;
    }

    private Vector3 GetVoxelCenter(int x, int y, int z)
    {
        float offset = Resolution * voxelSize * 0.5f;
        return new Vector3(
            (x + 0.5f) * voxelSize - offset,
            (y + 0.5f) * voxelSize - offset,
            (z + 0.5f) * voxelSize - offset);
    }

    private bool IsSolid(int x, int y, int z)
    {
        return x >= 0 && x < Resolution &&
               y >= 0 && y < Resolution &&
               z >= 0 && z < Resolution &&
               voxels[x, y, z];
    }

    private void PrepareMaterial()
    {
        if (rockMaterial != null)
        {
            meshRenderer.sharedMaterial = rockMaterial;
            return;
        }

        if (meshRenderer.sharedMaterial != null)
        {
            return;
        }

        Shader shader;
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            shader = Shader.Find("Standard");
        }
        else
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Lit");
            }
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogWarning("Compatible rock shader was not found. Assign Rock Material in the Inspector.", this);
            return;
        }

        generatedMaterial = new Material(shader) { name = "Generated Rock Material" };
        if (generatedMaterial.HasProperty("_BaseColor"))
        {
            generatedMaterial.SetColor("_BaseColor", rockColor);
        }
        else if (generatedMaterial.HasProperty("_Color"))
        {
            generatedMaterial.SetColor("_Color", rockColor);
        }

        meshRenderer.sharedMaterial = generatedMaterial;
    }

    private void OnDestroy()
    {
        if (rockMesh != null)
        {
            Destroy(rockMesh);
        }

        if (generatedMaterial != null)
        {
            Destroy(generatedMaterial);
        }
    }
}
