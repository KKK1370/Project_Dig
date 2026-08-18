using System;
using System.Collections.Generic;
using BornToDig.Destructibles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    public static class DestructiblePebbleInstaller
    {
        private const string Root = "Assets/BornToDig/DestructiblePebbles";
        private const string ScenePath = "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";
        private const float TargetLongestSize = 0.52f;

        [MenuItem("BORN TO DIG/Destructible Pebbles/Build Available Prefabs")]
        public static void BuildAvailablePrefabs()
        {
            BuildAvailablePrefabsInternal(true);
        }

        public static void BuildRockABatch()
        {
            BuildPrefabsForRock("A");
            InstallSamplesIntoScene(new[] { "A" });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DESTRUCTIBLE_PEBBLE_A_INSTALL_PASS");
        }

        public static void BuildAllBatch()
        {
            BuildAvailablePrefabsInternal(true);
            Debug.Log("DESTRUCTIBLE_PEBBLE_ALL_INSTALL_PASS");
        }

        private static void BuildAvailablePrefabsInternal(bool installSamples)
        {
            var available = new List<string>();
            foreach (string rockId in new[] { "A", "B", "C" })
            {
                string intactPath = ModelPath(rockId, false);
                string fracturedPath = ModelPath(rockId, true);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(intactPath) == null ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(fracturedPath) == null)
                {
                    continue;
                }

                BuildPrefabsForRock(rockId);
                available.Add(rockId);
            }

            if (available.Count == 0)
            {
                throw new InvalidOperationException("No intact/fractured pebble FBX pair was imported.");
            }

            if (installSamples)
            {
                InstallSamplesIntoScene(available);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildPrefabsForRock(string rockId)
        {
            EnsureAssetFolder($"{Root}/Materials");
            EnsureAssetFolder($"{Root}/Prefabs");

            ConfigureModelImporter(ModelPath(rockId, false));
            ConfigureModelImporter(ModelPath(rockId, true));
            ConfigureTextureImporter(TexturePath(rockId, "BaseColor"), false);
            ConfigureTextureImporter(TexturePath(rockId, "Normal"), true);
            ConfigureTextureImporter(TexturePath(rockId, "MetallicRoughness"), false);

            Material material = CreateOrUpdateMaterial(rockId);
            GameObject intactModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(rockId, false));
            GameObject fracturedModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(rockId, true));
            if (intactModel == null || fracturedModel == null)
            {
                throw new InvalidOperationException($"Rock_{rockId} model pair is missing.");
            }

            float uniformScale = CalculateUniformScale(intactModel);
            GameObject fracturedPrefab = CreateFracturedPrefab(
                rockId,
                fracturedModel,
                material,
                uniformScale);
            CreateIntactPrefab(rockId, intactModel, fracturedPrefab, material, uniformScale);
        }

        private static GameObject CreateFracturedPrefab(
            string rockId,
            GameObject modelAsset,
            Material material,
            float uniformScale)
        {
            var root = new GameObject($"Rock_{rockId}_Fractured");
            try
            {
                GameObject visual = InstantiateModel(modelAsset, root.transform, "FracturedVisual");
                visual.transform.localScale = Vector3.one * uniformScale;
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] meshFilters = visual.GetComponentsInChildren<MeshFilter>(true);
                if (meshFilters.Length != 5)
                {
                    throw new InvalidOperationException(
                        $"Rock_{rockId}_Fractured must contain exactly 5 meshes, got {meshFilters.Length}.");
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = material;
                }

                float totalProxyVolume = 0f;
                var proxyVolumes = new float[meshFilters.Length];
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    Vector3 size = meshFilters[i].sharedMesh.bounds.size;
                    proxyVolumes[i] = Mathf.Max(0.0001f, size.x * size.y * size.z);
                    totalProxyVolume += proxyVolumes[i];
                }

                for (int i = 0; i < meshFilters.Length; i++)
                {
                    MeshFilter filter = meshFilters[i];
                    GameObject fragment = filter.gameObject;
                    fragment.name = $"Rock_{rockId}_Fragment_{i + 1:00}";

                    MeshCollider collider = fragment.GetComponent<MeshCollider>();
                    if (collider == null)
                    {
                        collider = fragment.AddComponent<MeshCollider>();
                    }
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = true;
                    collider.cookingOptions =
                        MeshColliderCookingOptions.CookForFasterSimulation |
                        MeshColliderCookingOptions.EnableMeshCleaning |
                        MeshColliderCookingOptions.WeldColocatedVertices;

                    Rigidbody body = fragment.GetComponent<Rigidbody>();
                    if (body == null)
                    {
                        body = fragment.AddComponent<Rigidbody>();
                    }
                    float fraction = proxyVolumes[i] / totalProxyVolume;
                    body.mass = Mathf.Clamp(0.72f * fraction, 0.08f, 0.24f);
                    body.useGravity = true;
                    body.isKinematic = true;
                    body.linearDamping = 0.12f;
                    body.angularDamping = 0.32f;
                    body.maxAngularVelocity = 18f;
                    body.interpolation = RigidbodyInterpolation.None;
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }

                root.AddComponent<FracturedPebbleInstance>();
                string prefabPath = PrefabPath(rockId, true);
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateIntactPrefab(
            string rockId,
            GameObject modelAsset,
            GameObject fracturedPrefab,
            Material material,
            float uniformScale)
        {
            var root = new GameObject($"Rock_{rockId}_Intact");
            try
            {
                GameObject visual = InstantiateModel(modelAsset, root.transform, "IntactVisual");
                visual.transform.localScale = Vector3.one * uniformScale;
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = material;
                }

                Physics.SyncTransforms();
                Bounds bounds = CalculateRendererBounds(root);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;

                DestructiblePebble destructible = root.AddComponent<DestructiblePebble>();
                destructible.Configure(
                    2.5f,
                    fracturedPrefab,
                    0.65f,
                    0.12f,
                    3f,
                    0.006f,
                    2f);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(rockId, false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void InstallSamplesIntoScene(IReadOnlyList<string> rockIds)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("DestructiblePebble_Sample_", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            Camera camera = Camera.main;
            GameObject ground = GameObject.Find("Ground");
            Collider groundCollider = ground != null ? ground.GetComponent<Collider>() : null;
            if (camera == null || groundCollider == null)
            {
                throw new InvalidOperationException("VoxelRockMVP needs Main Camera and Ground Collider.");
            }

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float[] offsets = { 0f, -0.75f, 0.75f };
            // Keep the samples within easy reach but off the existing center ray toward
            // Voxel Rock and its buried gold nugget.
            Vector3 anchor = camera.transform.position + forward * 1.8f + right * 2.4f;

            for (int i = 0; i < rockIds.Count; i++)
            {
                string rockId = rockIds[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(rockId, false));
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Rock_{rockId}_Intact prefab is missing.");
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = $"DestructiblePebble_Sample_{rockId}";
                BoxCollider collider = instance.GetComponent<BoxCollider>();
                Vector3 position = anchor + right * offsets[Mathf.Min(i, offsets.Length - 1)];
                position.y = groundCollider.bounds.max.y + collider.size.y * 0.5f + 0.015f;
                instance.transform.SetPositionAndRotation(
                    position,
                    Quaternion.Euler(0f, 23f + i * 47f, 0f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                Debug.Log($"Placed {instance.name} at {instance.transform.position}.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static float CalculateUniformScale(GameObject modelAsset)
        {
            GameObject temporary = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                Bounds bounds = CalculateRendererBounds(temporary);
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest <= 0.0001f)
                {
                    throw new InvalidOperationException($"{modelAsset.name} has invalid renderer bounds.");
                }
                return TargetLongestSize / longest;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} has no Renderer.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static GameObject InstantiateModel(GameObject asset, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Material CreateOrUpdateMaterial(string rockId)
        {
            string path = $"{Root}/Materials/Pebble_Rock.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader was not found.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "Pebble_Rock" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(rockId, "BaseColor"));
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(rockId, "Normal"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseColor);
            material.SetFloat("_Metallic", 0.03f);
            material.SetFloat("_Smoothness", 0.26f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.8f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string path, bool normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static string ModelPath(string rockId, bool fractured)
        {
            return $"{Root}/Models/Rock_{rockId}_{(fractured ? "Fractured" : "Intact")}.fbx";
        }

        private static string TexturePath(string rockId, string suffix)
        {
            return $"{Root}/Textures/Pebble_{suffix}.png";
        }

        private static string PrefabPath(string rockId, bool fractured)
        {
            return $"{Root}/Prefabs/Rock_{rockId}_{(fractured ? "Fractured" : "Intact")}.prefab";
        }
    }
}
