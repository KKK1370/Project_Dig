#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    /// <summary>
    /// Builds an isolated visual review scene from PurePoly Mining Pack prefabs.
    /// It intentionally does not open or modify VoxelRockMVP, demo scenes, or project settings.
    /// </summary>
    public static class PurePolyMiningPackEnvironmentSceneBuilder
    {
        public const string ScenePath =
            "Assets/BornToDig/EnvironmentIntegration/Scenes/PurePolyMiningPackEnvironmentTest.unity";

        private const string RootName = "PurePoly_Mining_Pack_Background_Only";

        [MenuItem("Born To Dig/Environment/Create PurePoly Mining Pack Test Scene")]
        public static void CreateFromMenu()
        {
            Create();
            Debug.Log("PUREPOLY_ENVIRONMENT_TEST_SCENE_CREATED path=" + ScenePath);
        }

        public static void CreateBatch()
        {
            try
            {
                Create();
                Debug.Log("PUREPOLY_ENVIRONMENT_TEST_SCENE_CREATED path=" + ScenePath);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    throw;
                }
            }
        }

        public static void CreateAndRenderPreviewBatch()
        {
            try
            {
                Create();
                Camera camera = Camera.main;
                if (camera == null)
                {
                    throw new InvalidOperationException("Environment test scene did not create its review Camera.");
                }

                const int width = 1280;
                const int height = 720;
                RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24);
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture previousTarget = camera.targetTexture;
                try
                {
                    camera.targetTexture = renderTexture;
                    camera.Render();
                    RenderTexture.active = renderTexture;
                    var preview = new Texture2D(width, height, TextureFormat.RGB24, false);
                    preview.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    preview.Apply();
                    string logDirectory = Path.Combine(Application.dataPath, "..", "Logs");
                    Directory.CreateDirectory(logDirectory);
                    string previewPath = Path.Combine(logDirectory, "PurePolyEnvironmentPreview.png");
                    File.WriteAllBytes(previewPath, preview.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(preview);
                    Debug.Log("PUREPOLY_ENVIRONMENT_PREVIEW_RENDERED path=" + previewPath);
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(renderTexture);
                }

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    throw;
                }
            }
        }

        public static void Create()
        {
            EnsureFolder("Assets/BornToDig", "EnvironmentIntegration");
            EnsureFolder("Assets/BornToDig/EnvironmentIntegration", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject(RootName);

            // All imported objects are display-only: no Collider can intercept MiningTool's all-layer raycast.
            AddBackground(root.transform, "Ground",
                "Assets/PurePoly/Mining_Pack/Prefabs/Environment/PP_Ground_01.prefab",
                new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(2f, 1f, 2f));
            AddBackground(root.transform, "Mountain_Backdrop",
                "Assets/PurePoly/Mining_Pack/Prefabs/Environment/PP_Mountain_01.prefab",
                new Vector3(0f, 0f, 30f), new Vector3(0f, 180f, 0f), new Vector3(0.35f, 0.35f, 0.35f));
            AddBackground(root.transform, "Moss_Plateau_Backdrop",
                "Assets/PurePoly/Mining_Pack/Prefabs/Environment/PP_Rock_Plateau_Moss_01.prefab",
                new Vector3(-11f, 0f, 13f), new Vector3(0f, 35f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            AddBackground(root.transform, "Brown_Rock_Backdrop",
                "Assets/PurePoly/Mining_Pack/Prefabs/Stones, Rocks/PP_Rock_Brown_01.prefab",
                new Vector3(10f, 0f, 11f), new Vector3(0f, -25f, 0f), new Vector3(1.2f, 1.2f, 1.2f));

            AddBackground(root.transform, "Fir_Tree_A",
                "Assets/PurePoly/Mining_Pack/Prefabs/Vegetation/PP_Fir_Tree_01.prefab",
                new Vector3(-7f, 0f, 5f), new Vector3(0f, 15f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
            AddBackground(root.transform, "Fir_Tree_B",
                "Assets/PurePoly/Mining_Pack/Prefabs/Vegetation/PP_Fir_Tree_04.prefab",
                new Vector3(7f, 0f, 6f), new Vector3(0f, -20f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
            AddBackground(root.transform, "Grass_A",
                "Assets/PurePoly/Mining_Pack/Prefabs/Vegetation/PP_Grass_01.prefab",
                new Vector3(-2f, 0f, 4f), new Vector3(0f, 25f, 0f), Vector3.one);
            AddBackground(root.transform, "Grass_B",
                "Assets/PurePoly/Mining_Pack/Prefabs/Vegetation/PP_Grass_01.prefab",
                new Vector3(3f, 0f, 7f), new Vector3(0f, -30f, 0f), new Vector3(1.2f, 1.2f, 1.2f));

            AddBackground(root.transform, "Cave_Entrance",
                "Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Stone_Cave_Entrance_01.prefab",
                new Vector3(0f, 0f, 15f), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f));
            AddBackground(root.transform, "Cave_Tube",
                "Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_01.prefab",
                new Vector3(7f, 0f, 17f), new Vector3(0f, 90f, 0f), new Vector3(0.3f, 0.3f, 0.3f));

            CreateLighting();
            CreateReviewCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save the PurePoly environment test scene.");
            }

            AssetDatabase.SaveAssets();
        }

        private static void AddBackground(
            Transform parent,
            string instanceName,
            string prefabPath,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Required PurePoly prefab was not found: " + prefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.name = instanceName + "_BackgroundOnly";
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));
            instance.transform.localScale = scale;
            int disabledColliderCount = SetDisplayOnly(instance);
            int rendererCount = ValidateMaterials(instance, prefabPath);
            ValidateNoMissingComponents(instance, prefabPath);
            int lodGroupCount = instance.GetComponentsInChildren<LODGroup>(true).Length;
            Bounds bounds = CalculateBounds(instance);
            Debug.Log(
                "PUREPOLY_ENVIRONMENT_PREFAB_OK name=" + instanceName +
                " renderers=" + rendererCount +
                " disabledColliders=" + disabledColliderCount +
                " lodGroups=" + lodGroupCount +
                " boundsCenter=" + bounds.center +
                " boundsSize=" + bounds.size +
                " path=" + prefabPath);
        }

        private static int SetDisplayOnly(GameObject instance)
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 2; // Ignore Raycast; retained as a second safety boundary.
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }

            return colliders.Length;
        }

        private static int ValidateMaterials(GameObject instance, string prefabPath)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("PurePoly prefab has no Renderer: " + prefabPath);
            }

            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                    {
                        throw new InvalidOperationException("Missing Material or Shader in: " + prefabPath);
                    }
                }
            }

            return renderers.Length;
        }

        private static void ValidateNoMissingComponents(GameObject instance, string prefabPath)
        {
            foreach (Component component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    throw new InvalidOperationException("Missing Script in: " + prefabPath);
                }
            }
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.68f, 0.72f, 1f);
            RenderSettings.fog = false;

            GameObject lightObject = new GameObject("Environment_Test_Directional_Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.82f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void CreateReviewCamera()
        {
            GameObject cameraObject = new GameObject("Environment_Test_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.62f, 0.76f, 1f);
            camera.fieldOfView = 55f;
            camera.farClipPlane = 120f;
            cameraObject.transform.position = new Vector3(35f, 23f, -38f);
            cameraObject.transform.LookAt(new Vector3(0f, 4f, 13f));
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
