using UnityEditor;
using UnityEngine;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Upgrades SpeciesDefinition assets created by an older package version after each script reload.
    ///
    /// Version 2 (Camera &amp; Wildlife Awareness): the five built-in species get their tuned awareness radii,
    /// suspicion rates, patience, new behaviour sets and photo-clarity distance; other settings (population,
    /// visuals, prefabs, movement) are left as you tuned them. Custom species keep their values; their old
    /// alert/flee distances carry over automatically via FormerlySerializedAs.
    /// </summary>
    [InitializeOnLoad]
    public static class SpeciesAssetMigrator
    {
        static SpeciesAssetMigrator()
        {
            EditorApplication.delayCall += MigrateAll;
        }

        [MenuItem("WildLens/Upgrade Species Assets", priority = 30)]
        public static void MigrateAll()
        {
            int upgraded = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SpeciesDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue; // package contents are read-only
                var asset = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>(path);
                if (asset == null || asset.dataVersion >= SpeciesDefinition.CurrentDataVersion) continue;

                var defaults = SpeciesDefaults.CreateById(asset.speciesId);
                if (defaults != null)
                {
                    SpeciesDefaults.ApplyVersion2Tuning(asset, defaults);
                    Object.DestroyImmediate(defaults);
                }
                else
                {
                    if (asset.awarenessRadius < asset.alertRadius) asset.awarenessRadius = asset.alertRadius * 1.5f;
                    asset.dataVersion = SpeciesDefinition.CurrentDataVersion;
                }
                EditorUtility.SetDirty(asset);
                upgraded++;
            }

            if (upgraded > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[WildLens] Upgraded {upgraded} species asset(s) to data version {SpeciesDefinition.CurrentDataVersion} (awareness & behaviour tuning).");
            }
        }
    }
}
