#if UNITY_EDITOR
using System;
using System.Reflection;
using BornToDig.VoxelMining;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    /// <summary>
    /// Connects the voxel-rock scene to the project's existing FPS character without
    /// adding a second camera, a second camera controller, or a second crosshair.
    /// </summary>
    public static class VoxelRockFpsCompatibility
    {
        private const string ScenePath =
            "Assets/BornToDig/VoxelRock/Scenes/VoxelRockMVP.unity";
        private const string PlayerName = "MVP_FPS_Player";

        [MenuItem("Tools/BORN TO DIG/Integrate FPS Player With Voxel Rock Scene")]
        public static void IntegrateFromMenu()
        {
            IntegrateBatch();
            EditorUtility.DisplayDialog(
                "BORN TO DIG",
                "The voxel-rock scene now uses the existing FPS player, one camera, and one crosshair.",
                "OK");
        }

        public static void IntegrateBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VoxelRock rock = UnityEngine.Object.FindAnyObjectByType<VoxelRock>();
            if (rock == null)
            {
                throw new InvalidOperationException("VoxelRockMVP scene does not contain a VoxelRock.");
            }

            Bounds rockBounds = FindRockBounds(rock.gameObject);

            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                camera = CreateCameraFacingRock(rockBounds);
            }

            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                InvokeExistingFpsBuilder();
                player = GameObject.Find(PlayerName);
            }

            if (player == null)
            {
                throw new InvalidOperationException(
                    "The existing FpsCharacterMVP builder did not create MVP_FPS_Player.");
            }

            camera = Camera.main != null ? Camera.main : camera;
            AttachCameraToExistingPlayerIfNeeded(player, camera);
            PlacePlayerOutsideRock(player, camera, rockBounds);
            ConfigurePlayerController(player, camera);
            ConfigureMining(camera, rockBounds);
            DisableFlyCameraControllers(camera.gameObject);
            EnsureSingleEnabledCamera(camera);
            EnsureSingleAudioListener(camera.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save the integrated VoxelRockMVP scene.");
            }

            Selection.activeGameObject = player;
            Debug.Log(
                "BORN TO DIG compatibility pass complete: one FPS player, one camera, " +
                "one crosshair, MiningTool attached, FlyCamera disabled.");
        }

        internal static bool TryCreateExistingFpsPlayer()
        {
            Type builderType = Type.GetType(
                "BornToDig.CharacterMVP.Editor.FpsCharacterBuilder, Assembly-CSharp-Editor");
            MethodInfo createMethod = builderType?.GetMethod(
                "CreateFpsCharacterOnly",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
            {
                return false;
            }

            createMethod.Invoke(null, null);
            return GameObject.Find(PlayerName) != null;
        }

        private static void InvokeExistingFpsBuilder()
        {
            if (!TryCreateExistingFpsPlayer())
            {
                throw new InvalidOperationException(
                    "FpsCharacterMVP is present but its editor builder could not be called.");
            }
        }

        private static Camera CreateCameraFacingRock(Bounds rockBounds)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            float groundY = FindGroundHeight();
            Vector3 target = new Vector3(rockBounds.center.x, groundY + 1.65f, rockBounds.center.z);
            cameraObject.transform.position = new Vector3(
                rockBounds.center.x,
                groundY + 1.65f,
                rockBounds.min.z - 2f);
            cameraObject.transform.LookAt(target);
            return camera;
        }

        private static Bounds FindRockBounds(GameObject rock)
        {
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(rock.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void AttachCameraToExistingPlayerIfNeeded(GameObject player, Camera camera)
        {
            Transform pivot = player.transform.Find("CameraPivot");
            if (pivot != null && !camera.transform.IsChildOf(player.transform))
            {
                camera.transform.SetParent(pivot, true);
            }

            ConfigurePlayerController(player, camera);
        }

        private static void PlacePlayerOutsideRock(
            GameObject player,
            Camera camera,
            Bounds rockBounds)
        {
            float groundY = FindGroundHeight();
            player.transform.position = new Vector3(
                rockBounds.center.x,
                groundY + 0.02f,
                rockBounds.min.z - 2f);

            Transform pivot = player.transform.Find("CameraPivot");
            if (pivot != null)
            {
                camera.transform.SetParent(pivot, false);
                camera.transform.localPosition = Vector3.zero;
            }

            Vector3 cameraPosition = camera.transform.position;
            float targetY = Mathf.Clamp(
                cameraPosition.y,
                rockBounds.min.y + 0.1f,
                rockBounds.max.y - 0.1f);
            Vector3 target = new Vector3(rockBounds.center.x, targetY, rockBounds.center.z);
            Vector3 direction = (target - cameraPosition).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float horizontalLength = new Vector2(direction.x, direction.z).magnitude;
            float pitch = -Mathf.Atan2(direction.y, horizontalLength) * Mathf.Rad2Deg;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static void ConfigurePlayerController(GameObject player, Camera camera)
        {
            MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().FullName !=
                    "BornToDig.CharacterMVP.FpsCharacterController")
                {
                    continue;
                }

                MethodInfo configure = behaviour.GetType().GetMethod(
                    "Configure",
                    BindingFlags.Public | BindingFlags.Instance);
                configure?.Invoke(behaviour, new object[] { camera });
                EditorUtility.SetDirty(behaviour);
                return;
            }
        }

        private static void ConfigureMining(Camera camera, Bounds rockBounds)
        {
            MiningTool miningTool = camera.GetComponent<MiningTool>();
            if (miningTool == null)
            {
                miningTool = camera.gameObject.AddComponent<MiningTool>();
            }

            // CharacterMvpHud already draws the crosshair. Cursor-lock gating stops the
            // click used to resume FPS control from also damaging the rock.
            float distance = Vector3.Distance(camera.transform.position, rockBounds.center) +
                             rockBounds.extents.magnitude + 1f;
            miningTool.Configure(camera, Mathf.Max(4f, distance), 0.2f, 0.75f, false, true);
            EditorUtility.SetDirty(miningTool);
        }

        private static float FindGroundHeight()
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.Find("Plane");
            }

            if (ground != null)
            {
                Collider collider = ground.GetComponent<Collider>();
                if (collider != null)
                {
                    return collider.bounds.max.y;
                }

                Renderer renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.bounds.max.y;
                }
            }

            return 0f;
        }

        private static void DisableFlyCameraControllers(GameObject cameraObject)
        {
            MonoBehaviour[] behaviours = cameraObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == "FlyCameraController")
                {
                    behaviour.enabled = false;
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }

        private static void EnsureSingleEnabledCamera(Camera primary)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                bool shouldEnable = camera == primary;
                if (camera.enabled != shouldEnable)
                {
                    camera.enabled = shouldEnable;
                    EditorUtility.SetDirty(camera);
                }
            }

            primary.gameObject.tag = "MainCamera";
        }

        private static void EnsureSingleAudioListener(GameObject cameraObject)
        {
            AudioListener primary = cameraObject.GetComponent<AudioListener>();
            if (primary == null)
            {
                primary = cameraObject.AddComponent<AudioListener>();
            }

            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                bool shouldEnable = listener == primary;
                if (listener.enabled != shouldEnable)
                {
                    listener.enabled = shouldEnable;
                    EditorUtility.SetDirty(listener);
                }
            }
        }
    }
}
#endif
