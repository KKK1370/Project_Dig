#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BornToDig.CharacterMVP;
using BornToDig.Destructibles;
using BornToDig.Destructibles.Testing;
using BornToDig.GoldMVP;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace BornToDig.EditorTools
{
    public static class PebbleRockTestVerifier
    {
        private static readonly Vector3[] OcclusionDirections =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back,
            new Vector3(1f, 1f, 1f).normalized,
            new Vector3(1f, 1f, -1f).normalized,
            new Vector3(1f, -1f, 1f).normalized,
            new Vector3(1f, -1f, -1f).normalized,
            new Vector3(-1f, 1f, 1f).normalized,
            new Vector3(-1f, 1f, -1f).normalized,
            new Vector3(-1f, -1f, 1f).normalized,
            new Vector3(-1f, -1f, -1f).normalized
        };

        public static void VerifyEditModeBatch()
        {
            try
            {
                string result = VerifyEditMode();
                Debug.Log(result);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        public static string VerifyEditMode()
        {
            VerifyPrefab(out int countA, out int countB, out int countC,
                out float maximumNearestDistance);

            Scene scene = EditorSceneManager.OpenScene(
                PebbleRockTestGenerator.ScenePath,
                OpenSceneMode.Single);
            GameObject cluster = GameObject.Find(PebbleRockTestGenerator.ClusterName);
            GoldNuggetMVP gold = UnityEngine.Object.FindAnyObjectByType<GoldNuggetMVP>();
            MVPGameManager manager = UnityEngine.Object.FindAnyObjectByType<MVPGameManager>();
            MiningTool miningTool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            if (cluster == null || gold == null || manager == null || miningTool == null)
            {
                throw new InvalidOperationException(
                    "VoxelRockMVP is missing the cluster, gold, MVPGameManager or MiningTool.");
            }

            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cluster) !=
                PebbleRockTestGenerator.ClusterPrefabPath)
            {
                throw new InvalidOperationException("The scene cluster is not linked to its test prefab.");
            }

            DestructiblePebble[] scenePebbles =
                cluster.GetComponentsInChildren<DestructiblePebble>(true);
            if (scenePebbles.Length != PebbleRockTestGenerator.PebbleCount)
            {
                throw new InvalidOperationException(
                    $"Scene cluster contains {scenePebbles.Length} pebbles instead of " +
                    $"{PebbleRockTestGenerator.PebbleCount}.");
            }
            if (cluster.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                cluster.GetComponentsInChildren<FracturedPebbleInstance>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "The intact scene cluster contains always-active Rigidbody/fracture objects.");
            }

            PebbleGoldExposureTrackerTest tracker =
                cluster.GetComponent<PebbleGoldExposureTrackerTest>();
            if (tracker == null || tracker.GoldNugget != gold ||
                tracker.GeneratedPebbleCount != PebbleRockTestGenerator.PebbleCount ||
                tracker.GenerationSeed != PebbleRockTestGenerator.Seed)
            {
                throw new InvalidOperationException("The scene exposure tracker references are incomplete.");
            }

            if (manager.GoldNugget != gold || manager.UI == null)
            {
                throw new InvalidOperationException(
                    "Existing gold manager/UI references were broken by test placement.");
            }

            SerializedObject serializedGold = new SerializedObject(gold);
            if (serializedGold.FindProperty("voxelRock").objectReferenceValue != null)
            {
                throw new InvalidOperationException(
                    "Test gold still uses VoxelRock exposure instead of the test event bridge.");
            }

            Bounds clusterBounds = CalculateRendererBounds(cluster);
            if (!clusterBounds.Contains(gold.InteractionCollider.bounds.center))
            {
                throw new InvalidOperationException(
                    $"Gold is outside the pebble cluster bounds: gold={gold.transform.position}, " +
                    $"bounds={clusterBounds}.");
            }

            int monitoredCount = 0;
            float monitoredRadiusSquared = tracker.MonitoredRadius * tracker.MonitoredRadius;
            for (int i = 0; i < scenePebbles.Length; i++)
            {
                if ((scenePebbles[i].transform.position - gold.transform.position).sqrMagnitude <=
                    monitoredRadiusSquared)
                {
                    monitoredCount++;
                }
            }
            if (monitoredCount < 20 || monitoredCount > 70)
            {
                throw new InvalidOperationException(
                    $"Gold exposure neighborhood count {monitoredCount} is unsuitable for this test.");
            }

            Physics.SyncTransforms();
            int blockedDirections = CountInitiallyBlockedDirections(gold);
            if (blockedDirections < 10)
            {
                throw new InvalidOperationException(
                    $"Gold is insufficiently hidden: only {blockedDirections}/14 sampled directions are blocked.");
            }

            if (GameObject.Find("DestructiblePebble_Sample_A") != null ||
                GameObject.Find("DestructiblePebble_Sample_B") != null ||
                GameObject.Find("DestructiblePebble_Sample_C") != null)
            {
                throw new InvalidOperationException("Old individual pebble samples remain in the test scene.");
            }

            VerifyNoMissingScripts(scene);
            return "PEBBLE_ROCK_TEST_EDITMODE_PASS " +
                   $"total={scenePebbles.Length} A={countA} B={countB} C={countC} " +
                   $"monitored={monitoredCount} blockedDirections={blockedDirections}/14 " +
                   $"maxNearestDistance={maximumNearestDistance:F3} " +
                   $"gold={gold.transform.position} bounds={clusterBounds.size}";
        }

        private static void VerifyPrefab(
            out int countA,
            out int countB,
            out int countC,
            out float maximumNearestDistance)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                PebbleRockTestGenerator.ClusterPrefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException("The generated test cluster prefab is missing.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(
                PebbleRockTestGenerator.ClusterPrefabPath);
            try
            {
                DestructiblePebble[] pebbles =
                    root.GetComponentsInChildren<DestructiblePebble>(true);
                if (pebbles.Length != PebbleRockTestGenerator.PebbleCount)
                {
                    throw new InvalidOperationException(
                        $"Test prefab contains {pebbles.Length} pebbles.");
                }
                if (root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                    root.GetComponentsInChildren<FracturedPebbleInstance>(true).Length != 0)
                {
                    throw new InvalidOperationException(
                        "Test prefab must contain only intact pebbles without Rigidbody/fragments.");
                }

                countA = 0;
                countB = 0;
                countC = 0;
                var positions = new Vector3[pebbles.Length];
                var scales = new HashSet<int>();
                for (int i = 0; i < pebbles.Length; i++)
                {
                    DestructiblePebble pebble = pebbles[i];
                    if (pebble.GetComponent<BoxCollider>() == null)
                    {
                        throw new InvalidOperationException($"{pebble.name} has no BoxCollider.");
                    }
                    if (pebble.name.Contains("Rock_A")) countA++;
                    else if (pebble.name.Contains("Rock_B")) countB++;
                    else if (pebble.name.Contains("Rock_C")) countC++;
                    else throw new InvalidOperationException($"Unknown pebble type: {pebble.name}");

                    Vector3 scale = pebble.transform.localScale;
                    if (scale.x < 0.8f - 0.0001f || scale.x > 1.2f + 0.0001f ||
                        Mathf.Abs(scale.x - scale.y) > 0.0001f ||
                        Mathf.Abs(scale.x - scale.z) > 0.0001f)
                    {
                        throw new InvalidOperationException(
                            $"{pebble.name} scale is outside the 0.8-1.2 uniform range: {scale}.");
                    }

                    positions[i] = pebble.transform.localPosition;
                    scales.Add(Mathf.RoundToInt(scale.x * 1000f));
                }

                if (countA != 44 || countB != 44 || countC != 44 || scales.Count < 40)
                {
                    throw new InvalidOperationException(
                        $"Random mixture/scale variety is insufficient: {countA}/{countB}/{countC}, " +
                        $"uniqueScales={scales.Count}.");
                }

                maximumNearestDistance = 0f;
                for (int i = 0; i < positions.Length; i++)
                {
                    float nearest = float.PositiveInfinity;
                    for (int j = 0; j < positions.Length; j++)
                    {
                        if (i == j) continue;
                        nearest = Mathf.Min(nearest, Vector3.Distance(positions[i], positions[j]));
                    }
                    maximumNearestDistance = Mathf.Max(maximumNearestDistance, nearest);
                }
                if (maximumNearestDistance > 0.57f)
                {
                    throw new InvalidOperationException(
                        $"A pebble is too isolated; maximum nearest distance={maximumNearestDistance:F3}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int CountInitiallyBlockedDirections(GoldNuggetMVP gold)
        {
            int blocked = 0;
            Vector3 target = gold.InteractionCollider.bounds.center;
            for (int i = 0; i < OcclusionDirections.Length; i++)
            {
                Vector3 origin = target + OcclusionDirections[i] * 1.85f;
                RaycastHit[] hits = Physics.RaycastAll(
                    origin,
                    -OcclusionDirections[i],
                    2.1f,
                    ~0,
                    QueryTriggerInteraction.Collide);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    if (hits[hitIndex].collider.GetComponentInParent<DestructiblePebble>() != null)
                    {
                        blocked++;
                        break;
                    }
                    if (hits[hitIndex].collider == gold.InteractionCollider)
                    {
                        break;
                    }
                }
            }

            return blocked;
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} has no renderers.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void VerifyNoMissingScripts(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        throw new InvalidOperationException($"Missing Script found on {child.name}.");
                    }
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class PebbleRockTestPlayModeVerifier
    {
        private const string RunKey = "BornToDig.PebbleRockTest.PlayVerifierRunning";
        private const string ResultKey = "BornToDig.PebbleRockTest.PlayVerifierResult";
        private const string MessageKey = "BornToDig.PebbleRockTest.PlayVerifierMessage";

        private static readonly List<string> RuntimeErrors = new List<string>();
        private static readonly List<float> TunnelHitDistances = new List<float>();
        private static readonly List<FracturedPebbleInstance> BurstFragments =
            new List<FracturedPebbleInstance>();

        private static int phase;
        private static int tunnelBreaks;
        private static int miningHits;
        private static int burstBodies;
        private static int exposureTargetBreaks;
        private static double phaseDeadline;
        private static double nextActionAt;
        private static double enteredPlayModeAt;
        private static long burstMilliseconds;
        private static bool confirmedPreThresholdHidden;

        private static GameObject cluster;
        private static PebbleGoldExposureTrackerTest tracker;
        private static GoldNuggetMVP gold;
        private static MVPGameManager manager;
        private static MiningTool miningTool;
        private static FpsCharacterController fps;
        private static Camera camera;
        private static Ray tunnelRay;

        static PebbleRockTestPlayModeVerifier()
        {
            if (SessionState.GetBool(RunKey, false)) RegisterCallbacks();
        }

        public static void VerifyPlayModeBatch()
        {
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(ResultKey, 0);
            SessionState.SetString(MessageKey, string.Empty);
            EditorSceneManager.OpenScene(PebbleRockTestGenerator.ScenePath, OpenSceneMode.Single);
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
                TunnelHitDistances.Clear();
                BurstFragments.Clear();
                phase = 0;
                tunnelBreaks = 0;
                miningHits = 0;
                burstBodies = 0;
                exposureTargetBreaks = 0;
                confirmedPreThresholdHidden = false;
                enteredPlayModeAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived -= HandleRuntimeLog;
                Application.logMessageReceived += HandleRuntimeLog;
                EditorApplication.update -= RunPlayModeStep;
                EditorApplication.update += RunPlayModeStep;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= RunPlayModeStep;
                Application.logMessageReceived -= HandleRuntimeLog;
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                int result = SessionState.GetInt(ResultKey, -1);
                string message = SessionState.GetString(MessageKey, "Unknown result.");
                SessionState.SetBool(RunKey, false);
                if (result == 1)
                {
                    Debug.Log(message);
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError(message);
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void RunPlayModeStep()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

            try
            {
                switch (phase)
                {
                    case 0:
                        if (EditorApplication.timeSinceStartup - enteredPlayModeAt < 1.0d) return;
                        CaptureReferences();
                        phaseDeadline = EditorApplication.timeSinceStartup + 12d;
                        phase = 1;
                        break;

                    case 1:
                        RunTunnelMiningStep();
                        break;

                    case 2:
                        RunBurstBreak();
                        break;

                    case 3:
                        VerifyBurstMotionAndWaitForCleanup();
                        break;

                    case 4:
                        RunExposureStep();
                        break;

                    case 5:
                        ClearPickupSightLine();
                        break;

                    case 6:
                        TryPickupAndClear();
                        break;

                    case 7:
                        VerifyFinalState();
                        break;
                }
            }
            catch (Exception exception)
            {
                Complete(false, "PEBBLE_ROCK_TEST_PLAYMODE_FAIL " + exception);
            }
        }

        private static void CaptureReferences()
        {
            cluster = GameObject.Find(PebbleRockTestGenerator.ClusterName);
            tracker = cluster != null
                ? cluster.GetComponent<PebbleGoldExposureTrackerTest>()
                : null;
            gold = UnityEngine.Object.FindAnyObjectByType<GoldNuggetMVP>();
            manager = UnityEngine.Object.FindAnyObjectByType<MVPGameManager>();
            miningTool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            fps = UnityEngine.Object.FindAnyObjectByType<FpsCharacterController>();
            camera = Camera.main;
            VoxelRock voxelRock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();

            if (cluster == null || tracker == null || gold == null || manager == null ||
                miningTool == null || fps == null || camera == null || voxelRock == null ||
                !voxelRock.IsInitialized || voxelRock.RockCollider == null)
            {
                throw new InvalidOperationException(
                    "Required test/FPS/mining/gold/VoxelRock runtime references are not ready.");
            }
            if (tracker.MonitoredPebbleCount < 20 || gold.IsExposed || gold.IsCollected ||
                manager.CollectedCount != 0)
            {
                throw new InvalidOperationException(
                    $"Invalid initial state: monitored={tracker.MonitoredPebbleCount}, " +
                    $"exposed={gold.IsExposed}, collected={gold.IsCollected}.");
            }
            if (cluster.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                UnityEngine.Object.FindAnyObjectByType<FracturedPebbleInstance>() != null)
            {
                throw new InvalidOperationException("Fragments/Rigidbodies exist before any pebble breaks.");
            }

            fps.SetGameplayInputEnabled(false);
            CharacterController controller = fps.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Vector3 target = gold.InteractionCollider.bounds.center;
            tunnelRay = new Ray(target + Vector3.back * 3.4f, Vector3.forward);
            Physics.SyncTransforms();
        }

        private static void RunTunnelMiningStep()
        {
            if (EditorApplication.timeSinceStartup > phaseDeadline)
            {
                throw new TimeoutException("Timed out while waiting for fragments to clear the tunnel ray.");
            }
            if (EditorApplication.timeSinceStartup < nextActionAt) return;

            if (!Physics.Raycast(tunnelRay, out RaycastHit hit, 7f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            DestructiblePebble pebble = hit.collider.GetComponentInParent<DestructiblePebble>();
            if (pebble == null)
            {
                return;
            }

            if (TunnelHitDistances.Count > 0 &&
                hit.distance <= TunnelHitDistances[TunnelHitDistances.Count - 1] + 0.08f)
            {
                throw new InvalidOperationException(
                    $"Tunnel ray did not advance: last={TunnelHitDistances[TunnelHitDistances.Count - 1]:F3}, " +
                    $"next={hit.distance:F3}.");
            }

            TunnelHitDistances.Add(hit.distance);
            float hitPointsBefore = pebble.CurrentHitPoints;
            if (!miningTool.TryMine(tunnelRay))
            {
                throw new InvalidOperationException("Existing MiningTool did not damage the tunnel pebble.");
            }
            miningHits++;
            if (pebble.CurrentHitPoints >= hitPointsBefore)
            {
                throw new InvalidOperationException("Tunnel pebble HP did not decrease on the first hit.");
            }
            if (pebble.IsBroken)
            {
                throw new InvalidOperationException("Pebble broke in one normal MiningTool hit; expected about two.");
            }
            if (!miningTool.TryMine(tunnelRay))
            {
                throw new InvalidOperationException("Existing MiningTool did not deliver the second hit.");
            }
            miningHits++;
            if (!pebble.IsBroken)
            {
                throw new InvalidOperationException("Tunnel pebble did not break after about two hits.");
            }

            tunnelBreaks++;
            nextActionAt = EditorApplication.timeSinceStartup + 0.45d;
            if (tunnelBreaks >= 4)
            {
                phase = 2;
                nextActionAt = EditorApplication.timeSinceStartup + 0.6d;
            }
        }

        private static void RunBurstBreak()
        {
            if (EditorApplication.timeSinceStartup < nextActionAt) return;

            DestructiblePebble[] pebbles =
                cluster.GetComponentsInChildren<DestructiblePebble>(true);
            var candidates = new List<DestructiblePebble>();
            for (int i = 0; i < pebbles.Length; i++)
            {
                float distance = Vector3.Distance(
                    pebbles[i].transform.position,
                    gold.transform.position);
                if (distance > tracker.MonitoredRadius + 0.18f)
                {
                    candidates.Add(pebbles[i]);
                }
            }
            candidates.Sort((left, right) =>
                Vector3.Distance(right.transform.position, gold.transform.position)
                    .CompareTo(Vector3.Distance(left.transform.position, gold.transform.position)));
            if (candidates.Count < 10)
            {
                throw new InvalidOperationException("Fewer than ten surface pebbles remain for burst testing.");
            }

            FracturedPebbleInstance[] beforeFragments =
                UnityEngine.Object.FindObjectsByType<FracturedPebbleInstance>();
            var beforeSet = new HashSet<FracturedPebbleInstance>();
            for (int i = 0; i < beforeFragments.Length; i++)
            {
                beforeSet.Add(beforeFragments[i]);
            }

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                Vector3 direction = (candidates[i].transform.position - gold.transform.position).normalized;
                candidates[i].TakeDamage(
                    float.MaxValue,
                    candidates[i].transform.position - direction * 0.2f,
                    direction);
            }
            stopwatch.Stop();
            burstMilliseconds = stopwatch.ElapsedMilliseconds;

            FracturedPebbleInstance[] fractured =
                UnityEngine.Object.FindObjectsByType<FracturedPebbleInstance>();
            int bodyCount = 0;
            for (int i = 0; i < fractured.Length; i++)
            {
                if (!beforeSet.Contains(fractured[i]))
                {
                    BurstFragments.Add(fractured[i]);
                    bodyCount += fractured[i].GetComponentsInChildren<Rigidbody>(true).Length;
                }
            }
            burstBodies = bodyCount;
            if (BurstFragments.Count != 10 || bodyCount != 50)
            {
                throw new InvalidOperationException(
                    $"Ten rapid breaks did not create the expected fragment bodies: " +
                    $"newRoots={BurstFragments.Count}, bodies={bodyCount}.");
            }

            phaseDeadline = EditorApplication.timeSinceStartup + 0.55d;
            phase = 3;
        }

        private static void VerifyBurstMotionAndWaitForCleanup()
        {
            if (EditorApplication.timeSinceStartup < phaseDeadline) return;

            int dynamicBodies = 0;
            for (int rootIndex = 0; rootIndex < BurstFragments.Count; rootIndex++)
            {
                if (BurstFragments[rootIndex] == null) continue;
                Rigidbody[] bodies =
                    BurstFragments[rootIndex].GetComponentsInChildren<Rigidbody>(true);
                for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
                {
                    Rigidbody body = bodies[bodyIndex];
                    if (body.isKinematic) continue;
                    dynamicBodies++;
                    Vector3 position = body.position;
                    Vector3 velocity = body.linearVelocity;
                    if (!IsFinite(position) || !IsFinite(velocity) || velocity.magnitude > 25f)
                    {
                        throw new InvalidOperationException(
                            $"Abnormal fragment physics: position={position}, velocity={velocity}.");
                    }
                }
            }
            if (dynamicBodies < 50)
            {
                throw new InvalidOperationException(
                    $"Only {dynamicBodies} dynamic fragment bodies were active after burst break.");
            }

            phaseDeadline = EditorApplication.timeSinceStartup + 4.2d;
            phase = 4;
        }

        private static void RunExposureStep()
        {
            if (EditorApplication.timeSinceStartup < nextActionAt) return;

            int remainingBurstFragments = 0;
            for (int i = 0; i < BurstFragments.Count; i++)
            {
                if (BurstFragments[i] != null) remainingBurstFragments++;
            }
            if (remainingBurstFragments > 0)
            {
                if (EditorApplication.timeSinceStartup > phaseDeadline)
                {
                    throw new TimeoutException(
                        $"{remainingBurstFragments} burst fragment roots were not deleted after their " +
                        $"configured lifetime (Time.time={Time.time:F2}, timeScale={Time.timeScale:F2}).");
                }
                return;
            }

            int required = Mathf.CeilToInt(
                tracker.MonitoredPebbleCount * tracker.RequiredBrokenFraction);
            if (tracker.BrokenCount < required)
            {
                if (tracker.BrokenCount < required - 1 && gold.IsExposed)
                {
                    throw new InvalidOperationException("Gold became collectible before the 50% threshold.");
                }

                DestructiblePebble next = FindClosestUnbrokenMonitoredPebble();
                if (next == null)
                {
                    throw new InvalidOperationException(
                        "No monitored pebble remains before the exposure threshold.");
                }
                Vector3 direction = (next.transform.position - gold.transform.position).normalized;
                next.TakeDamage(float.MaxValue, next.transform.position - direction * 0.2f, direction);
                exposureTargetBreaks++;
                if (tracker.BrokenCount < required)
                {
                    confirmedPreThresholdHidden |= !gold.IsExposed;
                }
                nextActionAt = EditorApplication.timeSinceStartup + 0.06d;
                return;
            }

            if (!confirmedPreThresholdHidden || !gold.IsExposed || gold.ExposedFraction < 0.5f)
            {
                throw new InvalidOperationException(
                    $"Gold exposure transition failed: hiddenBefore={confirmedPreThresholdHidden}, " +
                    $"exposed={gold.IsExposed}, fraction={gold.ExposedFraction:F3}.");
            }

            phaseDeadline = EditorApplication.timeSinceStartup + 8d;
            nextActionAt = EditorApplication.timeSinceStartup + 0.15d;
            phase = 5;
        }

        private static DestructiblePebble FindClosestUnbrokenMonitoredPebble()
        {
            DestructiblePebble[] pebbles =
                cluster.GetComponentsInChildren<DestructiblePebble>(true);
            DestructiblePebble closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < pebbles.Length; i++)
            {
                float distance = Vector3.Distance(
                    pebbles[i].transform.position,
                    gold.transform.position);
                if (distance <= tracker.MonitoredRadius && distance < closestDistance)
                {
                    closest = pebbles[i];
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private static void ClearPickupSightLine()
        {
            if (EditorApplication.timeSinceStartup < nextActionAt) return;
            if (EditorApplication.timeSinceStartup > phaseDeadline)
            {
                throw new TimeoutException("Could not clear a camera-to-gold pickup sight line.");
            }

            Vector3 target = gold.InteractionCollider.bounds.center;
            Vector3 origin = target + Vector3.back * 2.2f;
            Ray ray = new Ray(origin, Vector3.forward);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                2.5f,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                DestructiblePebble blocker =
                    hits[i].collider.GetComponentInParent<DestructiblePebble>();
                if (blocker != null)
                {
                    blocker.TakeDamage(float.MaxValue, hits[i].point, ray.direction);
                    nextActionAt = EditorApplication.timeSinceStartup + 0.25d;
                    return;
                }
                if (hits[i].collider == gold.InteractionCollider)
                {
                    phaseDeadline = EditorApplication.timeSinceStartup + 5d;
                    phase = 6;
                    return;
                }
            }
        }

        private static void TryPickupAndClear()
        {
            FracturedPebbleInstance[] activeFragments =
                UnityEngine.Object.FindObjectsByType<FracturedPebbleInstance>();
            if (activeFragments.Length > 0)
            {
                if (EditorApplication.timeSinceStartup > phaseDeadline)
                {
                    throw new TimeoutException("Fragments did not clear before pickup verification.");
                }
                return;
            }

            Vector3 target = gold.InteractionCollider.bounds.center;
            Vector3 desiredCameraPosition = target + Vector3.back * 2.05f;
            fps.transform.position += desiredCameraPosition - camera.transform.position;
            Vector3 direction = (target - camera.transform.position).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            float pitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
            fps.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Physics.SyncTransforms();

            if (!gold.IsCameraTargetingPickup())
            {
                throw new InvalidOperationException(
                    "Existing camera-center/distance pickup targeting did not reach the exposed gold.");
            }
            if (!gold.CollectIfAvailable())
            {
                throw new InvalidOperationException("Existing gold pickup method rejected exposed gold.");
            }

            phaseDeadline = EditorApplication.timeSinceStartup + 1.2d;
            phase = 7;
        }

        private static void VerifyFinalState()
        {
            if (!manager.IsClear && EditorApplication.timeSinceStartup < phaseDeadline) return;
            if (!gold.IsCollected || manager.CollectedCount != 1 || !manager.IsClear)
            {
                throw new InvalidOperationException(
                    $"Existing collect/CLEAR flow failed: collected={gold.IsCollected}, " +
                    $"count={manager.CollectedCount}, clear={manager.IsClear}.");
            }
            if (RuntimeErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Runtime errors were logged: " + string.Join(" | ", RuntimeErrors));
            }
            if (TunnelHitDistances.Count < 4 ||
                TunnelHitDistances[TunnelHitDistances.Count - 1] - TunnelHitDistances[0] < 0.2f)
            {
                throw new InvalidOperationException(
                    "The repeated MiningTool ray did not demonstrate a deeper hole: " +
                    string.Join(", ", TunnelHitDistances));
            }

            float tunnelDepth =
                TunnelHitDistances[TunnelHitDistances.Count - 1] - TunnelHitDistances[0];
            Complete(true,
                "PEBBLE_ROCK_TEST_PLAYMODE_PASS " +
                $"tunnelBreaks={tunnelBreaks} miningHits={miningHits} " +
                $"firstHit={TunnelHitDistances[0]:F3} lastHit=" +
                $"{TunnelHitDistances[TunnelHitDistances.Count - 1]:F3} depth={tunnelDepth:F3} " +
                $"burstBreaks=10 burstBodies={burstBodies} burstMs={burstMilliseconds} " +
                $"exposureBreaks={exposureTargetBreaks} exposure={gold.ExposedFraction:F3} " +
                "pickup=Success clear=Success fragmentsCleaned=True voxelRockIntact=True");
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void HandleRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                stackTrace.Contains("Assets/"))
            {
                RuntimeErrors.Add(condition);
            }
        }

        private static void Complete(bool success, string message)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            EditorApplication.update -= RunPlayModeStep;
            SessionState.SetInt(ResultKey, success ? 1 : -1);
            SessionState.SetString(MessageKey, message);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
