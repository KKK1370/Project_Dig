#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BornToDig.CharacterMVP;
using BornToDig.Destructibles;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    public static class DestructiblePebbleVerifier
    {
        private const string Root = "Assets/BornToDig/DestructiblePebbles";
        private const string ScenePath = "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";

        public static void VerifyRockABatch()
        {
            VerifyPrefabPair("A");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject sample = GameObject.Find("DestructiblePebble_Sample_A");
            if (sample == null || sample.GetComponent<DestructiblePebble>() == null)
            {
                throw new InvalidOperationException("Rock_A destructible sample is missing from VoxelRockMVP.");
            }

            if (sample.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                sample.GetComponentsInChildren<FracturedPebbleInstance>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "The intact scene sample contains always-active fragments or Rigidbodies.");
            }

            VerifyNoMissingScripts(scene);
            Debug.Log("DESTRUCTIBLE_PEBBLE_A_EDITMODE_TEST_PASS " +
                      $"position={sample.transform.position} intactRigidbodies=0 fragmentCount=5");
        }

        public static void VerifyAllBatch()
        {
            foreach (string rockId in new[] { "A", "B", "C" })
            {
                VerifyPrefabPair(rockId);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string rockId in new[] { "A", "B", "C" })
            {
                GameObject sample = GameObject.Find($"DestructiblePebble_Sample_{rockId}");
                if (sample == null || sample.GetComponent<DestructiblePebble>() == null)
                {
                    throw new InvalidOperationException($"Rock_{rockId} sample is missing.");
                }
                if (sample.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                {
                    throw new InvalidOperationException($"Rock_{rockId} intact sample contains a Rigidbody.");
                }
            }

            VerifyNoMissingScripts(scene);
            Debug.Log("DESTRUCTIBLE_PEBBLE_ALL_EDITMODE_TEST_PASS rocks=3 fragmentsPerRock=5");
        }

        private static void VerifyPrefabPair(string rockId)
        {
            string intactPath = $"{Root}/Prefabs/Rock_{rockId}_Intact.prefab";
            string fracturedPath = $"{Root}/Prefabs/Rock_{rockId}_Fractured.prefab";
            GameObject intactAsset = AssetDatabase.LoadAssetAtPath<GameObject>(intactPath);
            GameObject fracturedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fracturedPath);
            if (intactAsset == null || fracturedAsset == null)
            {
                throw new InvalidOperationException($"Rock_{rockId} prefab pair is missing.");
            }

            GameObject intact = PrefabUtility.LoadPrefabContents(intactPath);
            GameObject fractured = PrefabUtility.LoadPrefabContents(fracturedPath);
            try
            {
                DestructiblePebble destructible = intact.GetComponent<DestructiblePebble>();
                if (destructible == null || destructible.FracturedPrefab != fracturedAsset ||
                    intact.GetComponent<BoxCollider>() == null)
                {
                    throw new InvalidOperationException($"Rock_{rockId} intact configuration is broken.");
                }
                if (intact.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                {
                    throw new InvalidOperationException($"Rock_{rockId} intact prefab must not contain Rigidbody.");
                }

                MeshFilter[] fragments = fractured.GetComponentsInChildren<MeshFilter>(true);
                Rigidbody[] bodies = fractured.GetComponentsInChildren<Rigidbody>(true);
                MeshCollider[] colliders = fractured.GetComponentsInChildren<MeshCollider>(true);
                if (fragments.Length != 5 || bodies.Length != 5 || colliders.Length != 5)
                {
                    throw new InvalidOperationException(
                        $"Rock_{rockId} fractured counts are {fragments.Length}/{bodies.Length}/{colliders.Length}.");
                }
                if (fractured.GetComponent<FracturedPebbleInstance>() == null)
                {
                    throw new InvalidOperationException($"Rock_{rockId} fractured marker is missing.");
                }

                var names = new HashSet<string>();
                for (int i = 0; i < fragments.Length; i++)
                {
                    names.Add(fragments[i].gameObject.name);
                }
                for (int i = 1; i <= 5; i++)
                {
                    if (!names.Contains($"Rock_{rockId}_Fragment_{i:00}"))
                    {
                        throw new InvalidOperationException($"Rock_{rockId} fragment naming is incomplete.");
                    }
                }
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (!bodies[i].isKinematic || !colliders[i].convex)
                    {
                        throw new InvalidOperationException(
                            $"Rock_{rockId} fragments must use kinematic Rigidbody and convex MeshCollider in prefab.");
                    }
                }

                Bounds intactBounds = CalculateRendererBounds(intact);
                Bounds fracturedBounds = CalculateRendererBounds(fractured);
                float sizeError = (intactBounds.size - fracturedBounds.size).magnitude;
                float centerError = (intactBounds.center - fracturedBounds.center).magnitude;
                if (sizeError > 0.003f || centerError > 0.003f)
                {
                    throw new InvalidOperationException(
                        $"Rock_{rockId} reconstructed silhouette mismatch: size={sizeError}, " +
                        $"center={centerError}, intactSize={intactBounds.size}, " +
                        $"fracturedSize={fracturedBounds.size}, intactCenter={intactBounds.center}, " +
                        $"fracturedCenter={fracturedBounds.center}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(intact);
                PrefabUtility.UnloadPrefabContents(fractured);
            }
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} has no renderer.");
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
                    MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] == null)
                        {
                            throw new InvalidOperationException($"Missing Script found on {child.name}.");
                        }
                    }
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class DestructiblePebblePlayModeVerifier
    {
        private const string ScenePath = "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";
        private const string RunKey = "BornToDig.DestructiblePebble.PlayVerifierRunning";
        private const string ResultKey = "BornToDig.DestructiblePebble.PlayVerifierResult";
        private const string MessageKey = "BornToDig.DestructiblePebble.PlayVerifierMessage";

        private static readonly List<string> RuntimeErrors = new List<string>();
        private static int phase;
        private static int hitCount;
        private static bool survivedAtLeastOneHit;
        private static double enteredPlayModeAt;
        private static double motionCheckAt;
        private static double cleanupDeadline;
        private static Vector3[] initialFragmentPositions;

        private static DestructiblePebble pebble;
        private static MiningTool miningTool;
        private static FpsCharacterController fps;
        private static Camera camera;
        private static FracturedPebbleInstance fractured;

        static DestructiblePebblePlayModeVerifier()
        {
            if (SessionState.GetBool(RunKey, false)) RegisterCallbacks();
        }

        public static void VerifyRockABatch()
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
                Application.logMessageReceived -= HandleRuntimeLog;
                Application.logMessageReceived += HandleRuntimeLog;
                phase = 0;
                hitCount = 0;
                survivedAtLeastOneHit = false;
                enteredPlayModeAt = EditorApplication.timeSinceStartup;
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
                if (phase == 0)
                {
                    if (EditorApplication.timeSinceStartup - enteredPlayModeAt < 0.5d) return;
                    CaptureReferencesAndAim();
                    phase = 1;
                    return;
                }

                if (phase == 1)
                {
                    Ray ray = CenterRayToPebble();
                    float before = pebble.CurrentHitPoints;
                    if (!miningTool.TryMine(ray))
                    {
                        throw new InvalidOperationException("Existing MiningTool did not damage Rock_A.");
                    }
                    hitCount++;

                    if (pebble != null && !pebble.IsBroken)
                    {
                        if (pebble.CurrentHitPoints >= before)
                        {
                            throw new InvalidOperationException("Pebble HP did not decrease.");
                        }
                        survivedAtLeastOneHit = true;
                        if (hitCount >= 10)
                        {
                            throw new InvalidOperationException("Rock_A did not break within 10 pickaxe hits.");
                        }
                        return;
                    }

                    fractured = UnityEngine.Object.FindAnyObjectByType<FracturedPebbleInstance>();
                    if (fractured == null || fractured.FragmentCount != 5)
                    {
                        throw new InvalidOperationException("Fractured Rock_A with five fragments was not spawned.");
                    }
                    Rigidbody[] bodies = fractured.GetComponentsInChildren<Rigidbody>(true);
                    if (bodies.Length != 5)
                    {
                        throw new InvalidOperationException($"Spawned fragment count is {bodies.Length}.");
                    }
                    initialFragmentPositions = new Vector3[bodies.Length];
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        if (bodies[i].isKinematic)
                        {
                            throw new InvalidOperationException("A spawned fragment Rigidbody stayed kinematic.");
                        }
                        initialFragmentPositions[i] = bodies[i].position;
                    }
                    motionCheckAt = EditorApplication.timeSinceStartup + 0.45d;
                    phase = 2;
                    return;
                }

                if (phase == 2)
                {
                    if (EditorApplication.timeSinceStartup < motionCheckAt) return;
                    if (GameObject.Find("DestructiblePebble_Sample_A") != null)
                    {
                        throw new InvalidOperationException("The intact Rock_A object was not removed.");
                    }

                    Rigidbody[] bodies = fractured.GetComponentsInChildren<Rigidbody>(true);
                    bool moved = false;
                    bool movedHorizontally = false;
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        Vector3 displacement = bodies[i].position - initialFragmentPositions[i];
                        moved |= displacement.magnitude > 0.02f;
                        movedHorizontally |= new Vector2(displacement.x, displacement.z).magnitude > 0.01f;
                    }
                    if (!moved)
                    {
                        throw new InvalidOperationException("Fragments did not move under impulse and gravity.");
                    }
                    if (!movedHorizontally)
                    {
                        throw new InvalidOperationException("Fragments fell but did not receive directional impulse.");
                    }
                    cleanupDeadline = EditorApplication.timeSinceStartup + 2.2d;
                    phase = 3;
                    return;
                }

                if (phase == 3)
                {
                    if (fractured != null && EditorApplication.timeSinceStartup < cleanupDeadline) return;
                    if (fractured != null)
                    {
                        throw new InvalidOperationException("Fractured Rock_A was not deleted after its lifetime.");
                    }
                    if (!survivedAtLeastOneHit)
                    {
                        throw new InvalidOperationException("Rock_A broke without demonstrating HP damage first.");
                    }
                    if (RuntimeErrors.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "Runtime errors were logged: " + string.Join(" | ", RuntimeErrors));
                    }
                    Complete(true,
                        "DESTRUCTIBLE_PEBBLE_A_PLAYMODE_TEST_PASS " +
                        $"hits={hitCount} fragments=5 moved=True lifetimeCleanup=True");
                }
            }
            catch (Exception exception)
            {
                Complete(false, "DESTRUCTIBLE_PEBBLE_A_PLAYMODE_TEST_FAIL " + exception);
            }
        }

        private static void CaptureReferencesAndAim()
        {
            GameObject sample = GameObject.Find("DestructiblePebble_Sample_A");
            pebble = sample != null ? sample.GetComponent<DestructiblePebble>() : null;
            miningTool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            fps = UnityEngine.Object.FindAnyObjectByType<FpsCharacterController>();
            camera = Camera.main;
            if (pebble == null || miningTool == null || fps == null || camera == null)
            {
                throw new InvalidOperationException("Required Rock_A/FPS/MiningTool references are missing.");
            }
            if (UnityEngine.Object.FindAnyObjectByType<FracturedPebbleInstance>() != null)
            {
                throw new InvalidOperationException("Fractured pebble exists before any damage.");
            }

            pebble.Configure(
                Mathf.Max(2.5f, miningTool.MiningStrength * 1.5f),
                pebble.FracturedPrefab,
                pebble.FragmentImpulse,
                0.12f,
                2f,
                0.006f,
                2f);

            fps.SetGameplayInputEnabled(false);
            CharacterController controller = fps.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Vector3 target = sample.GetComponent<Collider>().bounds.center;
            Vector3 outward = camera.transform.position - target;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) outward = Vector3.back;
            outward.Normalize();
            Vector3 desiredCameraPosition = target + outward * 1.4f + Vector3.up * 0.12f;
            fps.transform.position += desiredCameraPosition - camera.transform.position;

            Vector3 direction = (target - camera.transform.position).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            float pitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
            fps.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Physics.SyncTransforms();
        }

        private static Ray CenterRayToPebble()
        {
            Collider collider = pebble.GetComponent<Collider>();
            return new Ray(camera.transform.position,
                (collider.bounds.center - camera.transform.position).normalized);
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
