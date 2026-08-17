#if UNITY_EDITOR
using System;
using BornToDig.VoxelMining;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BornToDig.EditorTools
{
    public static class VoxelRockMvpVerifier
    {
        private const string ScenePath =
            "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";

        public static void VerifyBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);

            VoxelRock rock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();
            MiningTool tool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            Camera camera = Camera.main;
            if (rock == null || tool == null || camera == null)
            {
                throw new InvalidOperationException("MVP scene is missing the rock, mining tool, or camera.");
            }

            rock.Initialize();
            MeshFilter filter = rock.GetComponent<MeshFilter>();
            MeshCollider collider = rock.GetComponent<MeshCollider>();
            if (filter.sharedMesh == null ||
                filter.sharedMesh.vertexCount == 0 ||
                filter.sharedMesh.triangles.Length == 0)
            {
                throw new InvalidOperationException("VoxelRock did not generate a visible mesh.");
            }

            if (collider.sharedMesh != filter.sharedMesh)
            {
                throw new InvalidOperationException("MeshCollider was not updated to the runtime rock mesh.");
            }

            int initialVertices = filter.sharedMesh.vertexCount;
            int initialTriangles = filter.sharedMesh.triangles.Length / 3;
            Ray centerRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Physics.SyncTransforms();

            RaycastHit hit;
            if (!Physics.Raycast(centerRay, out hit, 4f) || hit.collider != collider)
            {
                throw new InvalidOperationException("The camera center ray did not hit the voxel rock.");
            }

            float firstDistance = hit.distance;
            float deepestDistance = firstDistance;
            int successfulHits = 0;
            bool penetrated = false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                if (!Physics.Raycast(centerRay, out hit, 4f) || hit.collider != collider)
                {
                    penetrated = true;
                    break;
                }

                deepestDistance = Mathf.Max(deepestDistance, hit.distance);
                if (!rock.Mine(hit.point, 0.2f, 0.75f))
                {
                    throw new InvalidOperationException("A raycast hit did not change the density grid.");
                }

                successfulHits++;
                Physics.SyncTransforms();
            }

            if (successfulHits == 0)
            {
                throw new InvalidOperationException("The mining loop did not perform any hits.");
            }

            if (!penetrated && deepestDistance <= firstDistance + 0.1f)
            {
                throw new InvalidOperationException("Repeated mining did not deepen the surface.");
            }

            if (!penetrated)
            {
                throw new InvalidOperationException("The center tunnel did not penetrate within 24 hits.");
            }

            if (collider.sharedMesh != filter.sharedMesh)
            {
                throw new InvalidOperationException("MeshCollider stopped matching the carved mesh.");
            }

            Debug.Log(
                "VOXEL_ROCK_SMOKE_TEST_PASS " +
                $"initialVertices={initialVertices} " +
                $"initialTriangles={initialTriangles} " +
                $"successfulHits={successfulHits} " +
                $"firstDistance={firstDistance:F3} " +
                $"deepestDistance={deepestDistance:F3} " +
                $"penetrated={penetrated}");
        }
    }
}
#endif
