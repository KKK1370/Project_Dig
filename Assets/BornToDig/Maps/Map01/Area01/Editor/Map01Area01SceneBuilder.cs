#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BornToDig.Map01;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace BornToDig.EditorTools
{
    /// <summary>
    /// Reproducible authoring tool for the production Map 01 / Area 01 environment base.
    /// Rebuilding only replaces assets inside this area's generated asset paths.
    /// It never opens or saves VoxelRockMVP, SampleScene, ProjectSettings, or imported pack assets.
    /// </summary>
    public static class Map01Area01SceneBuilder
    {
        public const string AreaRoot = "Assets/BornToDig/Maps/Map01/Area01";
        public const string ScenePath = AreaRoot + "/Scenes/Map01_Area01.unity";
        public const string TerrainDataPath = AreaRoot + "/Environment/Area01_TerrainData.asset";
        public const string GrassTexturePath = AreaRoot + "/Environment/Area01_GrassGround.asset";
        public const string SoilTexturePath = AreaRoot + "/Environment/Area01_SoftSoil.asset";
        public const string NaturePalettePath = AreaRoot + "/Environment/Area01_NaturePalette.asset";
        public const string GrassLayerPath = AreaRoot + "/Environment/Area01_Grass.terrainlayer";
        public const string SoilLayerPath = AreaRoot + "/Environment/Area01_Soil.terrainlayer";
        public const string VolumeProfilePath = AreaRoot + "/Lighting/Area01_DaylightVolume.asset";
        public const string SkyMaterialPath = AreaRoot + "/Lighting/Area01_SoftPlanetSky.mat";
        public const string PreviewPath = "Logs/Map01_Area01_Preview.png";

        public const string SpaceshipMarkerName = "Future_Spaceship_Area";
        public const string GiantRockMarkerName = "Future_GiantRock_Area";

        public static readonly Vector3 SpaceshipCenter = new Vector3(0f, 0f, -11.5f);
        public static readonly Vector3 SpaceshipSize = new Vector3(12f, 1f, 9f);
        public static readonly Vector3 GiantRockCenter = new Vector3(0f, 0f, 9f);
        public static readonly Vector3 GiantRockSize = new Vector3(14f, 1f, 14f);

        private const string PackRoot = "Assets/PurePoly/Mining_Pack";
        private const string PlayerPrefabPath = "Assets/FpsCharacterMVP/Prefabs/MVP_FPS_Player.prefab";
        private const string SourceMaterialPath = PackRoot + "/Materials/PP_Standard_Material.mat";
        private const string AreaMaterialPath = AreaRoot + "/Materials/Area01_PurePoly_Instanced.mat";
        private const int Seed = 10101;
        private const int TerrainDetailResolution = 512;
        private const int TerrainDetailResolutionPerPatch = 32;

        private static readonly string[] TreePrefabs =
        {
            PackRoot + "/Prefabs/Vegetation/PP_Tree_01.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_01.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_02.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_03.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_04.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_05.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Tree_01.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_06.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_07.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_08.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Tree_02.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Fir_Tree_09.prefab"
        };

        private static readonly string[] GrassClusterPrefabs =
        {
            PackRoot + "/Prefabs/Vegetation/PP_Grass_01.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_02.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_03.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_04.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_05.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_06.prefab"
        };

        private static readonly string[] GrassSinglePrefabs =
        {
            PackRoot + "/Prefabs/Vegetation/PP_Grass_Single_01.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_Single_03.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_Single_05.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_Single_07.prefab",
            PackRoot + "/Prefabs/Vegetation/PP_Grass_Single_09.prefab"
        };

        private static readonly string[] AccentPrefabs =
        {
            PackRoot + "/Prefabs/Mushrooms/PP_Mushrooms_Brown_01.prefab",
            PackRoot + "/Prefabs/Mushrooms/PP_Mushrooms_Brown_02.prefab",
            PackRoot + "/Prefabs/Mushrooms/PP_Mushrooms_Brown_04.prefab"
        };

        private static readonly string[] RockPrefabs =
        {
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Moss_01.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Moss_03.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Moss_05.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Moss_07.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Pile_Moss_01.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Pile_Moss_04.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Rock_Pile_Moss_07.prefab"
        };

        private static readonly string[] PebblePrefabs =
        {
            PackRoot + "/Prefabs/Stones, Rocks/PP_Pebbles_01.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Pebbles_04.prefab",
            PackRoot + "/Prefabs/Stones, Rocks/PP_Pebbles_07.prefab"
        };

        private static readonly string[] PlateauPrefabs =
        {
            PackRoot + "/Prefabs/Environment/PP_Rock_Plateau_Moss_01.prefab",
            PackRoot + "/Prefabs/Environment/PP_Rock_Plateau_Moss_04.prefab",
            PackRoot + "/Prefabs/Environment/PP_Rock_Plateau_Moss_09.prefab"
        };

        private static readonly string[] MountainPrefabs =
        {
            PackRoot + "/Prefabs/Environment/PP_Mountain_01.prefab",
            PackRoot + "/Prefabs/Environment/PP_Mountain_02.prefab"
        };

        private static readonly string[] LegacyWindMaterialPaths =
        {
            AreaRoot + "/Materials/Area01_GrassWind_A.mat",
            AreaRoot + "/Materials/Area01_GrassWind_B.mat",
            AreaRoot + "/Materials/Area01_GrassWind_C.mat",
            AreaRoot + "/Materials/Area01_FoliageWind_A.mat",
            AreaRoot + "/Materials/Area01_FoliageWind_B.mat"
        };

        private static int instanceCounter;
        private static System.Random random;

        [MenuItem("Born To Dig/Map 01/Build Area 01 Grassland")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ToAbsolutePath(ScenePath)) &&
                !EditorUtility.DisplayDialog(
                    "Rebuild Map01_Area01?",
                    "This recreates the generated Area01 Scene and its Area01-only Terrain, Materials, and Lighting profile. Imported assets and other Scenes are not touched.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            Build();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("MAP01_AREA01_SCENE_CREATED path=" + ScenePath);
        }

        public static void BuildBatch()
        {
            RunBatch(() =>
            {
                Build();
                Debug.Log("MAP01_AREA01_SCENE_CREATED path=" + ScenePath);
            });
        }

        public static void BuildAndRenderPreviewBatch()
        {
            RunBatch(() =>
            {
                Build();
                string validation = ValidateScene();
                Debug.Log(validation);
                RenderPreview();
            });
        }

        public static void ValidateBatch()
        {
            RunBatch(() => Debug.Log(ValidateScene()));
        }

        public static void Build()
        {
            ValidateRequiredSourceAssets();
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            random = new System.Random(Seed);
            instanceCounter = 0;

            DeleteAssetIfExists(NaturePalettePath);
            Material areaMaterial = CreateInstancedAreaMaterial();
            TerrainLayer[] terrainLayers = CreateTerrainLayers();
            TerrainData terrainData = CreateTerrainData(terrainLayers);
            VolumeProfile volumeProfile = CreateVolumeProfile();
            Material skyMaterial = CreateSkyMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Map01_Area01";

            GameObject mapRoot = new GameObject("Map01_Area01_Environment");
            GameObject terrainRoot = new GameObject("Terrain_and_Ground");
            terrainRoot.transform.SetParent(mapRoot.transform, false);
            Terrain terrain = CreateTerrain(scene, terrainRoot.transform, terrainData);

            GameObject placementRoot = new GameObject("Future_Placement_Areas");
            placementRoot.transform.SetParent(mapRoot.transform, false);
            CreatePlacementMarkers(placementRoot.transform, terrain);

            GameObject decorationRoot = new GameObject("Environment_Decoration");
            decorationRoot.transform.SetParent(mapRoot.transform, false);
            CreateEnvironment(decorationRoot.transform, terrain, areaMaterial);

            CreateNavigationBoundary(mapRoot.transform);
            CreateLightingAndAtmosphere(mapRoot.transform, volumeProfile, skyMaterial);
            CreatePlayer(scene, mapRoot.transform, terrain);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save production Area01 Scene: " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static string ValidateScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null || terrain.terrainData == null || terrain.GetComponent<TerrainCollider>() == null)
            {
                throw new InvalidOperationException("Area01 requires one working Terrain and TerrainCollider.");
            }

            GameObject spaceship = GameObject.Find(SpaceshipMarkerName);
            GameObject giantRock = GameObject.Find(GiantRockMarkerName);
            ValidateTransformOnlyMarker(spaceship, SpaceshipMarkerName, SpaceshipCenter, SpaceshipSize);
            ValidateTransformOnlyMarker(giantRock, GiantRockMarkerName, GiantRockCenter, GiantRockSize);

            float centerDistance = Vector2.Distance(ToXZ(spaceship.transform.position), ToXZ(giantRock.transform.position));
            if (centerDistance < 16f || centerDistance > 24f)
            {
                throw new InvalidOperationException("Future placement areas are outside the intended short walk distance: " + centerDistance);
            }

            Transform decoration = GameObject.Find("Environment_Decoration")?.transform;
            if (decoration == null)
            {
                throw new InvalidOperationException("Environment_Decoration root is missing.");
            }

            int enabledDecorationColliders = decoration.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled);
            if (enabledDecorationColliders != 0)
            {
                throw new InvalidOperationException("Decorative environment contains enabled Colliders: " + enabledDecorationColliders);
            }

            ValidateClearance(decoration.Find("Outer_Trees"), giantRock.transform.position, 12.5f, "tree");
            ValidateClearance(decoration.Find("Background_Rocks"), giantRock.transform.position, 11.5f, "background rock");
            ValidateClearance(decoration.Find("Midground_Shrubs"), giantRock.transform.position, 8.0f, "shrub");
            ValidateClearance(decoration, spaceship.transform.position, 5.6f, "spaceship pad decoration");

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            int enabledListeners = listeners.Count(listener => listener.enabled && listener.gameObject.activeInHierarchy);
            if (cameras.Length != 1 || enabledListeners != 1)
            {
                throw new InvalidOperationException($"Area01 must contain exactly one Camera/AudioListener. cameras={cameras.Length} listeners={enabledListeners}");
            }

            int miningTools = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Count(component => component != null && component.GetType().Name == "MiningTool");
            int miningTargets = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Count(component => component != null &&
                    (component.GetType().Name == "VoxelRock" || component.GetType().Name == "DestructiblePebble"));
            if (miningTools != 1 || miningTargets != 0)
            {
                throw new InvalidOperationException($"Expected one player MiningTool and no mining target. tools={miningTools} targets={miningTargets}");
            }

            Volume volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            WindZone wind = UnityEngine.Object.FindAnyObjectByType<WindZone>();
            Area01GentleWind gentleWind = UnityEngine.Object.FindAnyObjectByType<Area01GentleWind>();
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            if (volume == null || !volume.isGlobal || volume.sharedProfile == null || wind == null ||
                gentleWind == null || gentleWind.TreeTargetCount < 40 || gentleWind.GrassTargetCount < 80 ||
                lights.Length != 1)
            {
                throw new InvalidOperationException("Area01 Lighting/Volume/Wind setup is incomplete.");
            }

            Renderer[] renderers = decoration.GetComponentsInChildren<Renderer>(true);
            int instancedUrpRenderers = renderers.Count(renderer => renderer.sharedMaterial != null &&
                renderer.sharedMaterial.shader != null &&
                renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit" &&
                renderer.sharedMaterial.enableInstancing);
            if (renderers.Length < 180 || instancedUrpRenderers < 280)
            {
                throw new InvalidOperationException($"Environment density or shared URP material assignment is incomplete. renderers={renderers.Length} instancedURP={instancedUrpRenderers}");
            }

            VerifyNoMissingScripts(scene);

            int treeCount = CountDirectChildren(decoration.Find("Outer_Trees"));
            int shrubCount = CountDirectChildren(decoration.Find("Midground_Shrubs"));
            int detailCount = CountDirectChildren(decoration.Find("Meadow_Details"));
            int rockCount = CountDirectChildren(decoration.Find("Background_Rocks"));
            return "MAP01_AREA01_EDITMODE_PASS " +
                   $"trees={treeCount} shrubs={shrubCount} details={detailCount} rocks={rockCount} " +
                   $"renderers={renderers.Length} instancedURP={instancedUrpRenderers} " +
                   $"windTargets={gentleWind.TreeTargetCount + gentleWind.GrassTargetCount} " +
                   $"spaceship={spaceship.transform.position} spaceshipSize={spaceship.transform.localScale} " +
                   $"giantRock={giantRock.transform.position} giantRockSize={giantRock.transform.localScale} " +
                   $"centerDistance={centerDistance:F1}m cameras=1 listeners=1 miningTargets=0";
        }

        public static void RenderPreview()
        {
            Camera camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("Area01 preview Camera was not found.");
            }

            const int width = 1440;
            const int height = 900;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Renderer[] playerRenderers = GameObject.Find("Area01_FPS_Player")?.GetComponentsInChildren<Renderer>(true) ?? Array.Empty<Renderer>();
            bool[] previousRendererStates = playerRenderers.Select(renderer => renderer.enabled).ToArray();
            try
            {
                foreach (Renderer renderer in playerRenderers) renderer.enabled = false;
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                Texture2D preview = new Texture2D(width, height, TextureFormat.RGB24, false);
                preview.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                preview.Apply();
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, PreviewPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, preview.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(preview);
                Debug.Log("MAP01_AREA01_PREVIEW_RENDERED path=" + absolutePath);
            }
            finally
            {
                for (int index = 0; index < playerRenderers.Length; index++) playerRenderers[index].enabled = previousRendererStates[index];
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void CreatePlacementMarkers(Transform parent, Terrain terrain)
        {
            CreateMarker(parent, SpaceshipMarkerName, SpaceshipCenter, SpaceshipSize, terrain);
            CreateMarker(parent, GiantRockMarkerName, GiantRockCenter, GiantRockSize, terrain);
        }

        private static void CreateMarker(Transform parent, string name, Vector3 center, Vector3 size, Terrain terrain)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = new Vector3(center.x, GroundY(terrain, center) + 0.08f, center.z);
            marker.transform.localScale = size;
        }

        private static Terrain CreateTerrain(Scene scene, Transform parent, TerrainData terrainData)
        {
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.name = "Area01_GentleGrassland_Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(-40f, 0f, -40f);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 7f;
            terrain.basemapDistance = 80f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            terrain.allowAutoConnect = false;
            return terrain;
        }

        private static TerrainData CreateTerrainData(TerrainLayer[] layers)
        {
            DeleteAssetIfExists(TerrainDataPath);
            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = 129,
                alphamapResolution = 128,
                baseMapResolution = 512,
                size = new Vector3(80f, 5.5f, 80f),
                name = "Area01_TerrainData"
            };
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
            terrainData.SetDetailResolution(TerrainDetailResolution, TerrainDetailResolutionPerPatch);

            float[,] heights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];
            int maximum = terrainData.heightmapResolution - 1;
            for (int z = 0; z <= maximum; z++)
            {
                float worldZ = -40f + z / (float)maximum * 80f;
                for (int x = 0; x <= maximum; x++)
                {
                    float worldX = -40f + x / (float)maximum * 80f;
                    float noise = Mathf.PerlinNoise((worldX + 115f) * 0.045f, (worldZ + 87f) * 0.045f);
                    float broad = Mathf.Sin(worldX * 0.075f) * Mathf.Cos(worldZ * 0.061f);
                    float edge = Mathf.Clamp01((new Vector2(worldX, worldZ).magnitude - 22f) / 22f);
                    float height = 0.070f + (noise - 0.5f) * 0.070f + broad * 0.016f + edge * edge * 0.075f;

                    height = FlattenCircle(height, worldX, worldZ, SpaceshipCenter, 5.8f, 9f, 0.078f);
                    height = FlattenCircle(height, worldX, worldZ, GiantRockCenter, 7.2f, 11f, 0.082f);
                    float corridorDistance = DistanceToSegment(
                        new Vector2(worldX, worldZ),
                        new Vector2(SpaceshipCenter.x, SpaceshipCenter.z),
                        new Vector2(GiantRockCenter.x, GiantRockCenter.z));
                    float corridorBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.3f, 7.5f, corridorDistance));
                    height = Mathf.Lerp(0.080f, height, corridorBlend);
                    heights[z, x] = Mathf.Clamp01(height);
                }
            }
            terrainData.SetHeights(0, 0, heights);
            terrainData.terrainLayers = layers;

            int alphaResolution = terrainData.alphamapResolution;
            float[,,] alphaMaps = new float[alphaResolution, alphaResolution, 2];
            for (int z = 0; z < alphaResolution; z++)
            {
                float worldZ = -40f + z / (float)(alphaResolution - 1) * 80f;
                for (int x = 0; x < alphaResolution; x++)
                {
                    float worldX = -40f + x / (float)(alphaResolution - 1) * 80f;
                    float corridor = DistanceToSegment(
                        new Vector2(worldX, worldZ),
                        new Vector2(SpaceshipCenter.x, SpaceshipCenter.z),
                        new Vector2(GiantRockCenter.x, GiantRockCenter.z));
                    float pathWeight = 1f - Mathf.SmoothStep(2.2f, 5.8f, corridor);
                    float spaceshipPad = 1f - Mathf.SmoothStep(4.2f, 7.2f, Vector2.Distance(new Vector2(worldX, worldZ), ToXZ(SpaceshipCenter)));
                    float rockPad = 1f - Mathf.SmoothStep(5.4f, 8.2f, Vector2.Distance(new Vector2(worldX, worldZ), ToXZ(GiantRockCenter)));
                    float soil = Mathf.Clamp01(Mathf.Max(pathWeight * 0.42f, Mathf.Max(spaceshipPad * 0.48f, rockPad * 0.36f)));
                    alphaMaps[z, x, 0] = 1f - soil;
                    alphaMaps[z, x, 1] = soil;
                }
            }
            terrainData.SetAlphamaps(0, 0, alphaMaps);
            return terrainData;
        }

        private static TerrainLayer[] CreateTerrainLayers()
        {
            Texture2D grassTexture = CreateGroundTexture(
                GrassTexturePath,
                "Area01_GrassGround",
                new Color(0.34f, 0.57f, 0.27f),
                new Color(0.43f, 0.66f, 0.31f),
                1.4f);
            Texture2D soilTexture = CreateGroundTexture(
                SoilTexturePath,
                "Area01_SoftSoil",
                new Color(0.43f, 0.32f, 0.21f),
                new Color(0.56f, 0.43f, 0.28f),
                2.3f);

            DeleteAssetIfExists(GrassLayerPath);
            TerrainLayer grassLayer = new TerrainLayer
            {
                name = "Area01_Grass",
                diffuseTexture = grassTexture,
                tileSize = new Vector2(7f, 7f),
                smoothness = 0.05f,
                metallic = 0f
            };
            AssetDatabase.CreateAsset(grassLayer, GrassLayerPath);

            DeleteAssetIfExists(SoilLayerPath);
            TerrainLayer soilLayer = new TerrainLayer
            {
                name = "Area01_Soil",
                diffuseTexture = soilTexture,
                tileSize = new Vector2(5.5f, 5.5f),
                smoothness = 0.02f,
                metallic = 0f
            };
            AssetDatabase.CreateAsset(soilLayer, SoilLayerPath);
            return new[] { grassLayer, soilLayer };
        }

        private static Texture2D CreateGroundTexture(string assetPath, string name, Color low, Color high, float frequency)
        {
            DeleteAssetIfExists(assetPath);
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGB24, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float noise = Mathf.PerlinNoise((x + 11f) / 64f * frequency, (y + 29f) / 64f * frequency);
                    float fine = Mathf.Sin((x + y * 0.63f) * 0.41f) * 0.035f;
                    pixels[y * 64 + x] = Color.Lerp(low, high, Mathf.Clamp01(noise * 0.82f + 0.09f + fine));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, assetPath);
            return texture;
        }

        private static Material CreateInstancedAreaMaterial()
        {
            foreach (string legacyPath in LegacyWindMaterialPaths) DeleteAssetIfExists(legacyPath);
            DeleteAssetIfExists(AreaMaterialPath);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (source == null || source.shader == null || source.shader.name != "Universal Render Pipeline/Lit")
            {
                throw new InvalidOperationException("PurePoly URP/Lit source Material is missing or incompatible.");
            }

            Material material = new Material(source)
            {
                name = "Area01_PurePoly_Instanced",
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, AreaMaterialPath);
            return material;
        }

        private static VolumeProfile CreateVolumeProfile()
        {
            DeleteAssetIfExists(VolumeProfilePath);
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Area01_DaylightVolume";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.05f);
            color.contrast.Override(-4f);
            color.colorFilter.Override(new Color(1f, 0.985f, 0.95f, 1f));
            color.hueShift.Override(0f);
            color.saturation.Override(-3f);

            WhiteBalance whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(3f);
            whiteBalance.tint.Override(0f);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.08f);
            bloom.scatter.Override(0.45f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.055f);
            vignette.smoothness.Override(0.55f);
            return profile;
        }

        private static Material CreateSkyMaterial()
        {
            DeleteAssetIfExists(SkyMaterialPath);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                throw new InvalidOperationException("Unity procedural sky shader is unavailable.");
            }

            Material material = new Material(shader) { name = "Area01_SoftPlanetSky" };
            material.SetColor("_SkyTint", new Color(0.45f, 0.70f, 0.76f, 1f));
            material.SetColor("_GroundColor", new Color(0.47f, 0.53f, 0.39f, 1f));
            material.SetFloat("_SunSize", 0.025f);
            material.SetFloat("_SunSizeConvergence", 4.5f);
            material.SetFloat("_AtmosphereThickness", 0.82f);
            material.SetFloat("_Exposure", 0.92f);
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
            return material;
        }

        private static void CreateEnvironment(Transform parent, Terrain terrain, Material areaMaterial)
        {
            Transform trees = CreateGroup(parent, "Outer_Trees");
            Transform shrubs = CreateGroup(parent, "Midground_Shrubs");
            Transform details = CreateGroup(parent, "Meadow_Details");
            Transform rocks = CreateGroup(parent, "Background_Rocks");
            Transform distant = CreateGroup(parent, "Distant_Frame");

            CreateDistantFrame(distant, terrain, areaMaterial);
            CreateTrees(trees, terrain, areaMaterial);
            CreateBackgroundRocks(rocks, terrain, areaMaterial);
            CreateShrubs(shrubs, terrain, areaMaterial);
            CreateMeadowDetails(details, terrain, areaMaterial);
        }

        private static void CreateDistantFrame(Transform parent, Terrain terrain, Material areaMaterial)
        {
            Vector3[] mountainPositions =
            {
                new Vector3(-34f, 0f, 39f),
                new Vector3(34f, 0f, 39f)
            };
            for (int index = 0; index < mountainPositions.Length; index++)
            {
                CreatePackInstance(
                    MountainPrefabs[index % MountainPrefabs.Length],
                    parent,
                    terrain,
                    mountainPositions[index],
                    160f + index * 37f,
                    7.5f + index % 2 * 1.2f,
                    areaMaterial,
                    ShadowCastingMode.On,
                    true,
                    "DistantMountain");
            }

            Vector3[] plateauPositions =
            {
                new Vector3(-31f, 0f, 27f),
                new Vector3(31f, 0f, 26f),
                new Vector3(-34f, 0f, -17f),
                new Vector3(34f, 0f, -15f),
                new Vector3(-23f, 0f, -32f),
                new Vector3(23f, 0f, -31f)
            };
            for (int index = 0; index < plateauPositions.Length; index++)
            {
                CreatePackInstance(
                    PlateauPrefabs[index % PlateauPrefabs.Length],
                    parent,
                    terrain,
                    plateauPositions[index],
                    index * 53f,
                    5.5f + RandomRange(0f, 2.5f),
                    areaMaterial,
                    ShadowCastingMode.On,
                    true,
                    "DistantPlateau");
            }
        }

        private static void CreateTrees(Transform parent, Terrain terrain, Material areaMaterial)
        {
            Vector3[] anchors =
            {
                new Vector3(-16.5f, 0f, 19f), new Vector3(16.5f, 0f, 19.5f),
                new Vector3(-18f, 0f, 8f), new Vector3(18.5f, 0f, 8.5f),
                new Vector3(-21f, 0f, -2f), new Vector3(21f, 0f, -1f),
                new Vector3(-20f, 0f, -17f), new Vector3(20f, 0f, -17f),
                new Vector3(-13.5f, 0f, 26f), new Vector3(13.5f, 0f, 26.5f),
                new Vector3(-25f, 0f, 18f), new Vector3(25f, 0f, 19f)
            };
            for (int index = 0; index < anchors.Length; index++)
            {
                CreatePackInstance(
                    TreePrefabs[index % TreePrefabs.Length],
                    parent,
                    terrain,
                    anchors[index],
                    RandomRange(0f, 360f),
                    RandomRange(6.4f, 8.8f),
                    areaMaterial,
                    ShadowCastingMode.On,
                    false,
                    "FrameTree");
            }

            int created = 0;
            for (int attempt = 0; attempt < 240 && created < 34; attempt++)
            {
                float angle = RandomRange(0f, Mathf.PI * 2f);
                float radiusX = RandomRange(25f, 35f);
                float radiusZ = RandomRange(23f, 34f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radiusX, 0f, 5f + Mathf.Sin(angle) * radiusZ);
                if (Mathf.Abs(position.x) < 10f && position.z > -19f && position.z < 24f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 9f) continue;
                if (Mathf.Abs(position.x) > 37f || Mathf.Abs(position.z) > 37f) continue;

                CreatePackInstance(
                    Pick(TreePrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(6.5f, 10.5f),
                    areaMaterial,
                    ShadowCastingMode.On,
                    false,
                    "OuterTree");
                created++;
            }
        }

        private static void CreateBackgroundRocks(Transform parent, Terrain terrain, Material areaMaterial)
        {
            Vector3[] anchors =
            {
                new Vector3(-22f, 0f, 22f), new Vector3(22f, 0f, 23f),
                new Vector3(-27f, 0f, 5f), new Vector3(27f, 0f, 6f),
                new Vector3(-24f, 0f, -19f), new Vector3(24f, 0f, -20f),
                new Vector3(-12f, 0f, 30f), new Vector3(13f, 0f, 31f)
            };
            for (int index = 0; index < anchors.Length; index++)
            {
                CreatePackInstance(
                    RockPrefabs[index % RockPrefabs.Length],
                    parent,
                    terrain,
                    anchors[index],
                    RandomRange(0f, 360f),
                    RandomRange(2.1f, 4.2f),
                    areaMaterial,
                    ShadowCastingMode.On,
                    true,
                    "BackgroundRock");
            }

            int created = 0;
            for (int attempt = 0; attempt < 180 && created < 16; attempt++)
            {
                Vector3 position = new Vector3(RandomRange(-33f, 33f), 0f, RandomRange(-30f, 34f));
                if (Vector2.Distance(ToXZ(position), ToXZ(GiantRockCenter)) < 13f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 9f) continue;
                if (Mathf.Abs(position.x) < 7f && position.z > -17f && position.z < 20f) continue;

                CreatePackInstance(
                    Pick(RockPrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(1.2f, 2.8f),
                    areaMaterial,
                    ShadowCastingMode.On,
                    true,
                    "PerimeterRock");
                created++;
            }
        }

        private static void CreateShrubs(Transform parent, Terrain terrain, Material areaMaterial)
        {
            int created = 0;
            for (int attempt = 0; attempt < 300 && created < 48; attempt++)
            {
                Vector3 position = new Vector3(RandomRange(-27f, 27f), 0f, RandomRange(-24f, 30f));
                float giantDistance = Vector2.Distance(ToXZ(position), ToXZ(GiantRockCenter));
                if (giantDistance < 8.6f || giantDistance > 27f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 7.2f) continue;
                if (Mathf.Abs(position.x) < 4.4f && position.z > -15f && position.z < 14f) continue;

                CreatePackInstance(
                    Pick(GrassClusterPrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(0.72f, 1.18f),
                    areaMaterial,
                    ShadowCastingMode.Off,
                    false,
                    "ShrubCluster");
                created++;
            }
        }

        private static void CreateMeadowDetails(Transform parent, Terrain terrain, Material areaMaterial)
        {
            int grassCount = 0;
            for (int attempt = 0; attempt < 800 && grassCount < 150; attempt++)
            {
                Vector3 position = new Vector3(RandomRange(-31f, 31f), 0f, RandomRange(-28f, 33f));
                if (Vector2.Distance(ToXZ(position), ToXZ(GiantRockCenter)) < 7.4f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 6.4f) continue;
                if (Mathf.Abs(position.x) < 2.8f && position.z > -16f && position.z < 16f && RandomRange(0f, 1f) < 0.82f) continue;

                CreatePackInstance(
                    Pick(GrassSinglePrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(0.32f, 0.68f),
                    areaMaterial,
                    ShadowCastingMode.Off,
                    false,
                    "MeadowGrass");
                grassCount++;
            }

            int accentCount = 0;
            for (int attempt = 0; attempt < 220 && accentCount < 18; attempt++)
            {
                Vector3 position = new Vector3(RandomRange(-25f, 25f), 0f, RandomRange(-22f, 28f));
                if (Vector2.Distance(ToXZ(position), ToXZ(GiantRockCenter)) < 9f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 7.5f) continue;
                if (Mathf.Abs(position.x) < 4f && position.z > -15f && position.z < 15f) continue;

                CreatePackInstance(
                    Pick(AccentPrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(0.22f, 0.42f),
                    areaMaterial,
                    ShadowCastingMode.Off,
                    false,
                    "WarmAccent");
                accentCount++;
            }

            int pebbleCount = 0;
            for (int attempt = 0; attempt < 240 && pebbleCount < 24; attempt++)
            {
                Vector3 position = new Vector3(RandomRange(-29f, 29f), 0f, RandomRange(-25f, 31f));
                if (Vector2.Distance(ToXZ(position), ToXZ(GiantRockCenter)) < 8.4f) continue;
                if (Vector2.Distance(ToXZ(position), ToXZ(SpaceshipCenter)) < 7f) continue;
                if (Mathf.Abs(position.x) < 3.5f && position.z > -15f && position.z < 15f) continue;

                CreatePackInstance(
                    Pick(PebblePrefabs),
                    parent,
                    terrain,
                    position,
                    RandomRange(0f, 360f),
                    RandomRange(0.16f, 0.34f),
                    areaMaterial,
                    ShadowCastingMode.Off,
                    false,
                    "SmallPebble");
                pebbleCount++;
            }
        }

        private static GameObject CreatePackInstance(
            string prefabPath,
            Transform parent,
            Terrain terrain,
            Vector3 position,
            float rotationY,
            float targetHeight,
            Material overrideMaterial,
            ShadowCastingMode shadowCasting,
            bool staticRock,
            string label)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Required PurePoly prefab is missing: " + prefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instanceCounter++;
            instance.name = label + "_" + instanceCounter.ToString("D3") + "_" + prefab.name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = new Vector3(position.x, GroundY(terrain, position), position.z);
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("PurePoly prefab has no Renderer: " + prefabPath);
            }

            Bounds initialBounds = CalculateBounds(renderers);
            if (initialBounds.size.y <= 0.0001f)
            {
                throw new InvalidOperationException("PurePoly prefab has invalid bounds: " + prefabPath);
            }

            float uniformScale = targetHeight / initialBounds.size.y;
            instance.transform.localScale = Vector3.one * uniformScale;
            Bounds scaledBounds = CalculateBounds(renderers);
            instance.transform.position += Vector3.up * (GroundY(terrain, position) - scaledBounds.min.y);

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 2;
            }
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (Renderer renderer in renderers)
            {
                if (overrideMaterial != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = overrideMaterial;
                    renderer.sharedMaterials = materials;
                }
                if (renderer.sharedMaterials.Any(material => material == null || material.shader == null))
                {
                    throw new InvalidOperationException("Missing material/shader on: " + prefabPath);
                }
                renderer.shadowCastingMode = shadowCasting;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            if (staticRock)
            {
                GameObjectUtility.SetStaticEditorFlags(
                    instance,
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }
            return instance;
        }

        private static void CreateNavigationBoundary(Transform parent)
        {
            GameObject root = new GameObject("Soft_Map_Boundary");
            root.transform.SetParent(parent, false);
            CreateBoundary(root.transform, "North_Boundary", new Vector3(0f, 4f, 39.5f), new Vector3(80f, 8f, 1f));
            CreateBoundary(root.transform, "South_Boundary", new Vector3(0f, 4f, -39.5f), new Vector3(80f, 8f, 1f));
            CreateBoundary(root.transform, "East_Boundary", new Vector3(39.5f, 4f, 0f), new Vector3(1f, 8f, 80f));
            CreateBoundary(root.transform, "West_Boundary", new Vector3(-39.5f, 4f, 0f), new Vector3(1f, 8f, 80f));
        }

        private static void CreateBoundary(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject boundary = new GameObject(name);
            boundary.layer = 2;
            boundary.transform.SetParent(parent, false);
            boundary.transform.position = position;
            BoxCollider collider = boundary.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void CreateLightingAndAtmosphere(Transform parent, VolumeProfile profile, Material skyMaterial)
        {
            GameObject root = new GameObject("Area01_Lighting_and_Atmosphere");
            root.transform.SetParent(parent, false);

            GameObject sunObject = new GameObject("Warm_Afternoon_Sun");
            sunObject.transform.SetParent(root.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5400f;
            sun.color = new Color(1f, 0.97f, 0.90f, 1f);
            sun.intensity = 1.12f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;

            GameObject volumeObject = new GameObject("Area01_Global_Volume");
            volumeObject.transform.SetParent(root.transform, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            GameObject windObject = new GameObject("Area01_Gentle_Wind");
            windObject.transform.SetParent(root.transform, false);
            windObject.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
            WindZone wind = windObject.AddComponent<WindZone>();
            wind.mode = WindZoneMode.Directional;
            wind.windMain = 0.15f;
            wind.windTurbulence = 0.04f;
            wind.windPulseMagnitude = 0.05f;
            wind.windPulseFrequency = 0.08f;

            Transform decoration = GameObject.Find("Environment_Decoration")?.transform;
            Transform treeRoot = decoration?.Find("Outer_Trees");
            Transform shrubRoot = decoration?.Find("Midground_Shrubs");
            Transform detailRoot = decoration?.Find("Meadow_Details");
            if (treeRoot == null || shrubRoot == null || detailRoot == null)
            {
                throw new InvalidOperationException("Area01 vegetation groups are missing for wind setup.");
            }
            Transform[] treeTargets = treeRoot.Cast<Transform>().ToArray();
            List<Transform> grassTargets = shrubRoot.Cast<Transform>().ToList();
            int meadowIndex = 0;
            foreach (Transform child in detailRoot)
            {
                bool smallGrass = child.name.Contains("MeadowGrass") && meadowIndex++ % 3 == 0;
                bool accent = child.name.Contains("WarmAccent");
                if (smallGrass || accent) grassTargets.Add(child);
            }
            Area01GentleWind gentleWind = windObject.AddComponent<Area01GentleWind>();
            gentleWind.Configure(treeTargets, grassTargets.ToArray());

            RenderSettings.skybox = skyMaterial;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.72f, 0.76f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.68f, 0.58f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.39f, 0.30f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.82f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.79f, 0.79f, 1f);
            RenderSettings.fogStartDistance = 54f;
            RenderSettings.fogEndDistance = 112f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        private static void CreatePlayer(Scene scene, Transform parent, Terrain terrain)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("MVP FPS Player prefab is missing: " + PlayerPrefabPath);
            }

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "Area01_FPS_Player";
            player.transform.SetParent(parent, false);
            Vector3 spawn = new Vector3(0f, 0f, -6.5f);
            player.transform.position = new Vector3(spawn.x, GroundY(terrain, spawn) + 0.12f, spawn.z);
            player.transform.rotation = Quaternion.identity;

            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                throw new InvalidOperationException("MVP FPS Player prefab has no Camera.");
            }
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 130f;
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.renderShadows = true;

            MiningTool miningTool = camera.GetComponent<MiningTool>();
            if (miningTool == null) miningTool = camera.gameObject.AddComponent<MiningTool>();
            miningTool.Configure(
                camera,
                distance: 15.186258f,
                radius: 0.585f,
                strength: 1.528f,
                displayCrosshair: false,
                requireCursorLock: true,
                interval: 0.48f);
        }

        private static void ValidateRequiredSourceAssets()
        {
            List<string> paths = new List<string>();
            paths.AddRange(TreePrefabs);
            paths.AddRange(GrassClusterPrefabs);
            paths.AddRange(GrassSinglePrefabs);
            paths.AddRange(AccentPrefabs);
            paths.AddRange(RockPrefabs);
            paths.AddRange(PebblePrefabs);
            paths.AddRange(PlateauPrefabs);
            paths.AddRange(MountainPrefabs);
            paths.Add(SourceMaterialPath);
            paths.Add(PlayerPrefabPath);
            foreach (string path in paths.Distinct())
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new InvalidOperationException("Required source asset was not found: " + path);
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "BornToDig");
            EnsureFolder("Assets/BornToDig", "Maps");
            EnsureFolder("Assets/BornToDig/Maps", "Map01");
            EnsureFolder("Assets/BornToDig/Maps/Map01", "Area01");
            foreach (string child in new[] { "Scenes", "Prefabs", "Materials", "Environment", "Lighting", "Shaders", "Editor" })
            {
                EnsureFolder(AreaRoot, child);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Could not replace Area01 generated asset: " + path);
            }
        }

        private static void ValidateTransformOnlyMarker(GameObject marker, string expectedName, Vector3 expectedCenter, Vector3 expectedSize)
        {
            if (marker == null || marker.name != expectedName)
            {
                throw new InvalidOperationException("Missing future placement marker: " + expectedName);
            }
            if (marker.transform.childCount != 0 || marker.GetComponents<Component>().Length != 1)
            {
                throw new InvalidOperationException(expectedName + " must remain a Transform-only empty GameObject.");
            }
            if (Vector2.Distance(ToXZ(marker.transform.position), ToXZ(expectedCenter)) > 0.05f ||
                Vector3.Distance(marker.transform.localScale, expectedSize) > 0.05f)
            {
                throw new InvalidOperationException(expectedName + " position/size changed unexpectedly.");
            }
        }

        private static void ValidateClearance(Transform root, Vector3 center, float minimumDistance, string label)
        {
            if (root == null) throw new InvalidOperationException("Missing decoration group for clearance: " + label);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                float distance = Vector2.Distance(ToXZ(renderer.bounds.center), ToXZ(center));
                if (distance < minimumDistance)
                {
                    throw new InvalidOperationException($"{label} intrudes into reserved clearance: {renderer.name} distance={distance:F2}");
                }
            }
        }

        private static void VerifyNoMissingScripts(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        throw new InvalidOperationException("Missing Script found on " + child.name);
                    }
                }
            }
        }

        private static int CountDirectChildren(Transform root)
        {
            return root == null ? 0 : root.childCount;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float GroundY(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        }

        private static float FlattenCircle(float source, float x, float z, Vector3 center, float innerRadius, float outerRadius, float flatHeight)
        {
            float distance = Vector2.Distance(new Vector2(x, z), ToXZ(center));
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, outerRadius, distance));
            return Mathf.Lerp(flatHeight, source, blend);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static Vector2 ToXZ(Vector3 value)
        {
            return new Vector2(value.x, value.z);
        }

        private static float RandomRange(float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        private static string Pick(string[] values)
        {
            return values[random.Next(values.Length)];
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void RunBatch(Action action)
        {
            try
            {
                action();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }

    /// <summary>
    /// Draws editor-only reserved-area bounds while keeping both Scene markers as Transform-only GameObjects.
    /// </summary>
    public static class Map01Area01PlacementGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawMarker(Transform transform, GizmoType gizmoType)
        {
            if (transform.name != Map01Area01SceneBuilder.SpaceshipMarkerName &&
                transform.name != Map01Area01SceneBuilder.GiantRockMarkerName)
            {
                return;
            }

            bool spaceship = transform.name == Map01Area01SceneBuilder.SpaceshipMarkerName;
            Color color = spaceship
                ? new Color(0.35f, 0.78f, 1f, 0.82f)
                : new Color(1f, 0.73f, 0.23f, 0.86f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                new Vector3(transform.localScale.x, 0.08f, transform.localScale.z));
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = previous;
            Handles.color = color;
            Handles.Label(transform.position + Vector3.up * 0.35f, transform.name);
        }
    }

    [InitializeOnLoad]
    public static class Map01Area01PlayModeVerifier
    {
        private const string RunKey = "BornToDig.Map01Area01.PlayVerifierRunning";
        private const string ResultKey = "BornToDig.Map01Area01.PlayVerifierResult";
        private const string MessageKey = "BornToDig.Map01Area01.PlayVerifierMessage";

        private static readonly List<string> RuntimeErrors = new List<string>();
        private static double enteredPlayModeAt;

        static Map01Area01PlayModeVerifier()
        {
            if (SessionState.GetBool(RunKey, false)) RegisterCallbacks();
        }

        public static void ValidatePlayModeBatch()
        {
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(ResultKey, 0);
            SessionState.SetString(MessageKey, string.Empty);
            EditorSceneManager.OpenScene(Map01Area01SceneBuilder.ScenePath, OpenSceneMode.Single);
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
                EditorApplication.update -= RunPlayModeCheck;
                EditorApplication.update += RunPlayModeCheck;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= RunPlayModeCheck;
                Application.logMessageReceived -= HandleRuntimeLog;
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                int result = SessionState.GetInt(ResultKey, -1);
                string message = SessionState.GetString(MessageKey, "Unknown Area01 Play Mode result.");
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

        private static void RunPlayModeCheck()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.timeSinceStartup - enteredPlayModeAt < 1.25d)
            {
                return;
            }

            try
            {
                GameObject player = GameObject.Find("Area01_FPS_Player");
                Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
                CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
                int enabledListeners = listeners.Count(listener => listener.enabled);
                Area01GentleWind gentleWind = UnityEngine.Object.FindAnyObjectByType<Area01GentleWind>();
                if (player == null || terrain == null || controller == null || gentleWind == null ||
                    cameras.Length != 1 || enabledListeners != 1)
                {
                    throw new InvalidOperationException(
                        $"Runtime scene composition invalid. player={player != null} terrain={terrain != null} controller={controller != null} wind={gentleWind != null} cameras={cameras.Length} listeners={enabledListeners}");
                }

                Transform treeRoot = GameObject.Find("Environment_Decoration")?.transform.Find("Outer_Trees");
                bool windAnimating = treeRoot != null && treeRoot.Cast<Transform>().Any(tree =>
                    Mathf.Abs(Mathf.DeltaAngle(0f, tree.localEulerAngles.x)) > 0.01f ||
                    Mathf.Abs(Mathf.DeltaAngle(0f, tree.localEulerAngles.z)) > 0.01f);
                if (!windAnimating)
                {
                    throw new InvalidOperationException("Area01 gentle wind targets are not moving in Play Mode.");
                }

                float groundY = terrain.SampleHeight(player.transform.position) + terrain.transform.position.y;
                float heightAboveGround = player.transform.position.y - groundY;
                if (heightAboveGround < -0.2f || heightAboveGround > 1.2f)
                {
                    throw new InvalidOperationException("Player is not stably placed on Area01 Terrain: heightAboveGround=" + heightAboveGround);
                }

                Vector3 before = player.transform.position;
                controller.Move(player.transform.forward * 0.18f);
                Physics.SyncTransforms();
                float movement = Vector3.Distance(before, player.transform.position);
                if (movement < 0.10f)
                {
                    throw new InvalidOperationException("CharacterController could not move on Area01 Terrain.");
                }

                foreach (string markerName in new[] { Map01Area01SceneBuilder.SpaceshipMarkerName, Map01Area01SceneBuilder.GiantRockMarkerName })
                {
                    GameObject marker = GameObject.Find(markerName);
                    if (marker == null || marker.GetComponent<Renderer>() != null || marker.GetComponent<Collider>() != null)
                    {
                        throw new InvalidOperationException(markerName + " is missing or visible/collidable at runtime.");
                    }
                }

                if (RuntimeErrors.Count > 0)
                {
                    throw new InvalidOperationException("Runtime Console error: " + RuntimeErrors[0]);
                }

                Complete(true,
                    $"MAP01_AREA01_PLAYMODE_PASS playerGrounded={controller.isGrounded} movement={movement:F3} " +
                    $"heightAboveGround={heightAboveGround:F3} cameras=1 listeners=1 runtimeErrors=0 " +
                    "futureMarkersInvisible=True terrainCollision=True windAnimating=True");
            }
            catch (Exception exception)
            {
                Complete(false, "MAP01_AREA01_PLAYMODE_FAIL " + exception);
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
            EditorApplication.update -= RunPlayModeCheck;
            SessionState.SetInt(ResultKey, success ? 1 : -1);
            SessionState.SetString(MessageKey, message);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
