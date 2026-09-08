using UnityEngine;

namespace BornToDig.CharacterMVP
{
    [DisallowMultipleComponent]
    public sealed class CharacterMvpHud : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle textStyle;

        private void OnGUI()
        {
            EnsureStyles();

            float uiScale = Mathf.Clamp(Screen.height / 1080f, 0.72f, 1.25f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            float width = Screen.width / uiScale;
            float height = Screen.height / uiScale;
            DrawCrosshair(width, height);

            GUI.Box(new Rect(20f, 20f, 315f, 142f), GUIContent.none);
            GUI.Label(new Rect(38f, 35f, 275f, 28f), "BORN TO DIG - CHARACTER MVP", titleStyle);
            GUI.Label(new Rect(38f, 70f, 270f, 82f),
                "WASD / Arrows   Move\n" +
                "Mouse           Look\n" +
                "Space           Jump\n" +
                "Hold LMB        Swing pickaxe\n" +
                "Esc             Release cursor",
                textStyle);

            GUI.matrix = oldMatrix;
        }

        private void DrawCrosshair(float width, float height)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.82f);

            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            GUI.DrawTexture(new Rect(centerX - 10f, centerY - 1f, 20f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1f, centerY - 10f, 2f, 20f), Texture2D.whiteTexture);
            GUI.color = oldColor;
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

            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f) }
            };
        }
    }
}
