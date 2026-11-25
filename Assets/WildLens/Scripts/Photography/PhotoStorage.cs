using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildLens.Photography
{
    /// <summary>Writes photographs to disk and caches thumbnails for the UI.</summary>
    public static class PhotoStorage
    {
        public static string PhotoDirectory => Path.Combine(Application.persistentDataPath, "WildLens", "Photos");

        private static readonly Dictionary<string, Texture2D> Thumbnails = new Dictionary<string, Texture2D>();

        public static string Save(Texture2D image, string photoId, int jpgQuality)
        {
            try
            {
                Directory.CreateDirectory(PhotoDirectory);
                string path = Path.Combine(PhotoDirectory, photoId + ".jpg");
                File.WriteAllBytes(path, image.EncodeToJPG(Mathf.Clamp(jpgQuality, 1, 100)));
                return path;
            }
            catch (IOException e)
            {
                Debug.LogError($"[WildLens] Could not save photo: {e.Message}");
                return null;
            }
        }

        public static void CacheThumbnail(string photoId, Texture2D thumbnail)
        {
            if (!string.IsNullOrEmpty(photoId) && thumbnail != null) Thumbnails[photoId] = thumbnail;
        }

        /// <summary>Thumbnail from cache, or loaded from disk (null if the file is gone).</summary>
        public static Texture2D GetThumbnail(string photoId, string imagePath)
        {
            if (string.IsNullOrEmpty(photoId)) return null;
            if (Thumbnails.TryGetValue(photoId, out var tex) && tex != null) return tex;
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return null;

            var loaded = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!loaded.LoadImage(File.ReadAllBytes(imagePath)))
            {
                Object.Destroy(loaded);
                return null;
            }
            var thumb = Downscale(loaded, 384);
            if (thumb != loaded) Object.Destroy(loaded);
            Thumbnails[photoId] = thumb;
            return thumb;
        }

        public static Texture2D Downscale(Texture2D source, int width)
        {
            if (source.width <= width) return source;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * (source.height / (float)source.width)));
            var rt = RenderTexture.GetTemporary(width, height, 0);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }
    }
}
