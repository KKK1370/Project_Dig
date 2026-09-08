#if UNITY_EDITOR
using System;
using System.IO;
using BornToDig.GoldMVP;
using BornToDig.VoxelMining;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BornToDig.EditorTools
{
    public static class GoldNuggetMvpInstaller
    {
        private const string RootFolder = "Assets/BornToDig/GoldNuggetMVP";
        private const string ModelPath = RootFolder + "/Models/GoldNugget_MVP.fbx";
        private const string JapaneseFontPath = RootFolder + "/Fonts/NotoSansJP-VF.ttf";
        private const string MaterialPath = RootFolder + "/Materials/GoldNugget_MVP.mat";
        private const string PrefabPath = RootFolder + "/Prefabs/GoldNugget_MVP.prefab";
        private const string ScenePath = "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";

        [MenuItem("Tools/BORN TO DIG/Install Gold Nugget MVP")]
        public static void InstallFromMenu()
        {
            InstallBatch();
            EditorUtility.DisplayDialog(
                "BORN TO DIG",
                "GoldNugget_MVP, pickup UI, and clear loop were installed in VoxelRockMVP.",
                "OK");
        }

        public static void InstallBatch()
        {
            EnsureTmpEssentialResources();
            ConfigureModelImporter();
            Material material = GetOrCreateGoldMaterial();
            GameObject prefab = CreateOrUpdatePrefab(material);
            InstallIntoScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GOLD_NUGGET_MVP_INSTALL_PASS");
        }

        private static void EnsureTmpEssentialResources()
        {
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null)
            {
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                        typeof(TMP_Settings).Assembly);
                if (package == null)
                {
                    throw new InvalidOperationException("Could not locate the TextMeshPro package.");
                }

                string essentialPackage = Path.Combine(
                    package.resolvedPath,
                    "Package Resources",
                    "TMP Essential Resources.unitypackage");
                if (!File.Exists(essentialPackage))
                {
                    throw new FileNotFoundException(
                        "TextMeshPro Essential Resources package was not found.",
                        essentialPackage);
                }

                AssetDatabase.ImportPackage(essentialPackage, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null)
            {
                throw new InvalidOperationException(
                    "TextMeshPro Essential Resources could not be imported.");
            }
        }

        private static void ConfigureModelImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Gold model was not imported at {ModelPath}");
            }

            bool changed = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }

            if (importer.importCameras)
            {
                importer.importCameras = false;
                changed = true;
            }

            if (importer.importLights)
            {
                importer.importLights = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material GetOrCreateGoldMaterial()
        {
            EnsureFolder(RootFolder + "/Materials");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("A compatible Lit shader was not found.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "GoldNugget_MVP" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Color gold = new Color(1f, 0.56f, 0.055f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
            if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.9f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.58f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrUpdatePrefab(Material material)
        {
            EnsureFolder(RootFolder + "/Prefabs");
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
            {
                throw new InvalidOperationException("Gold FBX does not contain a GameObject.");
            }

            GameObject root = new GameObject("GoldNugget_MVP");
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            try
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                if (visual == null)
                {
                    visual = UnityEngine.Object.Instantiate(modelPrefab);
                }

                visual.name = "GoldNugget_Visual";
                visual.transform.SetParent(root.transform, false);
                SetLayerRecursively(visual, root.layer);

                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException("Gold model does not contain a MeshRenderer.");
                }

                Bounds initialBounds = CalculateMeshBoundsInRoot(root.transform, visual);
                float longestDimension = Mathf.Max(
                    initialBounds.size.x,
                    Mathf.Max(initialBounds.size.y, initialBounds.size.z));
                float scale = 0.52f / Mathf.Max(longestDimension, 0.0001f);
                visual.transform.localScale *= scale;

                Bounds centeredBounds = CalculateMeshBoundsInRoot(root.transform, visual);
                visual.transform.localPosition -= centeredBounds.center;
                centeredBounds = CalculateMeshBoundsInRoot(root.transform, visual);

                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = material;
                }

                BoxCollider boxCollider = root.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.center = root.transform.InverseTransformPoint(centeredBounds.center);
                boxCollider.size = centeredBounds.size + Vector3.one * 0.025f;

                GoldNuggetMVP nugget = root.AddComponent<GoldNuggetMVP>();
                nugget.Configure(null, null, boxCollider);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException("Failed to save GoldNugget_MVP prefab.");
                }

                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void InstallIntoScene(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VoxelRock rock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();
            Camera camera = Camera.main;
            if (rock == null || camera == null)
            {
                throw new InvalidOperationException("VoxelRockMVP needs a VoxelRock and Main Camera.");
            }

            DestroySceneObjectIfPresent("GoldNugget_MVP");
            DestroySceneObjectIfPresent("MVP_GameManager");
            DestroySceneObjectIfPresent("MVP_UI");

            GameObject nuggetObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (nuggetObject == null)
            {
                throw new InvalidOperationException("Failed to instantiate GoldNugget_MVP.");
            }

            nuggetObject.name = "GoldNugget_MVP";
            Bounds rockBounds = FindRockBounds(rock.gameObject);
            nuggetObject.transform.position = ChooseNuggetPosition(rockBounds, camera);
            nuggetObject.transform.rotation = Quaternion.Euler(12f, 28f, -8f);

            GoldNuggetMVP nugget = nuggetObject.GetComponent<GoldNuggetMVP>();
            Collider pickupCollider = nuggetObject.GetComponent<Collider>();
            nugget.Configure(rock, camera, pickupCollider, 0.5f, 0.07f, 2.75f);

            MVPUI ui = CreateMvpUI();
            GameObject managerObject = new GameObject("MVP_GameManager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            MVPGameManager manager = managerObject.AddComponent<MVPGameManager>();
            manager.Configure(nugget, ui, 0.75f);

            EditorUtility.SetDirty(nugget);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save VoxelRockMVP after gold installation.");
            }

            Selection.activeGameObject = nuggetObject;
            Debug.Log(
                $"GoldNugget_MVP placed at world {nuggetObject.transform.position}, " +
                $"rock bounds {rockBounds}.");
        }

        private static MVPUI CreateMvpUI()
        {
            GameObject canvasObject = new GameObject(
                "MVP_UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_Text objective = CreateText(
                "ObjectiveText",
                canvasObject.transform,
                string.Empty,
                32f,
                TextAlignmentOptions.TopLeft,
                new Color(1f, 0.82f, 0.25f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(38f, -32f),
                new Vector2(720f, 60f));

            TMP_Text pickupPrompt = CreateText(
                "PickupPrompt",
                canvasObject.transform,
                string.Empty,
                36f,
                TextAlignmentOptions.Center,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -92f),
                new Vector2(640f, 70f));

            GameObject clearPanel = new GameObject(
                "ClearPanel",
                typeof(RectTransform),
                typeof(Image));
            clearPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = clearPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = clearPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.42f);
            panelImage.raycastTarget = false;

            TMP_Text clearTitle = CreateText(
                "ClearTitle",
                clearPanel.transform,
                "MVP CLEAR",
                76f,
                TextAlignmentOptions.Center,
                new Color(1f, 0.72f, 0.12f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 55f),
                new Vector2(900f, 110f));
            clearTitle.fontStyle = FontStyles.Bold;

            TMP_Text clearSubtitle = CreateText(
                "ClearSubtitle",
                clearPanel.transform,
                string.Empty,
                38f,
                TextAlignmentOptions.Center,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -38f),
                new Vector2(900f, 80f));

            MVPUI ui = canvasObject.AddComponent<MVPUI>();
            Font japaneseFont = AssetDatabase.LoadAssetAtPath<Font>(JapaneseFontPath);
            if (japaneseFont == null)
            {
                throw new InvalidOperationException(
                    $"Japanese UI font was not imported at {JapaneseFontPath}");
            }

            ui.Configure(
                objective,
                pickupPrompt,
                clearPanel,
                clearTitle,
                clearSubtitle,
                japaneseFont);
            pickupPrompt.gameObject.SetActive(false);
            clearPanel.SetActive(false);
            return ui;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            string content,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static Bounds FindRockBounds(GameObject rock)
        {
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(rock.transform.position, Vector3.one * 4f);
            }

            return CombineBounds(renderers);
        }

        private static Bounds CombineBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Bounds CalculateMeshBoundsInRoot(Transform root, GameObject visual)
        {
            MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;
            Bounds combined = default;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Bounds meshBounds = mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 meshPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 rootPoint = root.InverseTransformPoint(
                        filters[i].transform.TransformPoint(meshPoint));

                    if (!hasBounds)
                    {
                        combined = new Bounds(rootPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(rootPoint);
                    }
                }
            }

            if (!hasBounds)
            {
                throw new InvalidOperationException("Gold model does not contain a readable mesh bounds.");
            }

            return combined;
        }

        private static Vector3 ChooseNuggetPosition(Bounds rockBounds, Camera camera)
        {
            Vector3 center = rockBounds.center;
            Vector3 outward = camera.transform.position - center;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f)
            {
                outward = Vector3.back;
            }

            outward.Normalize();
            float boundaryDistance = DistanceToBoundsEdge(rockBounds.extents, outward);
            float depth = Mathf.Min(1.65f, boundaryDistance * 0.48f);
            Vector3 position = center + outward * (boundaryDistance - depth);
            position.y = Mathf.Clamp(
                camera.transform.position.y - 0.08f,
                rockBounds.min.y + 0.65f,
                rockBounds.max.y - 0.65f);
            return position;
        }

        private static float DistanceToBoundsEdge(Vector3 extents, Vector3 direction)
        {
            float distance = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > 0.0001f)
                distance = Mathf.Min(distance, extents.x / Mathf.Abs(direction.x));
            if (Mathf.Abs(direction.y) > 0.0001f)
                distance = Mathf.Min(distance, extents.y / Mathf.Abs(direction.y));
            if (Mathf.Abs(direction.z) > 0.0001f)
                distance = Mathf.Min(distance, extents.z / Mathf.Abs(direction.z));
            return float.IsInfinity(distance) ? extents.magnitude : distance;
        }

        private static void DestroySceneObjectIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
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
