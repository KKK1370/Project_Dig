using System.Collections.Generic;
using BornToDig.Destructibles;
using BornToDig.GoldMVP;
using UnityEngine;

namespace BornToDig.Destructibles.Testing
{
    /// <summary>
    /// Test-only bridge between the pebble cluster and the existing gold pickup flow.
    /// It reacts only to Broken events; it does not poll from Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PebbleGoldExposureTrackerTest : MonoBehaviour
    {
        [SerializeField] private GoldNuggetMVP goldNugget;
        [SerializeField, Min(0.1f)] private float monitoredRadius = 0.92f;
        [SerializeField, Range(0.4f, 0.6f)] private float requiredBrokenFraction = 0.5f;
        [SerializeField, HideInInspector] private int generatedPebbleCount;
        [SerializeField, HideInInspector] private int generationSeed;

        private readonly List<DestructiblePebble> monitoredPebbles = new List<DestructiblePebble>();
        private int brokenCount;

        public GoldNuggetMVP GoldNugget => goldNugget;
        public float MonitoredRadius => monitoredRadius;
        public float RequiredBrokenFraction => requiredBrokenFraction;
        public int GeneratedPebbleCount => generatedPebbleCount;
        public int GenerationSeed => generationSeed;
        public int MonitoredPebbleCount => monitoredPebbles.Count;
        public int BrokenCount => brokenCount;
        public float BrokenFraction => monitoredPebbles.Count == 0
            ? 0f
            : brokenCount / (float)monitoredPebbles.Count;

        public void Configure(
            GoldNuggetMVP nugget,
            float radius,
            float exposureFraction,
            int pebbleCount,
            int seed)
        {
            goldNugget = nugget;
            monitoredRadius = Mathf.Max(0.1f, radius);
            requiredBrokenFraction = Mathf.Clamp(exposureFraction, 0.4f, 0.6f);
            generatedPebbleCount = Mathf.Max(0, pebbleCount);
            generationSeed = seed;
        }

        private void OnEnable()
        {
            RebuildSubscriptions();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        public void RebuildSubscriptions()
        {
            UnsubscribeAll();
            brokenCount = 0;

            if (goldNugget == null)
            {
                return;
            }

            Vector3 goldPosition = goldNugget.transform.position;
            float radiusSquared = monitoredRadius * monitoredRadius;
            DestructiblePebble[] pebbles = GetComponentsInChildren<DestructiblePebble>(true);
            for (int i = 0; i < pebbles.Length; i++)
            {
                DestructiblePebble pebble = pebbles[i];
                if ((pebble.transform.position - goldPosition).sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                monitoredPebbles.Add(pebble);
                pebble.Broken += HandlePebbleBroken;
            }

            goldNugget.ReportExternalExposure(0f);
        }

        private void HandlePebbleBroken()
        {
            brokenCount = Mathf.Min(brokenCount + 1, monitoredPebbles.Count);
            if (goldNugget != null)
            {
                goldNugget.ReportExternalExposure(BrokenFraction);
            }
        }

        private void UnsubscribeAll()
        {
            for (int i = 0; i < monitoredPebbles.Count; i++)
            {
                if (monitoredPebbles[i] != null)
                {
                    monitoredPebbles[i].Broken -= HandlePebbleBroken;
                }
            }

            monitoredPebbles.Clear();
        }

        private void OnValidate()
        {
            monitoredRadius = Mathf.Max(0.1f, monitoredRadius);
            requiredBrokenFraction = Mathf.Clamp(requiredBrokenFraction, 0.4f, 0.6f);
            generatedPebbleCount = Mathf.Max(0, generatedPebbleCount);
        }
    }
}
