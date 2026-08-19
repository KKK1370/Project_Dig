using BornToDig.CharacterMVP;
using BornToDig.VoxelMining;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BornToDig.MiningSkillMVP
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class MiningSkillProgression : MonoBehaviour
    {
        [Header("Skill point balance")]
        [SerializeField, Min(1)] private int voxelsPerSkillPoint = 2;
        [SerializeField, Min(1f)] private float densityPerSkillPoint = 100f;
        [SerializeField, Min(1)] private int firstUpgradeCost = 3;
        [SerializeField, Min(0)] private int costIncreasePerLevel = 2;
        [SerializeField, Min(1)] private int maximumSpeedLevel = 6;

        [Header("Mining speed")]
        [SerializeField, Range(0.5f, 0.99f)] private float intervalMultiplierPerLevel = 0.82f;
        [SerializeField, Min(0.1f)] private float minimumMiningInterval = 0.16f;

        private ClickableVoxelRock legacyRock;
        private VoxelRock voxelRock;
        private MiningTool miningTool;
        private PickaxeViewModel pickaxe;
        private FpsCharacterController fpsController;
        private Camera targetCamera;
        private float baseMiningInterval = 0.48f;
        private float currentMiningInterval = 0.48f;
        private float nextMineTime;
        private int skillPoints;
        private int speedLevel;
        private int totalVoxelsMined;
        private int voxelPointProgress;
        private float totalDensityMined;
        private float densityPointProgress;
        private float previousTimeScale = 1f;
        private bool skillScreenOpen;
        private GUIStyle titleStyle;
        private GUIStyle valueStyle;
        private GUIStyle hintStyle;
        private GUIStyle buttonStyle;

        public int SkillPoints => skillPoints;
        public int SpeedLevel => speedLevel;
        public int TotalVoxelsMined => totalVoxelsMined;
        public float TotalDensityMined => totalDensityMined;
        public float CurrentMiningInterval => currentMiningInterval;
        public int NextUpgradeCost => firstUpgradeCost + speedLevel * costIncreasePerLevel;
        public bool SkillScreenOpen => skillScreenOpen;

        private void Awake()
        {
            BindReferences();

            if (miningTool != null)
            {
                baseMiningInterval = miningTool.MiningInterval;
            }
            else if (pickaxe != null)
            {
                baseMiningInterval = pickaxe.SwingInterval;
            }

            ApplyMiningSpeed();
        }

        private void OnEnable()
        {
            BindVoxelRock();
            BindLegacyRock();
        }

        private void OnDisable()
        {
            if (voxelRock != null)
            {
                voxelRock.DensityRemoved -= HandleDensityRemoved;
            }

            if (legacyRock != null)
            {
                legacyRock.VoxelsRemoved -= HandleVoxelsRemoved;
            }

            if (skillScreenOpen)
            {
                CloseSkillScreen();
            }
        }

        private void Update()
        {
            BindReferences();
            HandleSkillScreenToggle();
            if (skillScreenOpen)
            {
                return;
            }

            HandleContinuousMining();
        }

        private void BindReferences()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (fpsController == null)
            {
                fpsController = Object.FindAnyObjectByType<FpsCharacterController>();
                if (fpsController != null && skillScreenOpen)
                {
                    fpsController.SetGameplayInputEnabled(false);
                }
            }
            if (miningTool == null)
            {
                miningTool = Object.FindAnyObjectByType<MiningTool>();
                if (miningTool != null)
                {
                    baseMiningInterval = miningTool.MiningInterval;
                    ApplyMiningSpeed();
                }
            }
            if (pickaxe == null)
            {
                pickaxe = Object.FindAnyObjectByType<PickaxeViewModel>();
                if (pickaxe != null)
                {
                    baseMiningInterval = pickaxe.SwingInterval;
                    ApplyMiningSpeed();
                }
            }
            if (voxelRock == null) BindVoxelRock();
            if (legacyRock == null) BindLegacyRock();
        }

        private void BindVoxelRock()
        {
            VoxelRock foundRock = Object.FindAnyObjectByType<VoxelRock>();
            if (foundRock == voxelRock)
            {
                if (voxelRock != null && isActiveAndEnabled)
                {
                    voxelRock.DensityRemoved -= HandleDensityRemoved;
                    voxelRock.DensityRemoved += HandleDensityRemoved;
                }
                return;
            }

            if (voxelRock != null)
            {
                voxelRock.DensityRemoved -= HandleDensityRemoved;
            }

            voxelRock = foundRock;
            if (voxelRock != null && isActiveAndEnabled)
            {
                voxelRock.DensityRemoved -= HandleDensityRemoved;
                voxelRock.DensityRemoved += HandleDensityRemoved;
            }
        }

        private void BindLegacyRock()
        {
            ClickableVoxelRock foundRock = Object.FindAnyObjectByType<ClickableVoxelRock>();
            if (foundRock == legacyRock)
            {
                if (legacyRock != null && isActiveAndEnabled)
                {
                    legacyRock.VoxelsRemoved -= HandleVoxelsRemoved;
                    legacyRock.VoxelsRemoved += HandleVoxelsRemoved;
                }
                return;
            }

            if (legacyRock != null)
            {
                legacyRock.VoxelsRemoved -= HandleVoxelsRemoved;
            }

            legacyRock = foundRock;
            if (legacyRock != null && isActiveAndEnabled)
            {
                legacyRock.VoxelsRemoved -= HandleVoxelsRemoved;
                legacyRock.VoxelsRemoved += HandleVoxelsRemoved;
            }
        }

        private void HandleDensityRemoved(float removedAmount)
        {
            totalDensityMined += removedAmount;
            densityPointProgress += removedAmount;

            while (densityPointProgress >= densityPerSkillPoint)
            {
                densityPointProgress -= densityPerSkillPoint;
                skillPoints++;
            }
        }

        private void HandleVoxelsRemoved(int removedCount)
        {
            totalVoxelsMined += removedCount;
            voxelPointProgress += removedCount;

            while (voxelPointProgress >= voxelsPerSkillPoint)
            {
                voxelPointProgress -= voxelsPerSkillPoint;
                skillPoints++;
            }
        }

        private void HandleSkillScreenToggle()
        {
            bool togglePressed = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
            if (Gamepad.current != null)
            {
                togglePressed |= Gamepad.current.startButton.wasPressedThisFrame;
            }

            if (!togglePressed)
            {
                return;
            }

            if (skillScreenOpen)
            {
                CloseSkillScreen();
            }
            else
            {
                OpenSkillScreen();
            }
        }

        private void OpenSkillScreen()
        {
            skillScreenOpen = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (fpsController != null)
            {
                fpsController.SetGameplayInputEnabled(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void CloseSkillScreen()
        {
            skillScreenOpen = false;
            Time.timeScale = previousTimeScale;

            if (fpsController != null)
            {
                fpsController.SetGameplayInputEnabled(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private bool TryPurchaseSpeedUpgrade()
        {
            if (speedLevel >= maximumSpeedLevel || skillPoints < NextUpgradeCost)
            {
                return false;
            }

            skillPoints -= NextUpgradeCost;
            speedLevel++;
            ApplyMiningSpeed();
            return true;
        }

        private void ApplyMiningSpeed()
        {
            float upgradedInterval = baseMiningInterval * Mathf.Pow(intervalMultiplierPerLevel, speedLevel);
            currentMiningInterval = Mathf.Max(minimumMiningInterval, upgradedInterval);

            if (pickaxe != null)
            {
                pickaxe.SetSwingInterval(currentMiningInterval);
            }

            if (miningTool != null)
            {
                miningTool.SetMiningInterval(currentMiningInterval);
            }
        }

        private void HandleContinuousMining()
        {
            if (miningTool != null)
            {
                return;
            }

            if (legacyRock == null || targetCamera == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool gamepadHeld = Gamepad.current != null && Gamepad.current.rightTrigger.isPressed;
            bool gamepadPressed = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
            bool miningHeld = mouseHeld || gamepadHeld;

            if (!miningHeld)
            {
                return;
            }

            if (mousePressed)
            {
                nextMineTime = Time.unscaledTime + currentMiningInterval;
                return;
            }

            if (gamepadPressed)
            {
                MineOnce();
                nextMineTime = Time.unscaledTime + currentMiningInterval;
                return;
            }

            if (Time.unscaledTime < nextMineTime)
            {
                return;
            }

            MineOnce();
            nextMineTime = Time.unscaledTime + currentMiningInterval;
        }

        private void MineOnce()
        {
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            legacyRock.TryMine(ray);
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.72f, 1.25f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Screen.width / scale;
            float height = Screen.height / scale;
            if (skillScreenOpen)
            {
                DrawSkillScreen(width, height);
            }
            else
            {
                DrawCompactHud(width);
            }

            GUI.matrix = oldMatrix;
        }

        private void DrawCompactHud(float width)
        {
            Rect panel = new Rect(width - 370f, 20f, 350f, 154f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, 310f, 26f), "MINING SKILL MVP", titleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 48f, 310f, 28f),
                $"Skill Points: {skillPoints}", valueStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 76f, 310f, 24f),
                $"Mining Speed Lv: {speedLevel} / {maximumSpeedLevel}", valueStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 103f, 310f, 22f),
                $"Interval: {currentMiningInterval:0.00}s   Mined: {GetMinedValue()}", hintStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 127f, 310f, 22f),
                "TAB: Open Skill Screen", hintStyle);
        }

        private void DrawSkillScreen(float width, float height)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            const float panelWidth = 540f;
            const float panelHeight = 390f;
            Rect panel = new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x + 34f, panel.y + 28f, 472f, 32f),
                "SKILL SCREEN", titleStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 76f, 300f, 30f),
                "MINING SPEED", valueStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 116f, 472f, 26f),
                $"Level                  {speedLevel} / {maximumSpeedLevel}", valueStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 151f, 472f, 26f),
                $"Skill Points           {skillPoints} SP", valueStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 186f, 472f, 24f),
                $"Mining Interval        {currentMiningInterval:0.00} sec", hintStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 215f, 472f, 24f),
                $"Mining Speed           x{baseMiningInterval / currentMiningInterval:0.00}", hintStyle);
            GUI.Label(new Rect(panel.x + 34f, panel.y + 244f, 472f, 24f),
                $"Total Mined            {GetMinedValue()}", hintStyle);

            bool canUpgrade = speedLevel < maximumSpeedLevel && skillPoints >= NextUpgradeCost;
            string buttonText = speedLevel >= maximumSpeedLevel
                ? "MAX LEVEL"
                : $"LEVEL UP   -   {NextUpgradeCost} SP";
            GUI.enabled = canUpgrade;
            if (GUI.Button(
                    new Rect(panel.x + 34f, panel.y + 286f, 300f, 54f),
                    buttonText,
                    buttonStyle))
            {
                TryPurchaseSpeedUpgrade();
            }
            GUI.enabled = true;

            if (!canUpgrade && speedLevel < maximumSpeedLevel)
            {
                GUI.Label(new Rect(panel.x + 350f, panel.y + 298f, 156f, 34f),
                    "Not enough SP", hintStyle);
            }

            if (GUI.Button(
                    new Rect(panel.x + 354f, panel.y + 286f, 152f, 54f),
                    "CLOSE (TAB)",
                    buttonStyle))
            {
                CloseSkillScreen();
            }
        }

        private string GetMinedValue()
        {
            return voxelRock != null
                ? totalDensityMined.ToString("0")
                : totalVoxelsMined.ToString();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.78f, 0.3f) }
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.82f, 0.88f, 0.92f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnValidate()
        {
            voxelsPerSkillPoint = Mathf.Max(1, voxelsPerSkillPoint);
            densityPerSkillPoint = Mathf.Max(1f, densityPerSkillPoint);
            firstUpgradeCost = Mathf.Max(1, firstUpgradeCost);
            costIncreasePerLevel = Mathf.Max(0, costIncreasePerLevel);
            maximumSpeedLevel = Mathf.Max(1, maximumSpeedLevel);
            minimumMiningInterval = Mathf.Max(0.1f, minimumMiningInterval);
        }
    }

    public static class MiningSkillBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateProgressionManager()
        {
            if (Object.FindAnyObjectByType<MiningSkillProgression>() != null)
            {
                return;
            }

            GameObject manager = new GameObject("MiningSkillMVP");
            manager.AddComponent<MiningSkillProgression>();
        }
    }
}
