using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Data
{
    /// <summary>The set of species present in a reserve. Journal order follows list order.</summary>
    [CreateAssetMenu(menuName = "WildLens/Species Database", fileName = "SpeciesDatabase")]
    public class SpeciesDatabase : ScriptableObject
    {
        public List<SpeciesDefinition> species = new List<SpeciesDefinition>();

        public int Count => species.Count;

        public SpeciesDefinition Get(string speciesId)
        {
            for (int i = 0; i < species.Count; i++)
                if (species[i] != null && species[i].speciesId == speciesId) return species[i];
            return null;
        }

        /// <summary>In-memory database with the default species, used when no asset is assigned.</summary>
        public static SpeciesDatabase CreateRuntimeDefault()
        {
            var db = CreateInstance<SpeciesDatabase>();
            db.name = "SpeciesDatabase (Runtime Default)";
            db.species.AddRange(SpeciesDefaults.CreateAll());
            return db;
        }
    }
}
