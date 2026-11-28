using UnityEditor;
using UnityEngine;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Upgrades SpeciesDefinition assets created by an older package version after each script reload.
    ///
    /// When the data version rises, the five built-in species get the current gameplay tuning
    /// (<see cref="SpeciesDefaults.ApplyTuning"/>: awareness, behaviours, photo requirements, observation,
    /// habitat hotspots, group size). Visuals, prefabs and movement are left as you tuned them.
    /// Custom species keep their values; old alert/flee distances carry over via FormerlySerializedAs.
    /// Data version 4 (1.9): an asset already at version 3 gets only the natural-events data (SpeciesDefaults.ApplyHabits), never the full tuning.
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
                if (defaults != null && asset.dataVersion >= 3)
                {
                    // 1.9 (version 3 → 4): only the natural-events data arrives (weather habits, the hints in the field notes and, for the
                    // few species that changed, the rhythm and colours). Awareness, behaviours, groups and anything tuned by hand stay as they are.
                    SpeciesDefaults.ApplyHabits(asset, defaults);
                    asset.dataVersion = SpeciesDefinition.CurrentDataVersion;
                    Object.DestroyImmediate(defaults);
                }
                else if (defaults != null)
                {
                    SpeciesDefaults.ApplyTuning(asset, defaults); // also brings the habits
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
                Debug.Log($"[WildLens] Upgraded {upgraded} species asset(s) to data version {SpeciesDefinition.CurrentDataVersion} (gameplay tuning).");
            }
        }
    }
}
