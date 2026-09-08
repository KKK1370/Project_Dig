using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    /// <summary>
    /// Builds a display-only scene from project prefab assets. Source prefabs and
    /// source materials are never edited; any shader repair is a gallery-owned
    /// material copy assigned only as an instance override.
    /// </summary>
    public static class PrefabGalleryBuilder
    {
        private const string GalleryRoot = "Assets/BornToDig/PrefabGallery";
        private const string SceneFolder = GalleryRoot + "/Scenes";
        private const string ScenePath = SceneFolder + "/PrefabGallery.unity";
        private const string MaterialFolder = GalleryRoot + "/GalleryMaterials";
        private const string ReportFolder = GalleryRoot + "/Reports";
        private const string SummaryPath = ReportFolder + "/PrefabGallerySummary.json";
        private const string PrefabReportPath = ReportFolder + "/PrefabGalleryPrefabs.csv";
        private const string MaterialReportPath = ReportFolder + "/PrefabGalleryMaterials.csv";
        private const string Area01Path = "Assets/BornToDig/Maps/Map01/Area01/Scenes/Map01_Area01.unity";
        private const string PlayerPrefabPath = "Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab";

        private const float RowWidth = 450f;
        private const float CategoryGap = 42f;
        private const float ItemGap = 3f;
        private const float RowGap = 4f;

        private static readonly string[] CategoryOrder =
        {
            "Vegetation", "Rocks", "Mining", "Props", "Furniture",
            "Buildings", "LightsFX", "Decorations", "Other"
        };

        private static readonly List<PrefabRecord> PrefabRecords = new List<PrefabRecord>();
        private static readonly List<MaterialRecord> MaterialRecords = new List<MaterialRecord>();
        private static readonly Dictionary<string, Material> GalleryMaterialCache = new Dictionary<string, Material>();

        [MenuItem("BORN TO DIG/Build Prefab Gallery")]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Prefab Gallery",
                    "PrefabGallery の自動生成物を再作成します。元Prefab、元Material、Map01_Area01は変更しません。続行しますか？",
                    "Build", "Cancel"))
            {
                return;
            }

            BuildInternal(false);
        }

        /// <summary>Batch entry point: -executeMethod BornToDig.EditorTools.PrefabGalleryBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            BuildInternal(true);
        }

        [MenuItem("BORN TO DIG/Validate Prefab Gallery")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        /// <summary>Batch entry point: -executeMethod BornToDig.EditorTools.PrefabGalleryBuilder.ValidateBatch</summary>
        public static void ValidateBatch()
        {
            ValidateScene(false);
        }

        private static void BuildInternal(bool batchMode)
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (!batchMode && currentScene.IsValid() && currentScene.isDirty)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Gallery",
                    "現在のSceneに未保存変更があります。誤保存・破棄を避けるため生成を中止しました。先に現在のSceneを安全に保存または閉じてください。",
                    "OK");
                return;
            }

            PrefabRecords.Clear();
            MaterialRecords.Clear();
            GalleryMaterialCache.Clear();

            EnsureFolder(GalleryRoot);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ReportFolder);

            string areaHashBefore = FileSha256(Area01Path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject galleryRoot = new GameObject("PrefabGallery");
            GameObject environment = CreateChild("GalleryEnvironment", galleryRoot.transform);
            GameObject groundRoot = CreateChild("Ground", environment.transform);
            GameObject lightingRoot = CreateChild("Lighting", environment.transform);
            GameObject playerRoot = CreateChild("Player", galleryRoot.transform);
            GameObject categoriesRoot = CreateChild("Categories", galleryRoot.transform);
            GameObject labelsRoot = CreateChild("GalleryLabels", galleryRoot.transform);

            ConfigureSceneLighting(lightingRoot.transform);
            CreatePlayer(playerRoot.transform);
            CreateSpawnPad(groundRoot.transform);

            List<Candidate> candidates = CollectCandidates();
            Dictionary<string, List<Candidate>> grouped = candidates
                .GroupBy(candidate => candidate.Category)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToList());

            int placed = 0;
            float nextCategoryZ = 18f;
            Dictionary<string, int> categoryCounts = CategoryOrder.ToDictionary(category => category, category => 0);
            Dictionary<string, int> subcategoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            try
            {
                foreach (string category in CategoryOrder)
                {
                    List<Candidate> categoryItems;
                    if (!grouped.TryGetValue(category, out categoryItems) || categoryItems.Count == 0)
                    {
                        continue;
                    }

                    GameObject categoryRoot = CreateChild(category, categoriesRoot.transform);
                    GameObject categoryLabels = CreateChild(category, labelsRoot.transform);
                    Dictionary<string, Transform> subcategoryRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);

                    float categoryStartZ = nextCategoryZ;
                    float cursorX = 0f;
                    float cursorZ = categoryStartZ;
                    float rowDepth = 0f;
                    float maxX = 0f;
                    float maxZ = categoryStartZ;

                    CreateTextLabel(category + "Label", category, new Vector3(0f, 3f, categoryStartZ - 9f), 2.1f,
                        categoryLabels.transform, TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.35f));

                    foreach (Candidate candidate in categoryItems)
                    {
                        GameObject instance = PrefabUtility.InstantiatePrefab(candidate.Asset, scene) as GameObject;
                        if (instance == null)
                        {
                            Exclude(candidate.Path, candidate.Name, "instantiate_failed", string.Empty);
                            continue;
                        }

                        instance.name = candidate.Name;
                        DisableDisplayInterference(instance);
                        ApplyGalleryMaterialRepairs(instance, candidate.Path);

                        Bounds bounds;
                        if (!TryGetRenderableBounds(instance, out bounds) || !IsFinite(bounds))
                        {
                            UnityEngine.Object.DestroyImmediate(instance);
                            Exclude(candidate.Path, candidate.Name, "invalid_or_empty_render_bounds", string.Empty);
                            continue;
                        }

                        float footprintWidth = Mathf.Max(4f, bounds.size.x + ItemGap);
                        float footprintDepth = Mathf.Max(4f, bounds.size.z + ItemGap + 1f);
                        if (cursorX > 0f && cursorX + footprintWidth > RowWidth)
                        {
                            cursorX = 0f;
                            cursorZ += rowDepth + RowGap;
                            rowDepth = 0f;
                        }

                        Transform subcategoryRoot;
                        if (!subcategoryRoots.TryGetValue(candidate.Subcategory, out subcategoryRoot))
                        {
                            subcategoryRoot = CreateChild(candidate.Subcategory, categoryRoot.transform).transform;
                            subcategoryRoots.Add(candidate.Subcategory, subcategoryRoot);
                        }

                        instance.transform.SetParent(subcategoryRoot, true);
                        Vector3 placement = new Vector3(
                            cursorX + ItemGap * 0.5f - bounds.min.x,
                            -bounds.min.y,
                            cursorZ + ItemGap * 0.5f - bounds.min.z);
                        instance.transform.position = placement;

                        Bounds placedBounds;
                        TryGetRenderableBounds(instance, out placedBounds);
                        float labelY = placedBounds.min.y + Mathf.Clamp(placedBounds.size.y * 0.18f, 0.55f, 2.4f);
                        float labelZ = placedBounds.min.z - 0.65f;
                        CreateTextLabel(
                            "Label_" + candidate.Name,
                            candidate.Name,
                            new Vector3(placedBounds.center.x, labelY, labelZ),
                            Mathf.Clamp(Mathf.Max(0.22f, placedBounds.size.x * 0.035f), 0.22f, 0.55f),
                            categoryLabels.transform,
                            TextAlignmentOptions.Center,
                            Color.white);

                        PrefabRecords.Add(new PrefabRecord
                        {
                            path = candidate.Path,
                            prefabName = candidate.Name,
                            category = candidate.Category,
                            subcategory = candidate.Subcategory,
                            status = "placed",
                            reason = string.Empty,
                            canonicalPath = string.Empty,
                            width = placedBounds.size.x,
                            height = placedBounds.size.y,
                            depth = placedBounds.size.z
                        });

                        placed++;
                        categoryCounts[category]++;
                        string subKey = category + "/" + candidate.Subcategory;
                        subcategoryCounts[subKey] = subcategoryCounts.ContainsKey(subKey) ? subcategoryCounts[subKey] + 1 : 1;
                        cursorX += footprintWidth;
                        rowDepth = Mathf.Max(rowDepth, footprintDepth);
                        maxX = Mathf.Max(maxX, cursorX);
                        maxZ = Mathf.Max(maxZ, cursorZ + rowDepth);

                        if (placed % 100 == 0)
                        {
                            Debug.Log("PREFAB_GALLERY_PROGRESS placed=" + placed + " current=" + candidate.Path);
                        }
                    }

                    if (categoryCounts[category] > 0)
                    {
                        CreateGroundSection(groundRoot.transform, category, maxX, categoryStartZ - 12f, maxZ + 6f);
                        nextCategoryZ = maxZ + CategoryGap;
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(categoryRoot);
                        UnityEngine.Object.DestroyImmediate(categoryLabels);
                    }
                }

                EditorSceneManager.SaveScene(scene, ScenePath, false);
                CleanupUnusedGalleryMaterials();
                AssetDatabase.SaveAssets();

                string areaHashAfter = FileSha256(Area01Path);
                bool areaUnchanged = !string.IsNullOrEmpty(areaHashBefore) && areaHashBefore == areaHashAfter;
                WriteReports(placed, categoryCounts, subcategoryCounts, areaHashBefore, areaHashAfter, areaUnchanged);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.SaveScene(scene, ScenePath, false);

                ValidateCurrentScene(placed, areaUnchanged);
                Debug.Log("PREFAB_GALLERY_BUILD_PASS scene=" + ScenePath + " placed=" + placed +
                          " excluded=" + PrefabRecords.Count(record => record.status == "excluded") +
                          " materialRepairs=" + MaterialRecords.Count(record => record.status == "gallery_copy_applied") +
                          " area01Unchanged=" + areaUnchanged);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (batchMode)
                {
                    EditorApplication.Exit(1);
                }
                throw;
            }
        }

        private static List<Candidate> CollectCandidates()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            List<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> visualSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
            List<Candidate> result = new List<Candidate>();

            foreach (string path in paths)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string prefabName = asset != null ? asset.name : Path.GetFileNameWithoutExtension(path);
                string reason;
                if (ShouldExclude(path, asset, out reason))
                {
                    Exclude(path, prefabName, reason, string.Empty);
                    continue;
                }

                string signature = BuildVisualSignature(asset, path);
                string canonical;
                if (!string.IsNullOrEmpty(signature) && visualSignatures.TryGetValue(signature, out canonical))
                {
                    Exclude(path, prefabName, "duplicate_static_visual", canonical);
                    continue;
                }

                if (!string.IsNullOrEmpty(signature))
                {
                    visualSignatures.Add(signature, path);
                }

                string category = Categorize(path, prefabName);
                result.Add(new Candidate
                {
                    Asset = asset,
                    Path = path,
                    Name = prefabName,
                    Category = category,
                    Subcategory = Subcategorize(category, path, prefabName)
                });
            }

            Debug.Log("PREFAB_GALLERY_SCAN found=" + paths.Count + " candidates=" + result.Count +
                      " preExcluded=" + PrefabRecords.Count(record => record.status == "excluded"));
            return result;
        }

        private static bool ShouldExclude(string path, GameObject asset, out string reason)
        {
            reason = string.Empty;
            if (asset == null)
            {
                reason = "asset_load_failed";
                return true;
            }

            string normalized = (path + "/" + asset.name).Replace('\\', '/').ToLowerInvariant();
            if (path.StartsWith(GalleryRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                reason = "gallery_generated_asset";
                return true;
            }

            if (path.Equals(PlayerPrefabPath, StringComparison.OrdinalIgnoreCase) ||
                ContainsAny(normalized, "/player/", "fps_player", "fps player", "charactercontroller", "firstperson"))
            {
                reason = "player_prefab";
                return true;
            }

            if (ContainsAny(normalized, "/ui/", "/editor/", "/debug/", "/test/", "/tests/", "/demo/", "/demos/", "/samples/", "/sample/"))
            {
                reason = normalized.Contains("/ui/") ? "ui_prefab" :
                    normalized.Contains("/editor/") ? "editor_or_system_prefab" :
                    normalized.Contains("/debug/") ? "debug_prefab" :
                    "test_or_sample_prefab";
                return true;
            }

            string lowerName = asset.name.ToLowerInvariant();
            if (ContainsAny(lowerName, "manager", "bootstrap", "eventsystem", "installer", "singleton", "spawner", "pooler", "gamecontroller"))
            {
                reason = "manager_or_system_prefab";
                return true;
            }

            if (ContainsAny(lowerName, "fragment", "fractured", "collision", "collider", "lod_part", "lodpart", "internal", "helper", "pivot", "socket"))
            {
                reason = "internal_part_prefab";
                return true;
            }

            if (asset.GetComponentInChildren<Canvas>(true) != null)
            {
                reason = "ui_prefab";
                return true;
            }

            int missingScripts = asset.GetComponentsInChildren<Transform>(true)
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            if (missingScripts > 0)
            {
                reason = "missing_script_prefab";
                return true;
            }

            Renderer[] renderers = asset.GetComponentsInChildren<Renderer>(true);
            bool hasLight = asset.GetComponentInChildren<Light>(true) != null;
            if ((renderers.Length == 0 || renderers.All(renderer => renderer == null || !HasDisplayContent(renderer))) && !hasLight)
            {
                reason = "no_3d_display_renderer";
                return true;
            }

            return false;
        }

        private static bool HasDisplayContent(Renderer renderer)
        {
            MeshRenderer meshRenderer = renderer as MeshRenderer;
            if (meshRenderer != null)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                return filter != null && filter.sharedMesh != null;
            }

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                return skinned.sharedMesh != null;
            }

            SpriteRenderer sprite = renderer as SpriteRenderer;
            if (sprite != null)
            {
                return sprite.sprite != null;
            }

            return renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer;
        }

        private static string BuildVisualSignature(GameObject asset, string path)
        {
            if (asset.GetComponentInChildren<ParticleSystem>(true) != null ||
                asset.GetComponentInChildren<Animator>(true) != null ||
                asset.GetComponentInChildren<Animation>(true) != null ||
                asset.GetComponentInChildren<Light>(true) != null)
            {
                return string.Empty;
            }

            Renderer[] renderers = asset.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && HasDisplayContent(renderer))
                .OrderBy(renderer => GetTransformPath(asset.transform, renderer.transform), StringComparer.Ordinal)
                .ToArray();
            if (renderers.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (Renderer renderer in renderers)
            {
                builder.Append(renderer.GetType().FullName).Append('|');
                AppendObjectId(builder, GetRendererAsset(renderer));
                foreach (Material material in renderer.sharedMaterials)
                {
                    AppendObjectId(builder, material);
                }

                Matrix4x4 matrix = asset.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int row = 0; row < 4; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        builder.Append(Mathf.Round(matrix[row, column] * 10000f) / 10000f).Append(',');
                    }
                }
            }

            return Sha256(builder.ToString());
        }

        private static UnityEngine.Object GetRendererAsset(Renderer renderer)
        {
            MeshRenderer meshRenderer = renderer as MeshRenderer;
            if (meshRenderer != null)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                return skinned.sharedMesh;
            }

            SpriteRenderer sprite = renderer as SpriteRenderer;
            return sprite != null ? sprite.sprite : null;
        }

        private static void AppendObjectId(StringBuilder builder, UnityEngine.Object value)
        {
            if (value == null)
            {
                builder.Append("null|");
                return;
            }

            string guid;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localId))
            {
                builder.Append(guid).Append(':').Append(localId).Append('|');
            }
            else
            {
                builder.Append(value.GetType().FullName).Append(':').Append(value.name).Append('|');
            }
        }

        private static string GetTransformPath(Transform root, Transform current)
        {
            List<int> indices = new List<int>();
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            indices.Reverse();
            return string.Join("/", indices);
        }

        private static string Categorize(string path, string prefabName)
        {
            string value = GetClassificationText(path, prefabName);
            if (ContainsAny(value, "tree", "bush", "grass", "flower", "mushroom", "vegetation", "foliage", "plant", "fern", "shrub", "cactus", "vine", "leaf", "leaves", "crop")) return "Vegetation";
            if (ContainsAny(value, "rock", "stone", "boulder", "cliff", "pebble", "crag", "mountain", "cave")) return "Rocks";
            if (ContainsAny(value, "mining", "mine_", "mine/", "pickaxe", "pick_axe", "ore", "minecart", "mine_cart", "workbench", "anvil", "forge", "smelter", "rail", "track")) return "Mining";
            if (ContainsAny(value, "chair", "table", "bed", "shelf", "cabinet", "throne", "desk", "bench", "stool", "sofa", "wardrobe", "furniture")) return "Furniture";
            if (ContainsAny(value, "building", "house", "castle", "tower", "bridge", "gate", "ruin", "structure", "roof", "stairs", "stair", "pillar", "column", "platform", "wall", "arch", "doorway", "tent", "hut")) return "Buildings";
            if (ContainsAny(value, "light", "torch", "lantern", "candle", "lamp", "fire", "flame", "smoke", "particle", "/fx", "vfx", "glow", "spark")) return "LightsFX";
            if (ContainsAny(value, "decor", "statue", "banner", "flag", "rug", "tapestry", "painting", "skull", "bone", "trophy", "fountain", "ornament")) return "Decorations";
            if (ContainsAny(value, "crate", "barrel", "fence", "sign", "tool", "prop", "weapon", "cart", "bucket", "chest", "box", "bag", "sack", "bottle", "pot", "vase")) return "Props";
            return "Other";
        }

        private static string Subcategorize(string category, string path, string prefabName)
        {
            string value = GetClassificationText(path, prefabName);
            if (category == "Vegetation")
            {
                if (ContainsAny(value, "tree", "trunk")) return "Trees";
                if (ContainsAny(value, "bush", "shrub")) return "Bushes";
                if (ContainsAny(value, "grass")) return "Grass";
                if (ContainsAny(value, "flower")) return "Flowers";
                if (ContainsAny(value, "mushroom")) return "Mushrooms";
                return "PlantsOther";
            }

            if (category == "Rocks")
            {
                if (ContainsAny(value, "cliff", "mountain", "crag")) return "Cliffs";
                if (ContainsAny(value, "pebble", "small", "tiny")) return "SmallRocksPebbles";
                if (ContainsAny(value, "large", "big", "giant", "massive")) return "LargeRocks";
                return "RocksOther";
            }

            if (category == "Mining")
            {
                if (ContainsAny(value, "pickaxe", "pick_axe")) return "Pickaxes";
                if (ContainsAny(value, "cart", "minecart", "mine_cart")) return "Carts";
                if (ContainsAny(value, "workbench", "anvil", "forge", "smelter")) return "Workbenches";
                if (ContainsAny(value, "ore")) return "OreProps";
                return "MiningProps";
            }

            if (category == "Props")
            {
                if (ContainsAny(value, "crate", "box")) return "Crates";
                if (ContainsAny(value, "barrel")) return "Barrels";
                if (ContainsAny(value, "fence")) return "Fences";
                if (ContainsAny(value, "sign")) return "Signs";
                if (ContainsAny(value, "lantern")) return "Lanterns";
                if (ContainsAny(value, "tool", "weapon")) return "Tools";
                return "PropsOther";
            }

            return category;
        }

        private static string GetClassificationText(string path, string prefabName)
        {
            string normalized = path.Replace('\\', '/').ToLowerInvariant();
            int prefabsIndex = normalized.LastIndexOf("/prefabs/", StringComparison.Ordinal);
            string relevantPath = prefabsIndex >= 0
                ? normalized.Substring(prefabsIndex + "/prefabs/".Length)
                : normalized;
            return relevantPath + "/" + prefabName.ToLowerInvariant();
        }

        private static void DisableDisplayInterference(GameObject instance)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }

            SetLayerRecursively(instance.transform, 2); // Ignore Raycast; project built-in layer.
        }

        private static void ApplyGalleryMaterialRepairs(GameObject instance, string prefabPath)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    string issue = DetectMaterialIssue(source, renderer);
                    if (string.IsNullOrEmpty(issue))
                    {
                        continue;
                    }

                    Material repaired = GetOrCreateGalleryMaterial(source, renderer, issue, prefabPath);
                    if (repaired != null)
                    {
                        materials[index] = repaired;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static string DetectMaterialIssue(Material material, Renderer renderer)
        {
            if (material == null) return "missing_material";
            if (material.shader == null) return "missing_shader";
            string shaderName = material.shader.name ?? string.Empty;
            if (shaderName.Equals("Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase)) return "error_shader";
            if (!material.shader.isSupported) return "unsupported_shader";
            if (shaderName.StartsWith("Legacy Shaders/", StringComparison.OrdinalIgnoreCase)) return "legacy_shader";
            if (shaderName.Equals("Standard", StringComparison.OrdinalIgnoreCase) || shaderName.StartsWith("Mobile/", StringComparison.OrdinalIgnoreCase)) return "built_in_shader";
            if (!(renderer is SpriteRenderer) && shaderName.IndexOf("Universal Render Pipeline/2D", StringComparison.OrdinalIgnoreCase) >= 0) return "urp_2d_shader_on_3d_renderer";
            return string.Empty;
        }

        private static Material GetOrCreateGalleryMaterial(Material source, Renderer renderer, string issue, string prefabPath)
        {
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            string sourceGuid = !string.IsNullOrEmpty(sourcePath) ? AssetDatabase.AssetPathToGUID(sourcePath) : "missing";
            string sourceName = source != null ? source.name : "MissingMaterial";
            string sourceShader = source != null && source.shader != null ? source.shader.name : "<missing>";
            bool particle = renderer is ParticleSystemRenderer;
            bool transparent = IsTransparentMaterial(source, sourceName, sourceShader, particle);
            bool doubleSided = ContainsAny((sourceName + " " + sourceShader).ToLowerInvariant(), "double", "leaf", "grass", "flower", "foliage", "cloth");
            string targetShaderName = particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit";
            string key = sourceGuid + "|" + targetShaderName + "|" + transparent + "|" + doubleSided;

            Material cached;
            if (GalleryMaterialCache.TryGetValue(key, out cached))
            {
                AddMaterialRecord(prefabPath, sourcePath, cached != null ? AssetDatabase.GetAssetPath(cached) : string.Empty,
                    sourceName, sourceShader, targetShaderName, issue, cached != null ? "gallery_copy_applied" : "repair_failed");
                return cached;
            }

            Shader targetShader = Shader.Find(targetShaderName);
            if (targetShader == null)
            {
                GalleryMaterialCache[key] = null;
                AddMaterialRecord(prefabPath, sourcePath, string.Empty, sourceName, sourceShader, targetShaderName, issue, "repair_failed_target_shader_missing");
                return null;
            }

            Texture baseTexture = GetFirstTexture(source, "_BaseMap", "_MainTex", "_BaseColorMap", "_Diffuse");
            Color baseColor = GetFirstColor(source, Color.white, "_BaseColor", "_Color");
            Color emissionColor = GetFirstColor(source, Color.black, "_EmissionColor", "_EmissiveColor");

            Material copy = source != null ? new Material(source) : new Material(targetShader);
            copy.shader = targetShader;
            copy.name = "Gallery_" + sourceName;
            SetTextureIfPresent(copy, "_BaseMap", baseTexture);
            SetColorIfPresent(copy, "_BaseColor", baseColor);
            if (emissionColor.maxColorComponent > 0.001f)
            {
                SetColorIfPresent(copy, "_EmissionColor", emissionColor);
                copy.EnableKeyword("_EMISSION");
            }

            if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", doubleSided ? 0f : 2f);
            if (copy.HasProperty("_Surface")) copy.SetFloat("_Surface", transparent ? 1f : 0f);
            if (copy.HasProperty("_Blend")) copy.SetFloat("_Blend", 0f);
            if (copy.HasProperty("_ZWrite")) copy.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (transparent)
            {
                copy.renderQueue = (int)RenderQueue.Transparent;
                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                copy.renderQueue = -1;
                copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            string fileName = SanitizeFileName(sourceName) + "_" + sourceGuid.Substring(0, Mathf.Min(8, sourceGuid.Length)) +
                              (particle ? "_Particle" : string.Empty) + (transparent ? "_Transparent" : string.Empty) + ".mat";
            string targetPath = MaterialFolder + "/" + fileName;
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                existing.name = copy.name;
                UnityEngine.Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(existing);
                copy = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(copy, targetPath);
            }
            GalleryMaterialCache[key] = copy;
            AddMaterialRecord(prefabPath, sourcePath, targetPath, sourceName, sourceShader, targetShaderName, issue, "gallery_copy_applied");
            return copy;
        }

        private static void CleanupUnusedGalleryMaterials()
        {
            HashSet<string> usedPaths = new HashSet<string>(
                GalleryMaterialCache.Values.Where(value => value != null).Select(AssetDatabase.GetAssetPath),
                StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!usedPaths.Contains(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static bool IsTransparentMaterial(Material material, string materialName, string shaderName, bool particle)
        {
            if (particle || ContainsAny((materialName + " " + shaderName).ToLowerInvariant(), "transparent", "glass", "smoke", "alpha", "fade")) return true;
            if (material == null) return false;
            if (material.renderQueue >= (int)RenderQueue.Transparent) return true;
            Color color = GetFirstColor(material, Color.white, "_BaseColor", "_Color");
            return color.a < 0.99f;
        }

        private static Texture GetFirstTexture(Material material, params string[] properties)
        {
            if (material == null) return null;
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null) return texture;
                }
            }
            return null;
        }

        private static Color GetFirstColor(Material material, Color fallback, params string[] properties)
        {
            if (material == null) return fallback;
            foreach (string property in properties)
            {
                if (material.HasProperty(property)) return material.GetColor(property);
            }
            return fallback;
        }

        private static void SetTextureIfPresent(Material material, string property, Texture value)
        {
            if (value != null && material.HasProperty(property)) material.SetTexture(property, value);
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void AddMaterialRecord(string prefabPath, string sourcePath, string galleryPath, string materialName,
            string sourceShader, string targetShader, string issue, string status)
        {
            MaterialRecords.Add(new MaterialRecord
            {
                prefabPath = prefabPath,
                sourceMaterialPath = sourcePath,
                galleryMaterialPath = galleryPath,
                materialName = materialName,
                sourceShader = sourceShader,
                targetShader = targetShader,
                issue = issue,
                status = status
            });
        }

        private static void ConfigureSceneLighting(Transform parent)
        {
            GameObject lightObject = CreateChild("Directional Light", parent);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.956f, 0.89f);
            light.intensity = 1.12f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.76f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.52f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.25f);
            RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.reflectionIntensity = 0.75f;
            RenderSettings.fog = false;
        }

        private static void CreatePlayer(Transform parent)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerAsset == null)
            {
                Debug.LogWarning("PREFAB_GALLERY_PLAYER_SKIPPED missing=" + PlayerPrefabPath);
                return;
            }

            GameObject player = PrefabUtility.InstantiatePrefab(playerAsset) as GameObject;
            if (player == null) return;
            player.name = "Area01_FPS_Player";
            player.transform.SetParent(parent, true);
            player.transform.position = new Vector3(0f, 0.15f, 0f);
            player.transform.rotation = Quaternion.identity;
        }

        private static void CreateGroundSection(Transform parent, string category, float maxX, float minZ, float maxZ)
        {
            float width = Mathf.Max(20f, maxX + 8f);
            float depth = Mathf.Max(20f, maxZ - minZ + 8f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = category + "_Ground";
            ground.transform.SetParent(parent, true);
            ground.transform.position = new Vector3(width * 0.5f - 2f, -0.25f, (minZ + maxZ) * 0.5f);
            ground.transform.localScale = new Vector3(width, 0.5f, depth);
            ground.layer = 0;
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private static void CreateSpawnPad(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Spawn_Ground";
            ground.transform.SetParent(parent, true);
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(24f, 0.5f, 24f);
            ground.layer = 0;
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private static TextMeshPro CreateTextLabel(string objectName, string text, Vector3 position, float scale,
            Transform parent, TextAlignmentOptions alignment, Color color)
        {
            GameObject labelObject = CreateChild(objectName, parent);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            labelObject.transform.localScale = Vector3.one * scale;
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.alignment = alignment;
            label.color = color;
            label.fontSize = 3.4f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1f;
            label.fontSizeMax = 3.4f;
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, text.Length * 0.65f), 2.2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }
            return label;
        }

        private static bool TryGetRenderableBounds(GameObject instance, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && HasDisplayContent(renderer)).ToArray();
            if (renderers.Length == 0)
            {
                return TryGetEffectBounds(instance, out bounds);
            }

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.sqrMagnitude > 0.000001f) return true;
            return TryGetEffectBounds(instance, out bounds);
        }

        private static bool TryGetEffectBounds(GameObject instance, out Bounds bounds)
        {
            bool initialized = false;
            bounds = default(Bounds);
            foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particleSystem.main;
                float reach = Mathf.Clamp(
                    main.startSize.constantMax + main.startSpeed.constantMax * main.startLifetime.constantMax,
                    1.5f, 15f);
                Bounds particleBounds = new Bounds(particleSystem.transform.position, Vector3.one * (reach * 2f));
                if (!initialized)
                {
                    bounds = particleBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(particleBounds);
                }
            }

            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                float horizontal = light.type == LightType.Directional ? 2f : Mathf.Clamp(light.range * 0.25f, 1f, 6f);
                Bounds lightBounds = new Bounds(light.transform.position, new Vector3(horizontal, 3f, horizontal));
                if (!initialized)
                {
                    bounds = lightBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(lightBounds);
                }
            }

            if (!initialized && (instance.GetComponentInChildren<TrailRenderer>(true) != null ||
                                 instance.GetComponentInChildren<LineRenderer>(true) != null))
            {
                bounds = new Bounds(instance.transform.position, Vector3.one * 4f);
                initialized = true;
            }
            return initialized;
        }

        private static bool IsFinite(Bounds bounds)
        {
            Vector3[] values = { bounds.center, bounds.size, bounds.min, bounds.max };
            return values.All(value => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) < 100000f;
        }

        private static void ValidateScene(bool showDialog)
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("PREFAB_GALLERY_VALIDATE_FAIL scene_missing=" + ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject categories = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == "Categories")?.gameObject;
            int placed = categories == null ? 0 : CountPlacedPrefabRoots(categories.transform);
            ValidateCurrentScene(placed, true);
            if (showDialog) EditorUtility.DisplayDialog("Prefab Gallery", "検証ログをConsoleへ出力しました。", "OK");
        }

        private static void ValidateCurrentScene(int expectedPlaced, bool areaUnchanged)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "PrefabGallery");
            if (root == null) throw new InvalidOperationException("PrefabGallery root is missing.");

            int missingScripts = 0;
            int cameras = 0;
            int listeners = 0;
            int directionalLights = 0;
            int missingMaterialSlots = 0;
            int invalidShaders = 0;
            foreach (GameObject gameObject in scene.GetRootGameObjects().SelectMany(rootObject => rootObject.GetComponentsInChildren<Transform>(true)).Select(transform => transform.gameObject))
            {
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                cameras += gameObject.GetComponents<Camera>().Count(camera => camera != null && camera.enabled);
                listeners += gameObject.GetComponents<AudioListener>().Count(listener => listener != null && listener.enabled);
                directionalLights += gameObject.GetComponents<Light>().Count(light => light != null && light.enabled && light.type == LightType.Directional);
                foreach (Renderer renderer in gameObject.GetComponents<Renderer>())
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            missingMaterialSlots++;
                        }
                        else if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
                        {
                            invalidShaders++;
                        }
                    }
                }
            }

            Transform categories = root.transform.Find("Categories");
            Transform labels = root.transform.Find("GalleryLabels");
            Transform galleryDirectionalTransform = root.transform.Find("GalleryEnvironment/Lighting/Directional Light");
            Light galleryDirectional = galleryDirectionalTransform != null ? galleryDirectionalTransform.GetComponent<Light>() : null;
            int actualPlaced = CountPlacedPrefabRoots(categories);
            int enabledDisplayColliders = categories == null ? 0 : categories.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled);
            int labelCount = labels == null ? 0 : labels.GetComponentsInChildren<TextMeshPro>(true).Length;

            if (missingScripts != 0) throw new InvalidOperationException("Missing scripts: " + missingScripts);
            if (cameras != 1) throw new InvalidOperationException("Expected one enabled Camera, found " + cameras);
            if (listeners != 1) throw new InvalidOperationException("Expected one enabled AudioListener, found " + listeners);
            if (galleryDirectional == null || !galleryDirectional.enabled || galleryDirectional.type != LightType.Directional)
                throw new InvalidOperationException("Gallery reference Directional Light is missing or disabled.");
            if (expectedPlaced > 0 && actualPlaced != expectedPlaced)
                throw new InvalidOperationException("Placed prefab count mismatch. expected=" + expectedPlaced + " actual=" + actualPlaced);
            if (enabledDisplayColliders != 0) throw new InvalidOperationException("Enabled display colliders: " + enabledDisplayColliders);
            if (labelCount < actualPlaced) throw new InvalidOperationException("Missing prefab labels. labels=" + labelCount + " prefabs=" + actualPlaced);
            if (missingMaterialSlots != 0) throw new InvalidOperationException("Missing material slots: " + missingMaterialSlots);
            if (invalidShaders != 0) throw new InvalidOperationException("Invalid or unsupported shaders: " + invalidShaders);
            if (!areaUnchanged) throw new InvalidOperationException("Map01_Area01 hash changed during gallery build.");

            Debug.Log("PREFAB_GALLERY_VALIDATE_PASS scene=" + scene.path + " expectedPlaced=" + expectedPlaced +
                      " actualPlaced=" + actualPlaced + " labels=" + labelCount + " cameras=" + cameras +
                      " listeners=" + listeners + " directionalLights=" + directionalLights +
                      " enabledDisplayColliders=" + enabledDisplayColliders + " missingScripts=" + missingScripts +
                      " missingMaterialSlots=" + missingMaterialSlots + " invalidShaders=" + invalidShaders);
        }

        private static void WriteReports(int placed, Dictionary<string, int> categoryCounts,
            Dictionary<string, int> subcategoryCounts, string areaHashBefore, string areaHashAfter, bool areaUnchanged)
        {
            GallerySummary summary = new GallerySummary
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                scenePath = ScenePath,
                playerPrefabPath = PlayerPrefabPath,
                scannedPrefabCount = PrefabRecords.Count,
                placedPrefabCount = placed,
                excludedPrefabCount = PrefabRecords.Count(record => record.status == "excluded"),
                galleryMaterialRepairUseCount = MaterialRecords.Count(record => record.status == "gallery_copy_applied"),
                uniqueGalleryMaterialCount = GalleryMaterialCache.Values.Where(value => value != null).Distinct().Count(),
                map01Area01Path = Area01Path,
                map01Area01Sha256Before = areaHashBefore,
                map01Area01Sha256After = areaHashAfter,
                map01Area01Unchanged = areaUnchanged,
                categories = categoryCounts.Select(pair => new CountRecord { name = pair.Key, count = pair.Value }).ToList(),
                subcategories = subcategoryCounts.OrderBy(pair => pair.Key).Select(pair => new CountRecord { name = pair.Key, count = pair.Value }).ToList(),
                exclusionReasons = PrefabRecords.Where(record => record.status == "excluded")
                    .GroupBy(record => record.reason).OrderBy(group => group.Key)
                    .Select(group => new CountRecord { name = group.Key, count = group.Count() }).ToList()
            };

            File.WriteAllText(SummaryPath, JsonUtility.ToJson(summary, true), new UTF8Encoding(false));
            File.WriteAllText(PrefabReportPath,
                "status,reason,category,subcategory,prefab_name,prefab_path,canonical_path,width,height,depth\n" +
                string.Join("\n", PrefabRecords.OrderBy(record => record.status).ThenBy(record => record.path).Select(record => string.Join(",", new[]
                {
                    Csv(record.status), Csv(record.reason), Csv(record.category), Csv(record.subcategory), Csv(record.prefabName),
                    Csv(record.path), Csv(record.canonicalPath), FloatCsv(record.width), FloatCsv(record.height), FloatCsv(record.depth)
                }))), new UTF8Encoding(false));

            File.WriteAllText(MaterialReportPath,
                "status,issue,prefab_path,source_material_path,gallery_material_path,material_name,source_shader,target_shader\n" +
                string.Join("\n", MaterialRecords.OrderBy(record => record.sourceMaterialPath).ThenBy(record => record.prefabPath).Select(record => string.Join(",", new[]
                {
                    Csv(record.status), Csv(record.issue), Csv(record.prefabPath), Csv(record.sourceMaterialPath),
                    Csv(record.galleryMaterialPath), Csv(record.materialName), Csv(record.sourceShader), Csv(record.targetShader)
                }))), new UTF8Encoding(false));
        }

        private static void Exclude(string path, string prefabName, string reason, string canonicalPath)
        {
            PrefabRecords.Add(new PrefabRecord
            {
                path = path,
                prefabName = prefabName,
                status = "excluded",
                reason = reason,
                canonicalPath = canonicalPath,
                category = string.Empty,
                subcategory = string.Empty
            });
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static int CountPlacedPrefabRoots(Transform categories)
        {
            if (categories == null) return 0;
            int count = 0;
            for (int categoryIndex = 0; categoryIndex < categories.childCount; categoryIndex++)
            {
                Transform category = categories.GetChild(categoryIndex);
                for (int subcategoryIndex = 0; subcategoryIndex < category.childCount; subcategoryIndex++)
                {
                    count += category.GetChild(subcategoryIndex).childCount;
                }
            }
            return count;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int index = 0; index < root.childCount; index++) SetLayerRecursively(root.GetChild(index), layer);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string FileSha256(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath)) return string.Empty;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(absolutePath))
            {
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string Sha256(string text)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
            }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FloatCsv(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(name) ? "Material" : name;
        }

        private sealed class Candidate
        {
            public GameObject Asset;
            public string Path;
            public string Name;
            public string Category;
            public string Subcategory;
        }

        [Serializable]
        private sealed class PrefabRecord
        {
            public string path;
            public string prefabName;
            public string category;
            public string subcategory;
            public string status;
            public string reason;
            public string canonicalPath;
            public float width;
            public float height;
            public float depth;
        }

        [Serializable]
        private sealed class MaterialRecord
        {
            public string prefabPath;
            public string sourceMaterialPath;
            public string galleryMaterialPath;
            public string materialName;
            public string sourceShader;
            public string targetShader;
            public string issue;
            public string status;
        }

        [Serializable]
        private sealed class CountRecord
        {
            public string name;
            public int count;
        }

        [Serializable]
        private sealed class GallerySummary
        {
            public string generatedUtc;
            public string unityVersion;
            public string scenePath;
            public string playerPrefabPath;
            public int scannedPrefabCount;
            public int placedPrefabCount;
            public int excludedPrefabCount;
            public int galleryMaterialRepairUseCount;
            public int uniqueGalleryMaterialCount;
            public string map01Area01Path;
            public string map01Area01Sha256Before;
            public string map01Area01Sha256After;
            public bool map01Area01Unchanged;
            public List<CountRecord> categories;
            public List<CountRecord> subcategories;
            public List<CountRecord> exclusionReasons;
        }
    }

    [InitializeOnLoad]
    public static class PrefabGalleryPlayModeVerifier
    {
        private const string RunKey = "BornToDig.PrefabGallery.PlayVerifierRunning";
        private const string ResultKey = "BornToDig.PrefabGallery.PlayVerifierResult";
        private const string MessageKey = "BornToDig.PrefabGallery.PlayVerifierMessage";
        private const string ScenePath = "Assets/BornToDig/PrefabGallery/Scenes/PrefabGallery.unity";

        private static readonly List<string> RuntimeErrors = new List<string>();
        private static double enteredPlayModeAt;

        static PrefabGalleryPlayModeVerifier()
        {
            if (SessionState.GetBool(RunKey, false)) RegisterCallbacks();
        }

        public static void ValidatePlayModeBatch()
        {
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(ResultKey, 0);
            SessionState.SetString(MessageKey, string.Empty);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RuntimeErrors.Clear();
                enteredPlayModeAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived -= HandleRuntimeLog;
                Application.logMessageReceived += HandleRuntimeLog;
                EditorApplication.update -= RunCheck;
                EditorApplication.update += RunCheck;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= RunCheck;
                Application.logMessageReceived -= HandleRuntimeLog;
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                int result = SessionState.GetInt(ResultKey, -1);
                string message = SessionState.GetString(MessageKey, "Unknown PrefabGallery Play Mode result.");
                SessionState.SetBool(RunKey, false);
                if (result == 1)
                {
                    Debug.Log(message);
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError(message);
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
        }

        private static void RunCheck()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.timeSinceStartup - enteredPlayModeAt < 1.5d)
            {
                return;
            }

            try
            {
                GameObject player = GameObject.Find("Area01_FPS_Player");
                CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
                int enabledListeners = listeners.Count(listener => listener.enabled);
                if (player == null || controller == null || cameras.Length != 1 || enabledListeners != 1)
                {
                    throw new InvalidOperationException(
                        "Runtime gallery composition invalid. player=" + (player != null) +
                        " controller=" + (controller != null) + " cameras=" + cameras.Length +
                        " listeners=" + enabledListeners);
                }

                CollisionFlags groundFlags = controller.Move(Vector3.down * 0.35f);
                Physics.SyncTransforms();
                bool floorCollision = (groundFlags & CollisionFlags.Below) != 0;
                if (!floorCollision)
                {
                    throw new InvalidOperationException("CharacterController did not collide with the gallery floor.");
                }

                Vector3 before = player.transform.position;
                controller.Move(player.transform.forward * 0.18f);
                Physics.SyncTransforms();
                float movement = Vector3.Distance(before, player.transform.position);
                if (movement < 0.10f)
                {
                    throw new InvalidOperationException("CharacterController could not move on gallery floor.");
                }

                if (RuntimeErrors.Count > 0)
                {
                    throw new InvalidOperationException("Runtime Console error: " + RuntimeErrors[0]);
                }

                Complete(true, "PREFAB_GALLERY_PLAYMODE_PASS movement=" + movement.ToString("F3", CultureInfo.InvariantCulture) +
                               " floorCollision=True cameras=1 listeners=1 runtimeErrors=0");
            }
            catch (Exception exception)
            {
                Complete(false, "PREFAB_GALLERY_PLAYMODE_FAIL " + exception);
            }
        }

        private static void HandleRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                RuntimeErrors.Add(condition);
            }
        }

        private static void Complete(bool success, string message)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            EditorApplication.update -= RunCheck;
            SessionState.SetInt(ResultKey, success ? 1 : -1);
            SessionState.SetString(MessageKey, message);
            EditorApplication.ExitPlaymode();
        }
    }
}
