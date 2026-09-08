#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BornToDig.CharacterMVP;
using BornToDig.GoldMVP;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BornToDig.EditorTools
{
    public static class GoldNuggetMvpVerifier
    {
        private const string ScenePath =
            "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";

        public static void VerifyBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);

            VoxelRock rock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();
            MiningTool miningTool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            GoldNuggetMVP nugget = UnityEngine.Object.FindAnyObjectByType<GoldNuggetMVP>();
            MVPGameManager manager = UnityEngine.Object.FindAnyObjectByType<MVPGameManager>();
            MVPUI ui = UnityEngine.Object.FindAnyObjectByType<MVPUI>();
            FpsCharacterController fps =
                UnityEngine.Object.FindAnyObjectByType<FpsCharacterController>();
            Camera camera = Camera.main;

            if (rock == null || miningTool == null || nugget == null || manager == null ||
                ui == null || fps == null || camera == null)
            {
                throw new InvalidOperationException(
                    "Gold MVP scene is missing a required rock, player, nugget, manager, UI, or camera.");
            }

            VerifySceneReferences(nugget, manager, ui, camera);
            VerifyNoMissingScripts();
            VerifySingleCameraAndListener();

            rock.Initialize();
            Physics.SyncTransforms();

            if (nugget.EvaluateExposureNow())
            {
                throw new InvalidOperationException(
                    $"Gold starts exposed ({nugget.ExposedFraction:P0}); it must begin buried.");
            }

            if (!rock.IsSolidAtWorldPoint(nugget.InteractionCollider.bounds.center))
            {
                throw new InvalidOperationException("Gold center is not inside the initial rock density.");
            }

            Vector3 cameraToGold =
                nugget.InteractionCollider.bounds.center - camera.transform.position;
            if (!Physics.Raycast(
                    camera.transform.position,
                    cameraToGold.normalized,
                    out RaycastHit initialHit,
                    cameraToGold.magnitude,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) ||
                initialHit.collider != rock.RockCollider)
            {
                throw new InvalidOperationException(
                    "The initial gold line of sight is not blocked by the voxel rock.");
            }

            SerializedObject toolProperties = new SerializedObject(miningTool);
            float miningDistance = toolProperties.FindProperty("miningDistance").floatValue;
            float miningRadius = toolProperties.FindProperty("miningRadius").floatValue;
            float miningStrength = toolProperties.FindProperty("miningStrength").floatValue;
            float initialFraction = nugget.ExposedFraction;
            int successfulHits = MineUntilExposed(
                rock,
                nugget,
                camera,
                miningDistance,
                miningRadius,
                miningStrength);

            if (!nugget.IsExposed || nugget.ExposedFraction < 0.5f)
            {
                throw new InvalidOperationException(
                    $"Gold did not reach 50% exposure. fraction={nugget.ExposedFraction:F3}");
            }

            Debug.Log(
                "GOLD_NUGGET_MVP_EDITMODE_TEST_PASS " +
                $"position={nugget.transform.position} " +
                $"initialExposure={initialFraction:F3} " +
                $"finalExposure={nugget.ExposedFraction:F3} " +
                $"successfulHits={successfulHits} " +
                $"pickupDistance={nugget.PickupDistance:F2}");
        }

        internal static int MineUntilExposed(
            VoxelRock rock,
            GoldNuggetMVP nugget,
            Camera camera,
            float distance,
            float radius,
            float strength,
            int maximumAttempts = 80)
        {
            Bounds goldBounds = nugget.InteractionCollider.bounds;
            Vector3[] offsets =
            {
                Vector3.zero,
                Vector3.right * goldBounds.extents.x * 0.8f,
                Vector3.left * goldBounds.extents.x * 0.8f,
                Vector3.up * goldBounds.extents.y * 0.8f,
                Vector3.down * goldBounds.extents.y * 0.8f,
                (Vector3.right + Vector3.up).normalized * goldBounds.extents.x * 0.7f,
                (Vector3.left + Vector3.up).normalized * goldBounds.extents.x * 0.7f,
                (Vector3.right + Vector3.down).normalized * goldBounds.extents.x * 0.7f,
                (Vector3.left + Vector3.down).normalized * goldBounds.extents.x * 0.7f
            };

            int successfulHits = 0;
            for (int attempt = 0; attempt < maximumAttempts && !nugget.IsExposed; attempt++)
            {
                Vector3 target = goldBounds.center + offsets[attempt % offsets.Length];
                Ray ray = new Ray(
                    camera.transform.position,
                    (target - camera.transform.position).normalized);
                if (!Physics.Raycast(
                        ray,
                        out RaycastHit hit,
                        distance,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) ||
                    hit.collider != rock.RockCollider)
                {
                    continue;
                }

                if (rock.Mine(hit.point, radius, strength))
                {
                    successfulHits++;
                    Physics.SyncTransforms();
                    nugget.EvaluateExposureNow();
                }
            }

            return successfulHits;
        }

        private static void VerifySceneReferences(
            GoldNuggetMVP nugget,
            MVPGameManager manager,
            MVPUI ui,
            Camera camera)
        {
            if (manager.GoldNugget != nugget || manager.UI != ui)
            {
                throw new InvalidOperationException("MVPGameManager references are broken.");
            }

            if (nugget.InteractionCollider == null || !nugget.InteractionCollider.isTrigger)
            {
                throw new InvalidOperationException("Gold needs its trigger pickup Collider.");
            }

            if (nugget.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast"))
            {
                throw new InvalidOperationException(
                    "Gold must stay on Ignore Raycast so existing mining rays are not blocked.");
            }

            if (nugget.GetComponent<Rigidbody>() != null)
            {
                throw new InvalidOperationException("Gold MVP does not need a Rigidbody.");
            }

            Renderer[] renderers = nugget.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || !renderers[0].enabled)
            {
                throw new InvalidOperationException("Gold needs an enabled MeshRenderer at startup.");
            }

            if (nugget.PickupDistance < 2f || nugget.PickupDistance > 3f)
            {
                throw new InvalidOperationException("Gold pickup distance must remain between 2m and 3m.");
            }

            if (camera.gameObject.GetComponent<MiningTool>() == null)
            {
                throw new InvalidOperationException("MiningTool is no longer on the Main Camera.");
            }
        }

        private static void VerifyNoMissingScripts()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    MonoBehaviour[] behaviours =
                        transforms[transformIndex].GetComponents<MonoBehaviour>();
                    for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                    {
                        if (behaviours[behaviourIndex] == null)
                        {
                            throw new InvalidOperationException(
                                $"Missing Script found on {transforms[transformIndex].name}.");
                        }
                    }
                }
            }
        }

        private static void VerifySingleCameraAndListener()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include);
            int enabledCameras = 0;
            int enabledListeners = 0;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].enabled && cameras[i].gameObject.scene.IsValid()) enabledCameras++;
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].enabled && listeners[i].gameObject.scene.IsValid())
                    enabledListeners++;
            }

            if (enabledCameras != 1 || enabledListeners != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one Camera and AudioListener, got {enabledCameras}/{enabledListeners}.");
            }
        }
    }

    [InitializeOnLoad]
    public static class GoldNuggetMvpPlayModeVerifier
    {
        private const string ScenePath =
            "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";
        private const string RunKey = "BornToDig.GoldMVP.PlayVerifierRunning";
        private const string ResultKey = "BornToDig.GoldMVP.PlayVerifierResult";
        private const string MessageKey = "BornToDig.GoldMVP.PlayVerifierMessage";

        private static readonly List<string> RuntimeErrors = new List<string>();

        private static double enteredPlayModeAt;
        private static int phase;
        private static int miningAttempts;
        private static int successfulHits;
        private static float miningDistance;
        private static float miningRadius;
        private static float miningStrength;
        private static double clearDeadline;

        private static VoxelRock rock;
        private static MiningTool miningTool;
        private static GoldNuggetMVP nugget;
        private static MVPGameManager manager;
        private static MVPUI ui;
        private static FpsCharacterController fps;
        private static Camera camera;

        static GoldNuggetMvpPlayModeVerifier()
        {
            if (SessionState.GetBool(RunKey, false))
            {
                RegisterCallbacks();
            }
        }

        public static void VerifyBatch()
        {
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(ResultKey, 0);
            SessionState.SetString(MessageKey, string.Empty);
            EditorSceneManager.OpenScene(ScenePath);
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
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RuntimeErrors.Clear();
                Application.logMessageReceived -= HandleRuntimeLog;
                Application.logMessageReceived += HandleRuntimeLog;
                phase = 0;
                miningAttempts = 0;
                successfulHits = 0;
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
                string message = SessionState.GetString(MessageKey, "Unknown play mode result.");
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
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                return;
            }

            try
            {
                if (phase == 0)
                {
                    if (EditorApplication.timeSinceStartup - enteredPlayModeAt < 0.5d)
                    {
                        return;
                    }

                    CaptureRuntimeReferences();
                    VerifyInitialPlayState();
                    phase = 1;
                    return;
                }

                if (phase == 1)
                {
                    RunOneMiningAttempt();
                    if (nugget.IsExposed)
                    {
                        phase = 2;
                    }
                    else if (miningAttempts >= 80)
                    {
                        throw new InvalidOperationException(
                            $"Play Mode mining did not expose gold. fraction={nugget.ExposedFraction:F3}");
                    }

                    return;
                }

                if (phase == 2)
                {
                    MovePlayerToPickupPosition();
                    if (!nugget.IsCameraTargetingPickup())
                    {
                        throw new InvalidOperationException(
                            "Gold is exposed but cannot be targeted from 2m at screen center.");
                    }

                    phase = 3;
                    return;
                }

                if (phase == 3)
                {
                    if (!ui.PickupPromptVisible)
                    {
                        return;
                    }

                    if (ui.PickupPromptText != "E 金塊を拾う")
                    {
                        throw new InvalidOperationException(
                            $"Pickup prompt text is incorrect: {ui.PickupPromptText}");
                    }

                    if (!nugget.CollectIfAvailable())
                    {
                        throw new InvalidOperationException("Exposed targeted gold could not be collected.");
                    }

                    if (manager.CollectedCount != 1 || nugget.IsCollected == false ||
                        nugget.InteractionCollider.enabled ||
                        ui.ObjectiveText != "金塊を入手！ 1 / 1")
                    {
                        throw new InvalidOperationException(
                            "Pickup did not set count, hide gold, or update the objective UI.");
                    }

                    clearDeadline = EditorApplication.timeSinceStartup + 1.5d;
                    phase = 4;
                    return;
                }

                if (phase == 4)
                {
                    if (!manager.IsClear && EditorApplication.timeSinceStartup < clearDeadline)
                    {
                        return;
                    }

                    if (!manager.IsClear || !ui.ClearVisible || ui.ClearTitle != "MVP CLEAR" ||
                        ui.ClearSubtitle != "金塊を発見しました！")
                    {
                        throw new InvalidOperationException(
                            "Clear UI did not appear with the required text after pickup.");
                    }

                    if (RuntimeErrors.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "Runtime errors were logged: " + string.Join(" | ", RuntimeErrors));
                    }

                    Complete(
                        true,
                        "GOLD_NUGGET_MVP_PLAYMODE_TEST_PASS " +
                        $"position={nugget.transform.position} " +
                        $"exposure={nugget.ExposedFraction:F3} " +
                        $"successfulHits={successfulHits} " +
                        "pickupCount=1 clear=True");
                }
            }
            catch (Exception exception)
            {
                Complete(false, "GOLD_NUGGET_MVP_PLAYMODE_TEST_FAIL " + exception);
            }
        }

        private static void CaptureRuntimeReferences()
        {
            rock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();
            miningTool = UnityEngine.Object.FindAnyObjectByType<MiningTool>();
            nugget = UnityEngine.Object.FindAnyObjectByType<GoldNuggetMVP>();
            manager = UnityEngine.Object.FindAnyObjectByType<MVPGameManager>();
            ui = UnityEngine.Object.FindAnyObjectByType<MVPUI>();
            fps = UnityEngine.Object.FindAnyObjectByType<FpsCharacterController>();
            camera = Camera.main;

            if (rock == null || miningTool == null || nugget == null || manager == null ||
                ui == null || fps == null || camera == null)
            {
                throw new InvalidOperationException("Required Play Mode objects are missing.");
            }

            SerializedObject toolProperties = new SerializedObject(miningTool);
            miningDistance = toolProperties.FindProperty("miningDistance").floatValue;
            miningRadius = toolProperties.FindProperty("miningRadius").floatValue;
            miningStrength = toolProperties.FindProperty("miningStrength").floatValue;
        }

        private static void VerifyInitialPlayState()
        {
            if (!rock.IsInitialized || nugget.IsExposed || nugget.IsCollected ||
                manager.CollectedCount != 0)
            {
                throw new InvalidOperationException(
                    "Initial Play Mode state is invalid: rock/gold/count mismatch.");
            }

            if (ui.ObjectiveText != "金塊を探す 0 / 1" ||
                ui.PickupPromptVisible || ui.ClearVisible)
            {
                throw new InvalidOperationException("Initial MVP UI state is incorrect.");
            }

            if (ui.ActiveFont == null || !ui.ActiveFont.HasCharacter('金'))
            {
                throw new InvalidOperationException(
                    "MVP UI does not have a Japanese-capable TMP font at runtime.");
            }

            if (!fps.enabled || camera.GetComponent<MiningTool>() == null)
            {
                throw new InvalidOperationException("FPS or existing MiningTool is disabled.");
            }

            if (!rock.IsSolidAtWorldPoint(nugget.InteractionCollider.bounds.center))
            {
                throw new InvalidOperationException("Gold does not start inside solid rock.");
            }
        }

        private static void RunOneMiningAttempt()
        {
            Bounds bounds = nugget.InteractionCollider.bounds;
            Vector3[] offsets =
            {
                Vector3.zero,
                Vector3.right * bounds.extents.x * 0.8f,
                Vector3.left * bounds.extents.x * 0.8f,
                Vector3.up * bounds.extents.y * 0.8f,
                Vector3.down * bounds.extents.y * 0.8f,
                (Vector3.right + Vector3.up).normalized * bounds.extents.x * 0.7f,
                (Vector3.left + Vector3.up).normalized * bounds.extents.x * 0.7f,
                (Vector3.right + Vector3.down).normalized * bounds.extents.x * 0.7f,
                (Vector3.left + Vector3.down).normalized * bounds.extents.x * 0.7f
            };

            Vector3 target = bounds.center + offsets[miningAttempts % offsets.Length];
            miningAttempts++;
            Ray ray = new Ray(
                camera.transform.position,
                (target - camera.transform.position).normalized);
            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    miningDistance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider == rock.RockCollider &&
                rock.Mine(hit.point, miningRadius, miningStrength))
            {
                successfulHits++;
                Physics.SyncTransforms();
                nugget.EvaluateExposureNow();
            }
        }

        private static void MovePlayerToPickupPosition()
        {
            Vector3 goldCenter = nugget.InteractionCollider.bounds.center;
            Vector3 outward = camera.transform.position - goldCenter;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) outward = Vector3.back;
            outward.Normalize();

            CharacterController characterController = fps.GetComponent<CharacterController>();
            fps.SetGameplayInputEnabled(false);
            if (characterController != null) characterController.enabled = false;

            Vector3 desiredCameraPosition = goldCenter + outward * 2f;
            fps.transform.position += desiredCameraPosition - camera.transform.position;

            Vector3 direction = (goldCenter - camera.transform.position).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            float pitch = -Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
            fps.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Physics.SyncTransforms();
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
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }

            EditorApplication.update -= RunPlayModeStep;
            SessionState.SetInt(ResultKey, success ? 1 : -1);
            SessionState.SetString(MessageKey, message);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
