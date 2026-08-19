using System;
using System.Collections.Generic;
using UnityEngine;

namespace BornToDig.VoxelMining
{
    /// <summary>
    /// Builds an isosurface one cube at a time. Cube-face contours are connected into
    /// loops, then triangulated. Ambiguous faces use their center density as a decider.
    /// </summary>
    public static class MarchingCubes
    {
        public sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        private static readonly Vector3Int[] CornerOffsets =
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 1, 1),
            new Vector3Int(0, 1, 1)
        };

        private static readonly int[,] EdgeCorners =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        // Edges and corners are listed cyclically around each cube face.
        private static readonly int[,] FaceEdges =
        {
            { 0, 1, 2, 3 },
            { 4, 5, 6, 7 },
            { 0, 9, 4, 8 },
            { 2, 10, 6, 11 },
            { 3, 11, 7, 8 },
            { 1, 10, 5, 9 }
        };

        private static readonly int[,] FaceCorners =
        {
            { 0, 1, 2, 3 },
            { 4, 5, 6, 7 },
            { 0, 1, 5, 4 },
            { 3, 2, 6, 7 },
            { 0, 3, 7, 4 },
            { 1, 2, 6, 5 }
        };

        public static MeshData Generate(VoxelGrid grid, float isoLevel)
        {
            var result = new MeshData();
            var edgeVertexCache = new Dictionary<EdgeKey, int>();
            int resolution = grid.Resolution;

            var cornerDensities = new float[8];
            var cornerPositions = new Vector3[8];
            var activeEdges = new bool[12];
            var edgeVertexIndices = new int[12];
            var neighbors = new int[12, 2];
            var degree = new int[12];
            var visited = new bool[12];
            var activeFaceEdges = new int[4];
            var loop = new List<int>(12);

            for (int z = 0; z < resolution - 1; z++)
            for (int y = 0; y < resolution - 1; y++)
            for (int x = 0; x < resolution - 1; x++)
            {
                bool anySolid = false;
                bool anyEmpty = false;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3Int offset = CornerOffsets[corner];
                    int gx = x + offset.x;
                    int gy = y + offset.y;
                    int gz = z + offset.z;
                    float density = grid[gx, gy, gz];
                    cornerDensities[corner] = density;
                    cornerPositions[corner] = grid.GridToLocal(gx, gy, gz);
                    anySolid |= density >= isoLevel;
                    anyEmpty |= density < isoLevel;
                }

                if (!anySolid || !anyEmpty)
                {
                    continue;
                }

                Array.Clear(activeEdges, 0, activeEdges.Length);
                Array.Clear(degree, 0, degree.Length);
                Array.Clear(visited, 0, visited.Length);
                for (int edge = 0; edge < 12; edge++)
                {
                    neighbors[edge, 0] = -1;
                    neighbors[edge, 1] = -1;
                    int cornerA = EdgeCorners[edge, 0];
                    int cornerB = EdgeCorners[edge, 1];
                    bool solidA = cornerDensities[cornerA] >= isoLevel;
                    bool solidB = cornerDensities[cornerB] >= isoLevel;
                    if (solidA == solidB)
                    {
                        continue;
                    }

                    activeEdges[edge] = true;
                    EdgeKey key = CreateEdgeKey(x, y, z, edge);
                    int vertexIndex;
                    if (!edgeVertexCache.TryGetValue(key, out vertexIndex))
                    {
                        float densityA = cornerDensities[cornerA];
                        float densityB = cornerDensities[cornerB];
                        float denominator = densityB - densityA;
                        float interpolation = Mathf.Abs(denominator) < 0.000001f
                            ? 0.5f
                            : Mathf.Clamp01((isoLevel - densityA) / denominator);
                        Vector3 position = Vector3.Lerp(
                            cornerPositions[cornerA],
                            cornerPositions[cornerB],
                            interpolation);
                        vertexIndex = result.Vertices.Count;
                        result.Vertices.Add(position);
                        edgeVertexCache.Add(key, vertexIndex);
                    }

                    edgeVertexIndices[edge] = vertexIndex;
                }

                for (int face = 0; face < 6; face++)
                {
                    int activeCount = 0;
                    for (int faceEdge = 0; faceEdge < 4; faceEdge++)
                    {
                        int edge = FaceEdges[face, faceEdge];
                        if (activeEdges[edge])
                        {
                            activeFaceEdges[activeCount++] = edge;
                        }
                    }

                    if (activeCount == 2)
                    {
                        AddConnection(activeFaceEdges[0], activeFaceEdges[1], neighbors, degree);
                    }
                    else if (activeCount == 4)
                    {
                        float centerDensity = 0f;
                        for (int i = 0; i < 4; i++)
                        {
                            centerDensity += cornerDensities[FaceCorners[face, i]];
                        }

                        centerDensity *= 0.25f;
                        bool centerSolid = centerDensity >= isoLevel;
                        bool firstCornerSolid =
                            cornerDensities[FaceCorners[face, 0]] >= isoLevel;

                        if (centerSolid == firstCornerSolid)
                        {
                            AddConnection(activeFaceEdges[0], activeFaceEdges[1], neighbors, degree);
                            AddConnection(activeFaceEdges[2], activeFaceEdges[3], neighbors, degree);
                        }
                        else
                        {
                            AddConnection(activeFaceEdges[1], activeFaceEdges[2], neighbors, degree);
                            AddConnection(activeFaceEdges[3], activeFaceEdges[0], neighbors, degree);
                        }
                    }
                }

                for (int start = 0; start < 12; start++)
                {
                    if (!activeEdges[start] || visited[start] || degree[start] != 2)
                    {
                        continue;
                    }

                    loop.Clear();
                    int previous = -1;
                    int current = start;
                    int guard = 0;

                    do
                    {
                        visited[current] = true;
                        loop.Add(current);
                        int next = neighbors[current, 0] != previous
                            ? neighbors[current, 0]
                            : neighbors[current, 1];
                        previous = current;
                        current = next;
                        guard++;
                    }
                    while (current >= 0 && current != start && guard <= 12);

                    if (current != start || loop.Count < 3)
                    {
                        continue;
                    }

                    TriangulateLoop(
                        result,
                        grid,
                        loop,
                        edgeVertexIndices,
                        cornerPositions,
                        cornerDensities,
                        isoLevel);
                }
            }

            return result;
        }

        private static void AddConnection(int a, int b, int[,] neighbors, int[] degree)
        {
            if (a == b || degree[a] >= 2 || degree[b] >= 2)
            {
                return;
            }

            for (int i = 0; i < degree[a]; i++)
            {
                if (neighbors[a, i] == b)
                {
                    return;
                }
            }

            neighbors[a, degree[a]++] = b;
            neighbors[b, degree[b]++] = a;
        }

        private static void TriangulateLoop(
            MeshData result,
            VoxelGrid grid,
            List<int> loop,
            int[] edgeVertexIndices,
            Vector3[] cornerPositions,
            float[] cornerDensities,
            float isoLevel)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < loop.Count; i++)
            {
                center += result.Vertices[edgeVertexIndices[loop[i]]];
            }

            center /= loop.Count;
            int centerIndex = result.Vertices.Count;
            result.Vertices.Add(center);

            Vector3 outward = -grid.SampleGradient(center);
            if (outward.sqrMagnitude < 0.000001f)
            {
                Vector3 solidCenter = Vector3.zero;
                Vector3 emptyCenter = Vector3.zero;
                int solidCount = 0;
                int emptyCount = 0;

                for (int corner = 0; corner < 8; corner++)
                {
                    if (cornerDensities[corner] >= isoLevel)
                    {
                        solidCenter += cornerPositions[corner];
                        solidCount++;
                    }
                    else
                    {
                        emptyCenter += cornerPositions[corner];
                        emptyCount++;
                    }
                }

                if (solidCount > 0 && emptyCount > 0)
                {
                    outward = emptyCenter / emptyCount - solidCenter / solidCount;
                }
            }

            for (int i = 0; i < loop.Count; i++)
            {
                int a = edgeVertexIndices[loop[i]];
                int b = edgeVertexIndices[loop[(i + 1) % loop.Count]];
                Vector3 triangleNormal = Vector3.Cross(
                    result.Vertices[a] - center,
                    result.Vertices[b] - center);

                if (triangleNormal.sqrMagnitude < 0.0000000000000001f)
                {
                    continue;
                }

                result.Triangles.Add(centerIndex);
                if (Vector3.Dot(triangleNormal, outward) >= 0f)
                {
                    result.Triangles.Add(a);
                    result.Triangles.Add(b);
                }
                else
                {
                    result.Triangles.Add(b);
                    result.Triangles.Add(a);
                }
            }
        }

        private static EdgeKey CreateEdgeKey(int x, int y, int z, int edge)
        {
            Vector3Int a = CornerOffsets[EdgeCorners[edge, 0]] + new Vector3Int(x, y, z);
            Vector3Int b = CornerOffsets[EdgeCorners[edge, 1]] + new Vector3Int(x, y, z);
            return new EdgeKey(a, b);
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int x;
            private readonly int y;
            private readonly int z;
            private readonly byte axis;

            public EdgeKey(Vector3Int a, Vector3Int b)
            {
                Vector3Int minimum = new Vector3Int(
                    Mathf.Min(a.x, b.x),
                    Mathf.Min(a.y, b.y),
                    Mathf.Min(a.z, b.z));
                x = minimum.x;
                y = minimum.y;
                z = minimum.z;
                axis = (byte)(a.x != b.x ? 0 : a.y != b.y ? 1 : 2);
            }

            public bool Equals(EdgeKey other)
            {
                return x == other.x && y == other.y && z == other.z && axis == other.axis;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = x;
                    hash = hash * 397 ^ y;
                    hash = hash * 397 ^ z;
                    hash = hash * 397 ^ axis;
                    return hash;
                }
            }
        }
    }
}
