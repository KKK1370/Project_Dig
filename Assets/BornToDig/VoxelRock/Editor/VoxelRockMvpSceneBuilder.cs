#if UNITY_EDITOR
using System;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    public static class VoxelRockMvpSceneBuilder
    {
        private const string ModelPath =
            "Assets/BornToDig/VoxelRock/Models/BORN_TO_DIG_Rock.fbx";
        private const string MaterialPath =
            "Assets/BornToDig/VoxelRock/Materials/BORN_TO_DIG_Rock.mat";
        private const string ScenePath =
            "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";

        [MenuItem("Tools/BORN TO DIG/Create Voxel Rock MVP Scene")]
        public static void CreateMvpSceneFromMenu()
        {
            if (CreateMvpScene())
            {
                EditorUtility.DisplayDialog(
                    "BORN TO DIG",
                    "VoxelRockMVP scene was created and opened. Press Play to test it.",
                    "OK");
            }
        }

        // Used by automated project verification as well as the menu command.
        public static void CreateMvpSceneBatch()
        {
            if (!CreateMvpScene())
            {
                throw new InvalidOperationException("Could not create the Voxel Rock MVP scene.");
            }
        }

        private static bool CreateMvpScene()
        {
            if (!MakeModelReadable())
            {
                return false;
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"Rock model was not found at {ModelPath}");
                return false;
            }

            EnsureFolder("Assets/BornToDig/VoxelRock/Materials");
            EnsureFolder("Assets/BornToDig/VoxelRock/Scenes");
            Material rockMaterial = GetOrCreateRockMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject rockObject = new GameObject("Voxel Rock");
            MeshFilter outputFilter = rockObject.AddComponent<MeshFilter>();
            MeshRenderer outputRenderer = rockObject.AddComponent<MeshRenderer>();
            MeshCollider outputCollider = rockObject.AddComponent<MeshCollider>();
            outputCollider.convex = false;

            GameObject sourceInstance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (sourceInstance == null)
            {
                sourceInstance = UnityEngine.Object.Instantiate(modelPrefab);
            }

            sourceInstance.name = "Source Model (hidden while playing)";
            sourceInstance.transform.SetParent(rockObject.transform, false);
            sourceInstance.transform.localPosition = Vector3.zero;
            sourceInstance.transform.localRotation = Quaternion.identity;
            sourceInstance.transform.localScale = Vector3.one;

            MeshFilter sourceFilter = FindLargestMesh(sourceInstance);
            if (sourceFilter == null)
            {
                Debug.LogError("The imported rock asset does not contain a mesh.");
                UnityEngine.Object.DestroyImmediate(rockObject);
                return false;
            }

            Renderer sourceRenderer = sourceFilter.GetComponent<Renderer>();
            if (sourceRenderer != null)
            {
                Vector3 centeringOffset = rockObject.transform.position - sourceRenderer.bounds.center;
                sourceInstance.transform.position += centeringOffset;
                rockObject.transform.position = new Vector3(
                    0f,
                    sourceRenderer.bounds.extents.y + 0.02f,
                    0f);
            }
            else
            {
                rockObject.transform.position = new Vector3(0f, 0.65f, 0f);
            }

            outputRenderer.sharedMaterial = rockMaterial;
            VoxelRock voxelRock = rockObject.AddComponent<VoxelRock>();
            voxelRock.Configure(sourceFilter, rockMaterial, 48);

            CreateGround();
            CreateLight();
            CreatePlayerCamera(rockObject.transform.position + Vector3.up * 0.1f);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Could not save scene at {ScenePath}");
                return false;
            }

            Selection.activeGameObject = rockObject;
            Debug.Log($"BORN TO DIG Voxel Rock MVP scene created: {ScenePath}");
            return true;
        }

        private static bool MakeModelReadable()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Unity could not import the FBX model at {ModelPath}");
                return false;
            }

            bool needsReimport = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                needsReimport = true;
            }

            // Blender's FBX unit metadata imports this particular source at 1/100 scale.
            // Restore the measured GLB size (about 1.9 m wide) for Unity gameplay units.
            if (!Mathf.Approximately(importer.globalScale, 100f))
            {
                importer.globalScale = 100f;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }

            return true;
        }

        private static MeshFilter FindLargestMesh(GameObject root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter largest = null;
            long largestIndexCount = -1;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                long indexCount = 0;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    indexCount += (long)mesh.GetIndexCount(subMesh);
                }

                if (indexCount > largestIndexCount)
                {
                    largestIndexCount = indexCount;
                    largest = filters[i];
                }
            }

            return largest;
        }

        private static Material GetOrCreateRockMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { name = "BORN TO DIG Rock" };
            Color color = new Color(0.31f, 0.27f, 0.22f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreatePlayerCamera(Vector3 target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = target + new Vector3(0f, 0.25f, -3f);
            cameraObject.transform.LookAt(target);

            MiningTool miningTool = cameraObject.AddComponent<MiningTool>();
            if (VoxelRockFpsCompatibility.TryCreateExistingFpsPlayer())
            {
                miningTool.Configure(camera, 4f, 0.2f, 0.75f, false, true);
                return;
            }

            // Fallback for a project that does not contain FpsCharacterMVP.
            miningTool.Configure(camera, 4f, 0.2f, 0.75f, true, false);
            Type flyController = Type.GetType("FlyCameraController, Assembly-CSharp");
            if (flyController != null && typeof(MonoBehaviour).IsAssignableFrom(flyController))
            {
                cameraObject.AddComponent(flyController);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
