using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BornToDig.EditorTools
{
    internal static class RockVertexColorDiagnostics
    {
        private const string MenuPath = "BORN TO DIG/Diagnostics/Inspect Selected Mesh Vertex Colors";

        [MenuItem(MenuPath)]
        private static void InspectSelectedMeshVertexColors()
        {
            Mesh mesh = ResolveSelectedMesh();
            if (mesh == null)
            {
                Debug.LogWarning("[RockColor] Mesh、MeshFilter、またはSkinnedMeshRendererを選択してください。");
                return;
            }

            Color32[] colors = mesh.colors32;
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (colors == null || colors.Length == 0)
            {
                Debug.LogWarning(
                    $"[RockColor] NO_VERTEX_COLORS mesh={mesh.name} asset={assetPath} " +
                    $"vertices={mesh.vertexCount} colors=0");
                return;
            }

            Vector4 min = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector4 max = new Vector4(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            Vector4 sum = Vector4.zero;
            Vector4 linearSum = Vector4.zero;
            var uniqueColors = new HashSet<uint>();
            int greenDominantCount = 0;

            foreach (Color32 color32 in colors)
            {
                Color srgb = color32;
                Color linear = srgb.linear;
                Vector4 value = new Vector4(srgb.r, srgb.g, srgb.b, srgb.a);

                min = Vector4.Min(min, value);
                max = Vector4.Max(max, value);
                sum += value;
                linearSum += new Vector4(linear.r, linear.g, linear.b, linear.a);

                if (color32.g > color32.r && color32.g > color32.b)
                {
                    greenDominantCount++;
                }

                uint packed = ((uint)color32.r << 24) | ((uint)color32.g << 16) | ((uint)color32.b << 8) | color32.a;
                uniqueColors.Add(packed);
            }

            Vector4 average = sum / colors.Length;
            Vector4 linearAverage = linearSum / colors.Length;

            Debug.Log(
                $"[RockColor] VERTEX_COLOR_STATS mesh={mesh.name} asset={assetPath} " +
                $"vertices={mesh.vertexCount} colors={colors.Length} uniqueRGBA8={uniqueColors.Count}\n" +
                $"sRGB min={Format(min)} max={Format(max)} avg={Format(average)}\n" +
                $"Linear avg={Format(linearAverage)} greenDominant={greenDominantCount}/{colors.Length}",
                mesh);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateInspectSelectedMeshVertexColors()
        {
            return ResolveSelectedMesh() != null;
        }

        private static Mesh ResolveSelectedMesh()
        {
            if (Selection.activeObject is Mesh selectedMesh)
            {
                return selectedMesh;
            }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return null;
            }

            MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return meshFilter.sharedMesh;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
            return skinnedMeshRenderer != null ? skinnedMeshRenderer.sharedMesh : null;
        }

        private static string Format(Vector4 value)
        {
            return $"({value.x:F4},{value.y:F4},{value.z:F4},{value.w:F4})";
        }
    }
}
