using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BornToDig.EditorTools
{
    public static class BTDVisualLookV1
    {
        public const string Map01ScenePath =
            "Assets/BornToDig/Maps/Map01/Area01/Scenes/Map01_Area01.unity";

        public const string OutdoorProfilePath =
            "Assets/BornToDig/VisualLook/Profiles/BTD_OutdoorWarm_v1.asset";

        public const string OutdoorSkyPath =
            "Assets/BornToDig/VisualLook/Lighting/BTD_OutdoorWarmSky_v1.mat";

        private const string VisualRoot = "Assets/BornToDig/VisualLook";
        private const string ApplyLog = "BTD_VISUAL_LOOK_V1_APPLIED";
        private const string ValidateLog = "BTD_VISUAL_LOOK_V1_VALIDATION_PASS";

        [MenuItem("Tools/BORN TO DIG/Visual Look/Create or Update Shared Assets")]
        public static void CreateOrUpdateSharedAssets()
        {
            EnsureFolders();
            ConfigureOutdoorProfile();
            ConfigureOutdoorSky();
            AssetDatabase.SaveAssets();
            Debug.Log("BTD_VISUAL_LOOK_V1_ASSETS_READY");
        }

        [MenuItem("Tools/BORN TO DIG/Visual Look/Apply Outdoor Warm v1 to Map01 Area01")]
        public static void ApplyOutdoorWarmToMap01()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Map01ScenePath)
            {
                throw new InvalidOperationException(
                    "Open Map01_Area01 before applying BTD Visual Look v1. Active scene: " + scene.path);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before applying BTD Visual Look v1.");
            }

            EnsureFolders();
            VolumeProfile profile = ConfigureOutdoorProfile();
            Material sky = ConfigureOutdoorSky();

            Volume volume = FindSceneComponent<Volume>(scene, "Area01_Global_Volume");
            Light sun = FindSceneComponent<Light>(scene, "Warm_Afternoon_Sun");
            Camera camera = FindSceneComponent<Camera>(scene, "Main Camera");

            if (volume == null || sun == null || camera == null)
            {
                throw new InvalidOperationException(
                    "Map01 visual anchors are incomplete. Expected Area01_Global_Volume, " +
                    "Warm_Afternoon_Sun, and Main Camera.");
            }

            Undo.RecordObjects(new UnityEngine.Object[] { volume, sun, camera }, "Apply BTD Visual Look v1");

            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            sun.color = new Color(1f, 0.94f, 0.82f, 1f);
            sun.useColorTemperature = true;
            sun.colorTemperature = 5200f;
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.84f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;

            camera.allowHDR = true;
            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                throw new InvalidOperationException("Main Camera has no UniversalAdditionalCameraData.");
            }

            cameraData.renderPostProcessing = true;

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.ambientSkyColor = new Color(0.49f, 0.62f, 0.66f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.47f, 0.56f, 0.46f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.21f, 1f);
            RenderSettings.reflectionIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.67f, 0.68f, 1f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 120f;

            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Unity could not save Map01_Area01.");
            }

            Debug.Log(ApplyLog);
        }

        [MenuItem("Tools/BORN TO DIG/Visual Look/Validate Map01 Area01")]
        public static void ValidateMap01Area01()
        {
            Scene scene = SceneManager.GetActiveScene();
            var failures = new List<string>();

            if (scene.path != Map01ScenePath) failures.Add("Map01_Area01 is not active.");

            Volume volume = FindSceneComponent<Volume>(scene, "Area01_Global_Volume");
            Light sun = FindSceneComponent<Light>(scene, "Warm_Afternoon_Sun");
            Camera camera = FindSceneComponent<Camera>(scene, "Main Camera");

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(OutdoorProfilePath);
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(OutdoorSkyPath);

            if (profile == null) failures.Add("Outdoor Volume Profile is missing.");
            if (sky == null) failures.Add("Outdoor sky material is missing.");
            if (volume == null || volume.sharedProfile != profile)
                failures.Add("Area01_Global_Volume does not use BTD_OutdoorWarm_v1.");
            if (sun == null || sun.shadowStrength < 0.8f)
                failures.Add("Warm_Afternoon_Sun does not meet the v1 shadow baseline.");

            UniversalAdditionalCameraData cameraData =
                camera == null ? null : camera.GetComponent<UniversalAdditionalCameraData>();
            if (camera == null || !camera.allowHDR || cameraData == null || !cameraData.renderPostProcessing)
                failures.Add("Main Camera HDR/Post Processing is not enabled.");

            if (profile != null)
            {
                RequireComponent<Tonemapping>(profile, failures);
                RequireComponent<Bloom>(profile, failures);
                RequireComponent<ColorAdjustments>(profile, failures);
                RequireComponent<WhiteBalance>(profile, failures);
                RequireComponent<Vignette>(profile, failures);
            }

            int pinkRenderers = 0;
            int missingMaterialSlots = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            missingMaterialSlots++;
                            continue;
                        }

                        if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                            pinkRenderers++;
                    }
                }
            }

            if (pinkRenderers > 0) failures.Add("Pink/error-shader renderer count: " + pinkRenderers);
            if (missingMaterialSlots > 0) failures.Add("Missing material slot count: " + missingMaterialSlots);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "BTD Visual Look v1 validation failed:\n- " + string.Join("\n- ", failures.ToArray()));
            }

            Debug.Log(ValidateLog +
                      " | profile=" + OutdoorProfilePath +
                      " | pinkRenderers=0 | missingMaterialSlots=0");
        }

        private static VolumeProfile ConfigureOutdoorProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(OutdoorProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "BTD_OutdoorWarm_v1";
                AssetDatabase.CreateAsset(profile, OutdoorProfilePath);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0.18f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.55f);
            bloom.clamp.Override(8f);
            bloom.highQualityFiltering.Override(false);
            bloom.tint.Override(Color.white);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(-0.05f);
            color.contrast.Override(14f);
            color.colorFilter.Override(new Color(1f, 0.98f, 0.94f, 1f));
            color.hueShift.Override(0f);
            color.saturation.Override(-4f);

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.active = true;
            whiteBalance.temperature.Override(6f);
            whiteBalance.tint.Override(-2f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.color.Override(new Color(0.08f, 0.07f, 0.055f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.32f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material ConfigureOutdoorSky()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) throw new InvalidOperationException("Skybox/Procedural shader is unavailable.");

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(OutdoorSkyPath);
            if (sky == null)
            {
                sky = new Material(shader) { name = "BTD_OutdoorWarmSky_v1" };
                AssetDatabase.CreateAsset(sky, OutdoorSkyPath);
            }
            else if (sky.shader != shader)
            {
                sky.shader = shader;
            }

            sky.SetColor("_SkyTint", new Color(0.36f, 0.58f, 0.66f, 1f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.38f, 0.30f, 1f));
            sky.SetFloat("_AtmosphereThickness", 0.90f);
            sky.SetFloat("_Exposure", 0.78f);
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.025f);
            sky.SetFloat("_SunSizeConvergence", 4.5f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component;
            if (!profile.TryGet(out component) || component == null)
                component = profile.Add<T>(true);

            if (AssetDatabase.Contains(profile) && !AssetDatabase.Contains(component))
                AssetDatabase.AddObjectToAsset(component, profile);

            return component;
        }

        private static void RequireComponent<T>(VolumeProfile profile, List<string> failures)
            where T : VolumeComponent
        {
            T component;
            if (!profile.TryGet(out component) || component == null || !component.active)
                failures.Add(typeof(T).Name + " is missing or inactive.");
        }

        private static T FindSceneComponent<T>(Scene scene, string gameObjectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (component.gameObject.name == gameObjectName) return component;
                }
            }

            return null;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/BornToDig", "VisualLook");
            EnsureFolder(VisualRoot, "Profiles");
            EnsureFolder(VisualRoot, "Lighting");
            EnsureFolder(VisualRoot, "Materials");
            EnsureFolder(VisualRoot, "Docs");
            EnsureFolder(VisualRoot, "Editor");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
