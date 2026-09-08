using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BornToDig.CharacterMVP.Editor
{
    public static class FpsCharacterBuilder
    {
        private const string RootFolder = "Assets/FpsCharacterMVP";
        private const string MaterialFolder = RootFolder + "/Generated/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string PlayerPrefabPath = PrefabFolder + "/MVP_FPS_Player.prefab";
        private const string PlayerName = "MVP_FPS_Player";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        public static void CreateFpsCharacterInSampleSceneAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            CreateFpsCharacterOnly();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException("Failed to save the scene after creating the FPS character.");
            }

            Debug.Log("Born to Dig: FPS character creation and scene save completed.");
        }

        [MenuItem("Tools/Born to Dig/Create FPS Character Only")]
        public static void CreateFpsCharacterOnly()
        {
            GameObject existing = GameObject.Find(PlayerName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                Debug.Log("Born to Dig: the FPS character already exists. No rock objects were changed.", existing);
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            Material wood = GetOrCreateMaterial("Pickaxe_Wood.mat", new Color32(102, 62, 34, 255), 0f, 0.18f);
            Material metal = GetOrCreateMaterial("Pickaxe_Metal.mat", new Color32(112, 127, 137, 255), 0.72f, 0.45f);

            Camera mainCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -4f), Quaternion.identity);
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
            }

            GameObject player = BuildPlayer(mainCamera, wood, metal);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            if (savedPrefab == null)
            {
                throw new IOException("Failed to save the FPS player prefab.");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log(
                "Born to Dig FPS character created. Existing rock objects, rock scripts, and rock settings were not modified.",
                player);
        }

        private static GameObject BuildPlayer(
            Camera mainCamera,
            Material wood,
            Material metal)
        {
            Transform cameraTransform = mainCamera.transform;
            Vector3 cameraWorldPosition = cameraTransform.position;
            Vector3 cameraEuler = cameraTransform.eulerAngles;
            float groundY = FindGroundHeight(cameraWorldPosition);
            float eyeHeight = Mathf.Clamp(cameraWorldPosition.y - groundY, 1.1f, 1.75f);

            GameObject player = new GameObject(PlayerName);
            Undo.RegisterCreatedObjectUndo(player, "Create Born to Dig FPS character");
            player.transform.position = new Vector3(cameraWorldPosition.x, groundY + 0.02f, cameraWorldPosition.z);
            player.transform.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);

            CharacterController collider = player.AddComponent<CharacterController>();
            collider.height = Mathf.Max(1.58f, eyeHeight + 0.13f);
            collider.radius = 0.34f;
            collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
            collider.stepOffset = 0.28f;
            collider.slopeLimit = 50f;
            collider.skinWidth = 0.055f;

            Transform cameraPivot = CreateEmpty("CameraPivot", player.transform, new Vector3(0f, eyeHeight, 0f));
            Undo.SetTransformParent(cameraTransform, cameraPivot, "Attach existing camera to FPS character");
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.Euler(NormalizeAngle(cameraEuler.x), 0f, 0f);
            mainCamera.gameObject.tag = "MainCamera";
            DisableKnownFlyCameraController(mainCamera.gameObject);

            FpsCharacterController fpsController = player.AddComponent<FpsCharacterController>();
            fpsController.Configure(mainCamera);

            Transform modelRoot = CreateEmpty("CharacterModelRoot_DropDwarfHereLater", player.transform, Vector3.zero);
            DwarfVisualSlot visualSlot = player.AddComponent<DwarfVisualSlot>();
            visualSlot.ConfigureGeneratedReferences(modelRoot, null);

            Transform pickaxeRoot = CreateEmpty(
                "PickaxeViewModel",
                cameraTransform,
                new Vector3(0.45f, -0.37f, 0.78f));
            pickaxeRoot.localRotation = Quaternion.Euler(8f, -8f, -12f);

            GameObject handle = CreateVisualPart(
                "Handle",
                pickaxeRoot,
                new Vector3(0f, -0.22f, 0f),
                new Vector3(0.075f, 0.78f, 0.075f),
                wood);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);

            CreateVisualPart(
                "Metal Head",
                pickaxeRoot,
                new Vector3(0.015f, 0.19f, 0f),
                new Vector3(0.52f, 0.12f, 0.13f),
                metal);
            GameObject leftTip = CreateVisualPart(
                "Left Tip",
                pickaxeRoot,
                new Vector3(-0.31f, 0.19f, 0f),
                new Vector3(0.22f, 0.075f, 0.09f),
                metal);
            leftTip.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            PickaxeViewModel pickaxe = player.AddComponent<PickaxeViewModel>();
            pickaxe.Configure(fpsController, pickaxeRoot);

            player.AddComponent<CharacterMvpHud>();

            return player;
        }

        private static GameObject CreateVisualPart(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Object.DestroyImmediate(partCollider);

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        private static Transform CreateEmpty(string objectName, Transform parent, Vector3 localPosition)
        {
            GameObject childObject = new GameObject(objectName);
            Transform child = childObject.transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static float FindGroundHeight(Vector3 cameraPosition)
        {
            GameObject plane = GameObject.Find("Plane");
            if (plane != null)
            {
                Collider planeCollider = plane.GetComponent<Collider>();
                if (planeCollider != null) return planeCollider.bounds.max.y;

                Renderer planeRenderer = plane.GetComponent<Renderer>();
                if (planeRenderer != null) return planeRenderer.bounds.max.y;
            }

            if (Physics.Raycast(cameraPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 100f))
            {
                return hit.point.y;
            }

            return 0f;
        }

        private static void DisableKnownFlyCameraController(GameObject cameraObject)
        {
            MonoBehaviour[] behaviours = cameraObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null && behaviour.GetType().Name == "FlyCameraController")
                {
                    Undo.RecordObject(behaviour, "Disable old fly camera controller");
                    behaviour.enabled = false;
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }

        private static Material GetOrCreateMaterial(
            string fileName,
            Color color,
            float metallic,
            float smoothness)
        {
            string path = MaterialFolder + "/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (shader == null)
            {
                throw new System.InvalidOperationException("No compatible Lit shader was found.");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];

            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
