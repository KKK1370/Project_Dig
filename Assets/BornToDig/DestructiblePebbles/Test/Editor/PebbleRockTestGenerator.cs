#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BornToDig.Destructibles.Testing;
using BornToDig.GoldMVP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    public static class PebbleRockTestGenerator
    {
        public const string ScenePath = "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";
        public const string TestRoot = "Assets/BornToDig/DestructiblePebbles/Test";
        public const string ClusterPrefabPath = TestRoot + "/Prefabs/PebbleRockCluster_Test.prefab";
        public const string ClusterName = "PebbleRockCluster_Test";
        public const int PebbleCount = 132;
        public const int Seed = 20260818;

        public static readonly Vector3 ClusterWorldPosition = new Vector3(7.35f, 1.08f, -3.05f);
        public static readonly Vector3 GoldLocalOffset = new Vector3(0.14f, -0.03f, -0.08f);

        private const float LatticeCellSize = 0.62f;
        private const float PositionJitter = 0.028f;
        private const float MonitoredRadius = 0.92f;
        private const float GoldExposureFraction = 0.5f;

        private static readonly Vector3[] FccBasis =
        {
            Vector3.zero,
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0f, 0.5f, 0.5f)
        };

        [MenuItem("Born To Dig/Destructible Pebbles/Generate Pebble Rock Test")]
        public static void GenerateFromMenu()
        {
            Generate();
            Debug.Log("PEBBLE_ROCK_TEST_GENERATED " +
                      $"count={PebbleCount} seed={Seed} prefab={ClusterPrefabPath}");
        }

        public static void GenerateBatch()
        {
            try
            {
                Generate();
                Debug.Log("PEBBLE_ROCK_TEST_GENERATED " +
                          $"count={PebbleCount} seed={Seed} prefab={ClusterPrefabPath}");
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

        public static void Generate()
        {
            EnsureFolder(TestRoot, "Prefabs");

            GameObject[] intactPrefabs =
            {
                LoadRequired<GameObject>("Assets/BornToDig/DestructiblePebbles/Prefabs/Rock_A_Intact.prefab"),
                LoadRequired<GameObject>("Assets/BornToDig/DestructiblePebbles/Prefabs/Rock_B_Intact.prefab"),
                LoadRequired<GameObject>("Assets/BornToDig/DestructiblePebbles/Prefabs/Rock_C_Intact.prefab")
            };

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GoldNuggetMVP gold = UnityEngine.Object.FindAnyObjectByType<GoldNuggetMVP>();
            Camera playerCamera = Camera.main;
            if (gold == null || playerCamera == null || gold.InteractionCollider == null)
            {
                throw new InvalidOperationException(
                    "VoxelRockMVP must contain the existing GoldNuggetMVP, Main Camera and pickup Collider.");
            }

            RemoveSceneObject(ClusterName);
            RemoveSceneObject("DestructiblePebble_Sample_A");
            RemoveSceneObject("DestructiblePebble_Sample_B");
            RemoveSceneObject("DestructiblePebble_Sample_C");

            GameObject temporaryRoot = BuildPrefabSource(intactPrefabs);
            GameObject prefabAsset;
            try
            {
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, ClusterPrefabPath);
                if (prefabAsset == null)
                {
                    throw new InvalidOperationException("Could not save the pebble rock test prefab.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }

            AssetDatabase.SaveAssets();
            prefabAsset = LoadRequired<GameObject>(ClusterPrefabPath);
            GameObject cluster = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
            cluster.name = ClusterName;
            cluster.transform.position = ClusterWorldPosition;

            gold.transform.position = ClusterWorldPosition + GoldLocalOffset;
            gold.Configure(
                null,
                playerCamera,
                gold.InteractionCollider,
                GoldExposureFraction,
                0.07f,
                2.75f);

            PebbleGoldExposureTrackerTest tracker =
                cluster.GetComponent<PebbleGoldExposureTrackerTest>();
            if (tracker == null)
            {
                throw new InvalidOperationException("Generated test cluster is missing its exposure tracker.");
            }
            tracker.Configure(gold, MonitoredRadius, GoldExposureFraction, PebbleCount, Seed);
            EditorUtility.SetDirty(tracker);
            EditorUtility.SetDirty(gold);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefabSource(GameObject[] intactPrefabs)
        {
            var random = new System.Random(Seed);
            List<Candidate> candidates = BuildCandidates();
            if (candidates.Count < PebbleCount)
            {
                throw new InvalidOperationException(
                    $"Only {candidates.Count} lattice positions were generated for {PebbleCount} pebbles.");
            }

            candidates.Sort((left, right) => left.score.CompareTo(right.score));
            List<int> rockTypes = BuildBalancedRockTypes(random);

            GameObject root = new GameObject(ClusterName);
            PebbleGoldExposureTrackerTest tracker =
                root.AddComponent<PebbleGoldExposureTrackerTest>();
            tracker.Configure(null, MonitoredRadius, GoldExposureFraction, PebbleCount, Seed);

            for (int i = 0; i < PebbleCount; i++)
            {
                int rockType = rockTypes[i];
                GameObject pebble = (GameObject)PrefabUtility.InstantiatePrefab(intactPrefabs[rockType]);
                pebble.name = $"Pebble_{i + 1:000}_Rock_{(char)('A' + rockType)}";
                pebble.transform.SetParent(root.transform, false);

                Vector3 jitter = new Vector3(
                    RandomRange(random, -PositionJitter, PositionJitter),
                    RandomRange(random, -PositionJitter, PositionJitter),
                    RandomRange(random, -PositionJitter, PositionJitter));
                pebble.transform.localPosition = candidates[i].position + jitter;
                pebble.transform.localRotation = RandomRotation(random);
                float scale = RandomRange(random, 0.8f, 1.2f);
                pebble.transform.localScale = Vector3.one * scale;
            }

            return root;
        }

        private static List<Candidate> BuildCandidates()
        {
            var candidates = new List<Candidate>();
            Vector3 radii = new Vector3(1.55f, 1.15f, 1.4f);

            for (int x = -4; x <= 4; x++)
            {
                for (int y = -4; y <= 4; y++)
                {
                    for (int z = -4; z <= 4; z++)
                    {
                        for (int basisIndex = 0; basisIndex < FccBasis.Length; basisIndex++)
                        {
                            Vector3 lattice = new Vector3(x, y, z) + FccBasis[basisIndex];
                            Vector3 position = lattice * LatticeCellSize;
                            position += new Vector3(
                                0.08f * Mathf.Sin(position.y * 2.7f + position.z * 1.9f),
                                0.035f * Mathf.Sin(position.x * 2.1f),
                                0.07f * Mathf.Sin(position.x * 1.7f - position.y * 2.3f));

                            float normalized =
                                position.x * position.x / (radii.x * radii.x) +
                                position.y * position.y / (radii.y * radii.y) +
                                position.z * position.z / (radii.z * radii.z);
                            float irregularity =
                                0.055f * Mathf.Sin(position.x * 4.3f + position.z * 2.1f) +
                                0.035f * Mathf.Cos(position.y * 5.2f - position.x * 1.8f);
                            float flattenedBasePenalty = position.y < -1.03f
                                ? (-1.03f - position.y) * 3f
                                : 0f;
                            candidates.Add(new Candidate(
                                position,
                                normalized + irregularity + flattenedBasePenalty));
                        }
                    }
                }
            }

            return candidates;
        }

        private static List<int> BuildBalancedRockTypes(System.Random random)
        {
            var types = new List<int>(PebbleCount);
            for (int i = 0; i < PebbleCount; i++)
            {
                types.Add(i % 3);
            }

            for (int i = types.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (types[i], types[swapIndex]) = (types[swapIndex], types[i]);
            }

            return types;
        }

        private static Quaternion RandomRotation(System.Random random)
        {
            return Quaternion.Euler(
                RandomRange(random, 0f, 360f),
                RandomRange(random, 0f, 360f),
                RandomRange(random, 0f, 360f));
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        private static void RemoveSceneObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required asset is missing: {assetPath}");
            }

            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private readonly struct Candidate
        {
            public Candidate(Vector3 position, float score)
            {
                this.position = position;
                this.score = score;
            }

            public readonly Vector3 position;
            public readonly float score;
        }
    }
}
#endif
