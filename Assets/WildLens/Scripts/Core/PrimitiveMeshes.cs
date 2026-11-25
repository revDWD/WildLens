using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Core
{
    /// <summary>Access to Unity's built-in primitive meshes without leaving primitive colliders behind.</summary>
    public static class PrimitiveMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Get(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out var mesh) && mesh != null) return mesh;
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            Meshes[type] = mesh;
            return mesh;
        }

        /// <summary>Creates a child GameObject that renders a primitive (no collider).</summary>
        public static GameObject CreatePart(string name, PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = Get(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static Transform CreatePivot(string name, Transform parent, Vector3 localPos, Vector3 localEuler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go.transform;
        }
    }
}
