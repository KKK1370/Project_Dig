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

            VerifyFpsCompatibility(tool, camera);

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
            float verificationDistance = Vector3.Distance(
                camera.transform.position,
                collider.bounds.center) + collider.bounds.extents.magnitude + 1f;
            int maximumAttempts = Mathf.Max(
                24,
                Mathf.CeilToInt(collider.bounds.size.z / 0.05f));
            Physics.SyncTransforms();

            RaycastHit hit;
            bool hitAnything = Physics.Raycast(centerRay, out hit, verificationDistance);
            if (!hitAnything || hit.collider != collider)
            {
                string hitDescription = hitAnything
                    ? $"hit={hit.collider.name} at {hit.distance:F3}m"
                    : "hit=nothing";
                throw new InvalidOperationException(
                    "The camera center ray did not hit the voxel rock. " +
                    $"{hitDescription}, camera={camera.transform.position}, " +
                    $"forward={camera.transform.forward}, rockBounds={collider.bounds}");
            }

            float firstDistance = hit.distance;
            float deepestDistance = firstDistance;
            int successfulHits = 0;
            bool penetrated = false;

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                if (!Physics.Raycast(centerRay, out hit, verificationDistance) ||
                    hit.collider != collider)
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
                throw new InvalidOperationException(
                    $"The center tunnel did not penetrate within {maximumAttempts} hits. " +
                    $"firstDistance={firstDistance:F3}, deepestDistance={deepestDistance:F3}");
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

        private static void VerifyFpsCompatibility(MiningTool tool, Camera primaryCamera)
        {
            GameObject player = GameObject.Find("MVP_FPS_Player");
            if (player == null)
            {
                throw new InvalidOperationException("VoxelRockMVP does not contain the existing FPS player.");
            }

            if (tool.gameObject != primaryCamera.gameObject)
            {
                throw new InvalidOperationException("MiningTool is not attached to the FPS player camera.");
            }

            int enabledSceneCameras = 0;
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].gameObject.scene.IsValid() && cameras[i].enabled)
                {
                    enabledSceneCameras++;
                }
            }

            if (enabledSceneCameras != 1)
            {
                throw new InvalidOperationException(
                    $"VoxelRockMVP has {enabledSceneCameras} enabled cameras; exactly one is required.");
            }

            int enabledListeners = 0;
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].gameObject.scene.IsValid() && listeners[i].enabled)
                {
                    enabledListeners++;
                }
            }

            if (enabledListeners != 1)
            {
                throw new InvalidOperationException(
                    $"VoxelRockMVP has {enabledListeners} enabled AudioListeners; exactly one is required.");
            }

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include);
            bool hasFpsController = false;
            bool hasCharacterHud = false;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName;
                if (typeName == "BornToDig.CharacterMVP.FpsCharacterController")
                {
                    hasFpsController = behaviour.enabled;
                }
                else if (typeName == "BornToDig.CharacterMVP.CharacterMvpHud")
                {
                    hasCharacterHud = behaviour.enabled;
                }
                else if (behaviour.GetType().Name == "FlyCameraController" && behaviour.enabled)
                {
                    throw new InvalidOperationException(
                        "FlyCameraController is enabled together with FpsCharacterController.");
                }
                else if (behaviour.GetType().Name == "ClickableVoxelRock" && behaviour.enabled)
                {
                    throw new InvalidOperationException(
                        "The legacy ClickableVoxelRock must not be active in VoxelRockMVP.");
                }
            }

            if (!hasFpsController || !hasCharacterHud)
            {
                throw new InvalidOperationException(
                    "VoxelRockMVP is missing the FPS controller or its single crosshair HUD.");
            }
        }
    }
}
#endif
