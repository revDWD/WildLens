using UnityEngine;

namespace WildLens.Core
{
    /// <summary>Ground queries that work with one or many terrain tiles, falling back to a physics raycast.</summary>
    public static class TerrainUtil
    {
        public static Terrain FindTerrainAt(Vector3 p)
        {
            var terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null) continue;
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (p.x >= pos.x && p.x <= pos.x + size.x && p.z >= pos.z && p.z <= pos.z + size.z)
                    return t;
            }
            return Terrain.activeTerrain;
        }

        public static float SampleHeight(Vector3 p)
        {
            var t = FindTerrainAt(p);
            if (t != null && t.terrainData != null)
                return t.SampleHeight(p) + t.transform.position.y;

            if (Physics.Raycast(new Vector3(p.x, p.y + 500f, p.z), Vector3.down, out var hit, 2000f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return p.y;
        }

        public static Vector3 SampleNormal(Vector3 p)
        {
            var t = FindTerrainAt(p);
            if (t != null && t.terrainData != null)
            {
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                float nx = Mathf.Clamp01((p.x - pos.x) / size.x);
                float nz = Mathf.Clamp01((p.z - pos.z) / size.z);
                return t.terrainData.GetInterpolatedNormal(nx, nz);
            }
            return Vector3.up;
        }

        public static Vector3 Ground(Vector3 p) => new Vector3(p.x, SampleHeight(p), p.z);

        public static float SlopeDegrees(Vector3 p) => Vector3.Angle(SampleNormal(p), Vector3.up);

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
