using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;
using WildLens.Player;
#if WILDLENS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace WildLens.EditorTools
{
    /// <summary>
    /// WildLens ▸ Capture Comparison Shots: renders a fixed set of views to PNG (1920×1080) under
    /// &lt;project&gt;/WildLensCaptures/&lt;date_time&gt;/. Views: player start, three points along the trail looking
    /// ahead, one looking across the loop, and every <see cref="ScenicViewpoint"/>. Trail views are derived from the
    /// trail, which regenerates identically for the same seed, so captures taken before and after a rebuild line up.
    /// </summary>
    public static class ComparisonCapture
    {
        private struct View
        {
            public string name;
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }

        [MenuItem("WildLens/Capture Comparison Shots", priority = 22)]
        public static void Capture()
        {
            var views = CollectViews();
            if (views.Count == 0)
            {
                EditorUtility.DisplayDialog("WildLens", "Nothing to capture: open a scene with a built WildLens slice.", "OK");
                return;
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "WildLensCaptures",
                DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            Directory.CreateDirectory(folder);

            var source = FindPlayerCamera();
            var go = new GameObject("WildLens Capture Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            if (source != null) cam.CopyFrom(source);
            cam.enabled = false;
            cam.allowHDR = true;
#if WILDLENS_URP
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
#endif
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            int written = 0;
            try
            {
                for (int i = 0; i < views.Count; i++)
                {
                    var v = views[i];
                    EditorUtility.DisplayProgressBar("WildLens", "Capturing " + v.name, i / (float)views.Count);
                    go.transform.SetPositionAndRotation(v.position, v.rotation);
                    cam.fieldOfView = v.fov;
                    cam.targetTexture = rt;
                    cam.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = previous;
                    string file = Path.Combine(folder, $"{i + 1:00}_{Sanitize(v.name)}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    written++;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[WildLens] Capture failed: " + e.Message);
            }
            finally
            {
                cam.targetTexture = null;
                EditorUtility.ClearProgressBar();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }

            Debug.Log($"[WildLens] Captured {written} comparison shots to {folder}");
            EditorUtility.RevealInFinder(folder);
        }

        private static List<View> CollectViews()
        {
            var views = new List<View>();
            var player = SceneQuery.FindOne<FirstPersonController>();
            if (player != null)
                views.Add(new View { name = "Player start", position = player.transform.position + Vector3.up * 1.65f, rotation = player.transform.rotation, fov = 65f });

            var trail = SceneQuery.FindOne<TrailPath>();
            if (trail != null && trail.Points.Count > 1)
            {
                foreach (float f in new[] { 0.25f, 0.5f, 0.75f })
                {
                    float d = trail.Length * f;
                    Vector3 p = TerrainUtil.Ground(trail.GetPoint(d)) + Vector3.up * 1.65f;
                    views.Add(new View { name = $"Trail {Mathf.RoundToInt(f * 100)}%", position = p, rotation = Quaternion.LookRotation(trail.GetDirection(d)), fov = 65f });
                }
                Vector3 centre = Vector3.zero;
                foreach (var q in trail.Points) centre += q;
                centre /= trail.Points.Count;
                Vector3 eye = TerrainUtil.Ground(trail.GetPoint(trail.Length * 0.5f)) + Vector3.up * 1.65f;
                Vector3 look = TerrainUtil.Ground(centre) + Vector3.up * 1.2f - eye;
                views.Add(new View { name = "Across the loop", position = eye, rotation = Quaternion.LookRotation(look), fov = 65f });
            }

            foreach (var vp in SceneQuery.FindAll<ScenicViewpoint>())
                views.Add(new View { name = vp.title, position = vp.transform.position, rotation = vp.transform.rotation, fov = vp.captureFov });
            return views;
        }

        private static Camera FindPlayerCamera()
        {
            var rig = SceneQuery.FindOne<PlayerCameraRig>();
            if (rig != null) return rig.GetComponent<Camera>();
            return Camera.main;
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_').Replace('%', 'p');
        }
    }
}
