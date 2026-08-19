using TMPro;
using UnityEngine;

namespace BornToDig.GoldMVP
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class MVPUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text pickupPromptText;
        [SerializeField] private GameObject clearPanel;
        [SerializeField] private TMP_Text clearTitleText;
        [SerializeField] private TMP_Text clearSubtitleText;
        [SerializeField] private Font japaneseSourceFont;

        private TMP_FontAsset runtimeJapaneseFont;

        public string ObjectiveText => objectiveText != null ? objectiveText.text : string.Empty;
        public string PickupPromptText => pickupPromptText != null ? pickupPromptText.text : string.Empty;
        public bool PickupPromptVisible => pickupPromptText != null && pickupPromptText.gameObject.activeSelf;
        public bool ClearVisible => clearPanel != null && clearPanel.activeSelf;
        public string ClearTitle => clearTitleText != null ? clearTitleText.text : string.Empty;
        public string ClearSubtitle => clearSubtitleText != null ? clearSubtitleText.text : string.Empty;
        public TMP_FontAsset ActiveFont => objectiveText != null ? objectiveText.font : null;

        public void Configure(
            TMP_Text objective,
            TMP_Text pickupPrompt,
            GameObject clearRoot,
            TMP_Text clearTitle,
            TMP_Text clearSubtitle,
            Font japaneseFont)
        {
            objectiveText = objective;
            pickupPromptText = pickupPrompt;
            clearPanel = clearRoot;
            clearTitleText = clearTitle;
            clearSubtitleText = clearSubtitle;
            japaneseSourceFont = japaneseFont;
        }

        private void Awake()
        {
            SetInitialVisibility();
        }

        private void Start()
        {
            // TMP components finish their own Awake initialization first. Assigning here keeps
            // their default-font setup from replacing the Japanese runtime font.
            AssignJapaneseFont();
            ShowSearching();
        }

        private void OnDestroy()
        {
            if (runtimeJapaneseFont != null)
            {
                Destroy(runtimeJapaneseFont);
            }
        }

        public void ShowSearching()
        {
            if (objectiveText != null)
            {
                objectiveText.text = "金塊を探す 0 / 1";
            }

            SetPickupPromptVisible(false);
            if (clearPanel != null)
            {
                clearPanel.SetActive(false);
            }
        }

        public void SetPickupPromptVisible(bool visible)
        {
            if (pickupPromptText == null)
            {
                return;
            }

            pickupPromptText.text = "E 金塊を拾う";
            pickupPromptText.gameObject.SetActive(visible);
        }

        public void ShowCollected()
        {
            if (objectiveText != null)
            {
                objectiveText.text = "金塊を入手！ 1 / 1";
            }

            SetPickupPromptVisible(false);
        }

        public void ShowClear()
        {
            if (clearTitleText != null)
            {
                clearTitleText.text = "MVP CLEAR";
            }

            if (clearSubtitleText != null)
            {
                clearSubtitleText.text = "金塊を発見しました！";
            }

            if (clearPanel != null)
            {
                clearPanel.SetActive(true);
            }
        }

        private void SetInitialVisibility()
        {
            if (pickupPromptText != null)
            {
                pickupPromptText.gameObject.SetActive(false);
            }

            if (clearPanel != null)
            {
                clearPanel.SetActive(false);
            }
        }

        private void AssignJapaneseFont()
        {
            if (japaneseSourceFont != null)
            {
                runtimeJapaneseFont = TMP_FontAsset.CreateFontAsset(japaneseSourceFont);
                if (runtimeJapaneseFont != null)
                {
                    runtimeJapaneseFont.name = "Noto Sans JP MVP Runtime";
                    runtimeJapaneseFont.TryAddCharacters(
                        "金塊を探す拾う入手発見しました！012/ E MVPCLAR");
                }
            }

            TMP_FontAsset fontToUse = runtimeJapaneseFont != null
                ? runtimeJapaneseFont
                : TMP_Settings.defaultFontAsset;
            if (fontToUse == null)
            {
                return;
            }

            TMP_Text[] texts =
            {
                objectiveText,
                pickupPromptText,
                clearTitleText,
                clearSubtitleText
            };

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].font = fontToUse;
                    texts[i].SetAllDirty();
                }
            }
        }
    }
}
