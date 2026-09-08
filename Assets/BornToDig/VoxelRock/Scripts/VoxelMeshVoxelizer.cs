using System.Collections.Generic;
using UnityEngine;

namespace BornToDig.VoxelMining
{
    /// <summary>
    /// Converts a closed triangle mesh into solid/empty density samples.
    /// It scans each Y/Z grid line along X and fills pairs of surface intersections.
    /// </summary>
    public static class VoxelMeshVoxelizer
    {
        public static void FillFromMesh(
            VoxelGrid grid,
            Vector3[] rockLocalVertices,
            int[] triangles)
        {
            int resolution = grid.Resolution;
            var intersections = new List<float>[resolution, resolution];
            float barycentricTolerance = 0.00001f;

            for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                Vector3 a = rockLocalVertices[triangles[triangle]];
                Vector3 b = rockLocalVertices[triangles[triangle + 1]];
                Vector3 c = rockLocalVertices[triangles[triangle + 2]];

                Vector2 a2 = new Vector2(a.y, a.z);
                Vector2 b2 = new Vector2(b.y, b.z);
                Vector2 c2 = new Vector2(c.y, c.z);
                float denominator = Cross(b2 - a2, c2 - a2);
                if (Mathf.Abs(denominator) < 0.000000001f)
                {
                    // The ray is parallel to this triangle, so it cannot toggle inside/outside.
                    continue;
                }

                Vector3 minimum = Vector3.Min(a, Vector3.Min(b, c));
                Vector3 maximum = Vector3.Max(a, Vector3.Max(b, c));
                Vector3 minimumGrid = grid.LocalToGrid(minimum);
                Vector3 maximumGrid = grid.LocalToGrid(maximum);
                int yMin = Mathf.Clamp(Mathf.FloorToInt(minimumGrid.y) - 1, 0, resolution - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt(maximumGrid.y) + 1, 0, resolution - 1);
                int zMin = Mathf.Clamp(Mathf.FloorToInt(minimumGrid.z) - 1, 0, resolution - 1);
                int zMax = Mathf.Clamp(Mathf.CeilToInt(maximumGrid.z) + 1, 0, resolution - 1);

                for (int z = zMin; z <= zMax; z++)
                for (int y = yMin; y <= yMax; y++)
                {
                    Vector3 sample3 = grid.GridToLocal(0, y, z);
                    Vector2 sample = new Vector2(sample3.y, sample3.z);
                    Vector2 fromA = sample - a2;
                    float weightB = Cross(fromA, c2 - a2) / denominator;
                    float weightC = Cross(b2 - a2, fromA) / denominator;
                    float weightA = 1f - weightB - weightC;

                    if (weightA < -barycentricTolerance ||
                        weightB < -barycentricTolerance ||
                        weightC < -barycentricTolerance)
                    {
                        continue;
                    }

                    float xIntersection = weightA * a.x + weightB * b.x + weightC * c.x;
                    List<float> row = intersections[y, z];
                    if (row == null)
                    {
                        row = new List<float>();
                        intersections[y, z] = row;
                    }

                    row.Add(xIntersection);
                }
            }

            float mergeTolerance = Mathf.Max(grid.Spacing.x * 0.02f, 0.000001f);
            for (int z = 0; z < resolution; z++)
            for (int y = 0; y < resolution; y++)
            {
                List<float> row = intersections[y, z];
                if (row == null || row.Count < 2)
                {
                    continue;
                }

                row.Sort();
                var unique = new List<float>(row.Count);
                for (int i = 0; i < row.Count; i++)
                {
                    if (unique.Count == 0 ||
                        Mathf.Abs(row[i] - unique[unique.Count - 1]) > mergeTolerance)
                    {
                        unique.Add(row[i]);
                    }
                }

                // Pairs of crossings describe the solid intervals on a closed mesh.
                for (int pair = 0; pair + 1 < unique.Count; pair += 2)
                {
                    float intervalStart = unique[pair] - mergeTolerance;
                    float intervalEnd = unique[pair + 1] + mergeTolerance;
                    for (int x = 0; x < resolution; x++)
                    {
                        float sampleX = grid.GridToLocal(x, y, z).x;
                        if (sampleX >= intervalStart && sampleX <= intervalEnd)
                        {
                            grid[x, y, z] = 1f;
                        }
                    }
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
