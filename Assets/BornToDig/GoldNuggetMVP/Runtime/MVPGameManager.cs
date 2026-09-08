using System.Collections;
using UnityEngine;

namespace BornToDig.GoldMVP
{
    [DisallowMultipleComponent]
    public sealed class MVPGameManager : MonoBehaviour
    {
        [SerializeField] private GoldNuggetMVP goldNugget;
        [SerializeField] private MVPUI mvpUI;
        [SerializeField, Range(0.5f, 1f)] private float clearDelay = 0.75f;

        private Coroutine clearRoutine;

        public int CollectedCount { get; private set; }
        public bool IsClear { get; private set; }
        public GoldNuggetMVP GoldNugget => goldNugget;
        public MVPUI UI => mvpUI;

        public void Configure(GoldNuggetMVP nugget, MVPUI ui, float delay = 0.75f)
        {
            goldNugget = nugget;
            mvpUI = ui;
            clearDelay = Mathf.Clamp(delay, 0.5f, 1f);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            CollectedCount = 0;
            IsClear = false;

            if (mvpUI != null)
            {
                mvpUI.ShowSearching();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
                clearRoutine = null;
            }
        }

        private void Subscribe()
        {
            if (goldNugget == null)
            {
                return;
            }

            goldNugget.PickupTargetChanged -= HandlePickupTargetChanged;
            goldNugget.PickupTargetChanged += HandlePickupTargetChanged;
            goldNugget.Collected -= HandleCollected;
            goldNugget.Collected += HandleCollected;
        }

        private void Unsubscribe()
        {
            if (goldNugget == null)
            {
                return;
            }

            goldNugget.PickupTargetChanged -= HandlePickupTargetChanged;
            goldNugget.Collected -= HandleCollected;
        }

        private void HandlePickupTargetChanged(bool targeted)
        {
            if (mvpUI != null)
            {
                mvpUI.SetPickupPromptVisible(targeted && CollectedCount == 0);
            }
        }

        private void HandleCollected()
        {
            if (CollectedCount > 0)
            {
                return;
            }

            CollectedCount = 1;
            if (mvpUI != null)
            {
                mvpUI.ShowCollected();
            }

            clearRoutine = StartCoroutine(ShowClearAfterDelay());
        }

        private IEnumerator ShowClearAfterDelay()
        {
            yield return new WaitForSecondsRealtime(clearDelay);
            IsClear = true;
            if (mvpUI != null)
            {
                mvpUI.ShowClear();
            }

            clearRoutine = null;
        }

        private void OnValidate()
        {
            clearDelay = Mathf.Clamp(clearDelay, 0.5f, 1f);
        }
    }
}
