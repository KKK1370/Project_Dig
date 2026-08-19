using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    internal static class Map01Area01TerrainDetailDiagnostics
    {
        private const string TerrainDataPath =
            "Assets/BornToDig/Maps/Map01/Area01/Environment/Area01_TerrainData.asset";
        private const int DetailResolution = 512;
        private const int ResolutionPerPatch = 32;

        [MenuItem("BORN TO DIG/Map 01/Area 01/Diagnose Terrain Details")]
        public static void Diagnose()
        {
            TerrainData data = LoadTerrainData();
            LogTerrainData(data, "DIAGNOSE");
        }

        public static void DiagnoseBatch()
        {
            try
            {
                Diagnose();
                Debug.Log("MAP01_AREA01_DETAIL_DIAGNOSIS_COMPLETE");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("BORN TO DIG/Map 01/Area 01/Fix Terrain Detail Resolution")]
        public static void Fix()
        {
            TerrainData data = LoadTerrainData();
            DetailPrototype[] prototypesBefore = data.detailPrototypes;

            if (data.detailWidth == 0 || data.detailHeight == 0)
            {
                data.SetDetailResolution(DetailResolution, ResolutionPerPatch);
                data.detailPrototypes = prototypesBefore;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"MAP01_AREA01_DETAIL_RESOLUTION_FIXED resolution={DetailResolution} " +
                    $"perPatch={ResolutionPerPatch} prototypesPreserved={prototypesBefore.Length}");
            }
            else
            {
                Debug.Log(
                    $"MAP01_AREA01_DETAIL_RESOLUTION_ALREADY_VALID width={data.detailWidth} " +
                    $"height={data.detailHeight}; no change made");
            }

            LogTerrainData(data, "AFTER_FIX");
        }

        public static void FixBatch()
        {
            try
            {
                Fix();
                Debug.Log("MAP01_AREA01_DETAIL_FIX_COMPLETE");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateBatch()
        {
            TerrainData copy = null;
            try
            {
                TerrainData data = LoadTerrainData();
                if (data.detailWidth != DetailResolution || data.detailHeight != DetailResolution)
                {
                    throw new InvalidOperationException(
                        $"Unexpected detail resolution: {data.detailWidth}x{data.detailHeight}");
                }

                if (data.detailResolutionPerPatch != ResolutionPerPatch || data.detailPatchCount != 16)
                {
                    throw new InvalidOperationException(
                        $"Unexpected detail patch layout: perPatch={data.detailResolutionPerPatch}, " +
                        $"patchCount={data.detailPatchCount}");
                }

                if (data.detailPrototypes.Length != 1 ||
                    AssetDatabase.GetAssetPath(data.detailPrototypes[0].prototype) !=
                    "Assets/TerrainSampleAssets/Prefabs/Bush_B.prefab")
                {
                    throw new InvalidOperationException("Bush_B detail prototype was not preserved.");
                }

                copy = UnityEngine.Object.Instantiate(data);
                int[,] testDensity = { { 1 } };
                copy.SetDetailLayer(0, 0, 0, testDensity);
                int writtenValue = copy.GetDetailLayer(0, 0, 1, 1, 0)[0, 0];
                if (writtenValue != 1)
                {
                    throw new InvalidOperationException(
                        $"Detail density write/read failed on temporary TerrainData: {writtenValue}");
                }

                Scene scene = EditorSceneManager.OpenScene(
                    Map01Area01SceneBuilder.ScenePath,
                    OpenSceneMode.Single);
                Terrain terrain = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                    .SingleOrDefault(candidate => candidate.terrainData == data);
                if (terrain == null)
                {
                    throw new InvalidOperationException("Map01_Area01 does not reference Area01_TerrainData.");
                }

                if (!terrain.drawTreesAndFoliage || terrain.detailObjectDensity <= 0f ||
                    terrain.detailObjectDistance <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Terrain detail rendering disabled: draw={terrain.drawTreesAndFoliage}, " +
                        $"density={terrain.detailObjectDensity}, distance={terrain.detailObjectDistance}");
                }

                Debug.Log(
                    "MAP01_AREA01_DETAIL_VALIDATION_PASS " +
                    $"temporaryDensityWrite={writtenValue} productionDensitySum={SumDetailLayer(data, 0)} " +
                    $"scene={scene.path} drawFoliage={terrain.drawTreesAndFoliage} " +
                    $"densityScale={terrain.detailObjectDensity} distance={terrain.detailObjectDistance}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
            finally
            {
                if (copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
        }

        private static TerrainData LoadTerrainData()
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                throw new InvalidOperationException($"TerrainData not found: {TerrainDataPath}");
            }

            return data;
        }

        private static void LogTerrainData(TerrainData data, string phase)
        {
            DetailPrototype[] prototypes = data.detailPrototypes;
            string pipeline = GraphicsSettings.currentRenderPipeline == null
                ? "BuiltIn"
                : GraphicsSettings.currentRenderPipeline.GetType().FullName;

            Debug.Log(
                $"MAP01_AREA01_DETAIL_DATA phase={phase} size={data.size} " +
                $"detailWidth={data.detailWidth} detailHeight={data.detailHeight} " +
                $"detailResolution={data.detailResolution} perPatch={data.detailResolutionPerPatch} " +
                $"patchCount={data.detailPatchCount} prototypes={prototypes.Length} pipeline={pipeline}");

            for (int i = 0; i < prototypes.Length; i++)
            {
                DetailPrototype detail = prototypes[i];
                GameObject prefab = detail.prototype;
                Renderer[] renderers = prefab == null
                    ? Array.Empty<Renderer>()
                    : prefab.GetComponentsInChildren<Renderer>(true);
                MeshFilter[] filters = prefab == null
                    ? Array.Empty<MeshFilter>()
                    : prefab.GetComponentsInChildren<MeshFilter>(true);
                long densitySum = SumDetailLayer(data, i);
                string rendererSummary = string.Join(
                    "; ",
                    renderers.Select(renderer =>
                        $"{renderer.GetType().Name}:{renderer.name}:enabled={renderer.enabled}:materials=[" +
                        string.Join(",", renderer.sharedMaterials.Select(material =>
                            material == null
                                ? "null"
                                : $"{material.name}/{material.shader.name}/supported={material.shader.isSupported}/instancing={material.enableInstancing}")) +
                        "]"));

                Debug.Log(
                    $"MAP01_AREA01_DETAIL_PROTOTYPE index={i} prefab={AssetDatabase.GetAssetPath(prefab)} " +
                    $"usePrototypeMesh={detail.usePrototypeMesh} renderMode={detail.renderMode} " +
                    $"useInstancing={detail.useInstancing} widths={detail.minWidth}-{detail.maxWidth} " +
                    $"heights={detail.minHeight}-{detail.maxHeight} renderers={renderers.Length} " +
                    $"meshFilters={filters.Length} densitySum={densitySum} rendererInfo={rendererSummary}");
            }
        }

        private static long SumDetailLayer(TerrainData data, int layer)
        {
            if (data.detailWidth == 0 || data.detailHeight == 0)
            {
                return 0;
            }

            int[,] values = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            long total = 0;
            foreach (int value in values)
            {
                total += value;
            }

            return total;
        }
    }
}
