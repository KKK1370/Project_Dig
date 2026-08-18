using UnityEngine;

namespace BornToDig.VoxelMining
{
    /// <summary>
    /// Stores scalar density samples in the rock's local space.
    /// A value of 1 is solid, 0 is empty, and the surface is at IsoLevel.
    /// </summary>
    public sealed class VoxelGrid
    {
        private readonly float[,,] densities;

        public int Resolution { get; }
        public Bounds LocalBounds { get; }
        public Vector3 Spacing { get; }

        public VoxelGrid(int resolution, Bounds localBounds)
        {
            Resolution = Mathf.Max(4, resolution);
            LocalBounds = localBounds;
            densities = new float[Resolution, Resolution, Resolution];
            Spacing = localBounds.size / (Resolution - 1f);
        }

        public float this[int x, int y, int z]
        {
            get => densities[x, y, z];
            set => densities[x, y, z] = Mathf.Clamp01(value);
        }

        public Vector3 GridToLocal(int x, int y, int z)
        {
            return LocalBounds.min + Vector3.Scale(new Vector3(x, y, z), Spacing);
        }

        public Vector3 LocalToGrid(Vector3 localPosition)
        {
            Vector3 relative = localPosition - LocalBounds.min;
            return new Vector3(
                relative.x / Spacing.x,
                relative.y / Spacing.y,
                relative.z / Spacing.z);
        }

        public float SampleTrilinear(Vector3 localPosition)
        {
            Vector3 gridPosition = LocalToGrid(localPosition);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gridPosition.x), 0, Resolution - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(gridPosition.y), 0, Resolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(gridPosition.z), 0, Resolution - 1);
            int x1 = Mathf.Min(x0 + 1, Resolution - 1);
            int y1 = Mathf.Min(y0 + 1, Resolution - 1);
            int z1 = Mathf.Min(z0 + 1, Resolution - 1);

            float tx = Mathf.Clamp01(gridPosition.x - x0);
            float ty = Mathf.Clamp01(gridPosition.y - y0);
            float tz = Mathf.Clamp01(gridPosition.z - z0);

            float x00 = Mathf.Lerp(densities[x0, y0, z0], densities[x1, y0, z0], tx);
            float x10 = Mathf.Lerp(densities[x0, y1, z0], densities[x1, y1, z0], tx);
            float x01 = Mathf.Lerp(densities[x0, y0, z1], densities[x1, y0, z1], tx);
            float x11 = Mathf.Lerp(densities[x0, y1, z1], densities[x1, y1, z1], tx);
            float y0Value = Mathf.Lerp(x00, x10, ty);
            float y1Value = Mathf.Lerp(x01, x11, ty);
            return Mathf.Lerp(y0Value, y1Value, tz);
        }

        public Vector3 SampleGradient(Vector3 localPosition)
        {
            float xStep = Mathf.Max(Spacing.x * 0.5f, 0.0001f);
            float yStep = Mathf.Max(Spacing.y * 0.5f, 0.0001f);
            float zStep = Mathf.Max(Spacing.z * 0.5f, 0.0001f);

            float dx = SampleTrilinear(localPosition + Vector3.right * xStep) -
                       SampleTrilinear(localPosition - Vector3.right * xStep);
            float dy = SampleTrilinear(localPosition + Vector3.up * yStep) -
                       SampleTrilinear(localPosition - Vector3.up * yStep);
            float dz = SampleTrilinear(localPosition + Vector3.forward * zStep) -
                       SampleTrilinear(localPosition - Vector3.forward * zStep);

            return new Vector3(dx / (2f * xStep), dy / (2f * yStep), dz / (2f * zStep));
        }

        /// <summary>
        /// Reduces density inside a world-space sphere. Returns true when data changed.
        /// </summary>
        public bool CarveSphere(
            Transform rockTransform,
            Vector3 worldCenter,
            float worldRadius,
            float strength)
        {
            return CarveSphereAmount(rockTransform, worldCenter, worldRadius, strength) > 0f;
        }

        /// <summary>
        /// Reduces density and returns the exact accumulated density removed.
        /// </summary>
        public float CarveSphereAmount(
            Transform rockTransform,
            Vector3 worldCenter,
            float worldRadius,
            float strength)
        {
            worldRadius = Mathf.Max(0.001f, worldRadius);
            strength = Mathf.Max(0f, strength);
            if (strength <= 0f)
            {
                return 0f;
            }

            Vector3 localCenter = rockTransform.InverseTransformPoint(worldCenter);
            Vector3 scale = rockTransform.lossyScale;
            Vector3 localRadius = new Vector3(
                worldRadius / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                worldRadius / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                worldRadius / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));

            Vector3 minimumGrid = LocalToGrid(localCenter - localRadius);
            Vector3 maximumGrid = LocalToGrid(localCenter + localRadius);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(minimumGrid.x) - 1, 0, Resolution - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(minimumGrid.y) - 1, 0, Resolution - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt(minimumGrid.z) - 1, 0, Resolution - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(maximumGrid.x) + 1, 0, Resolution - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(maximumGrid.y) + 1, 0, Resolution - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt(maximumGrid.z) + 1, 0, Resolution - 1);

            float radiusSquared = worldRadius * worldRadius;
            float removedDensity = 0f;

            for (int z = zMin; z <= zMax; z++)
            for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
            {
                float oldDensity = densities[x, y, z];
                if (oldDensity <= 0f)
                {
                    continue;
                }

                Vector3 worldSample = rockTransform.TransformPoint(GridToLocal(x, y, z));
                float distanceSquared = (worldSample - worldCenter).sqrMagnitude;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float normalizedDistance = Mathf.Sqrt(distanceSquared) / worldRadius;
                float smoothFalloff = 1f - normalizedDistance * normalizedDistance *
                    (3f - 2f * normalizedDistance);
                float densityReduction = strength * Mathf.Lerp(0.2f, 1f, smoothFalloff);
                float newDensity = Mathf.Max(0f, oldDensity - densityReduction);

                if (!Mathf.Approximately(newDensity, oldDensity))
                {
                    densities[x, y, z] = newDensity;
                    removedDensity += oldDensity - newDensity;
                }
            }

            return removedDensity;
        }
    }
}
