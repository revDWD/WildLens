using System;
using System.IO;
using UnityEngine;
using WildLens.Data;
using WildLens.Journal;

namespace WildLens
{
    /// <summary>
    /// Scene-level composition root. Owns long-lived services (species database, journal) and global UI state
    /// (whether a menu is open, which pauses the world and frees the cursor). Other systems find it via Instance.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WildLensGame : MonoBehaviour
    {
        [SerializeField] private SpeciesDatabase speciesDatabase;
        [Tooltip("Journal file name inside persistentDataPath/WildLens/.")]
        [SerializeField] private string journalFileName = "journal.json";

        public static WildLensGame Instance { get; private set; }

        public SpeciesDatabase Species { get; private set; }
        public JournalService Journal { get; private set; }

        public bool IsUiOpen { get; private set; }
        /// <summary>Frame on which a menu closed; input that closed it shouldn't be reused that frame.</summary>
        public int UiClosedFrame { get; private set; } = -1;
        public event Action<bool> UiOpenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WildLens] Duplicate WildLensGame found; destroying the extra one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Species = speciesDatabase != null ? speciesDatabase : SpeciesDatabase.CreateRuntimeDefault();
            string path = Path.Combine(Application.persistentDataPath, "WildLens", journalFileName);
            Journal = new JournalService(Species, path);
            Journal.Load();
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        public void SetUiOpen(bool open)
        {
            if (IsUiOpen == open) return;
            IsUiOpen = open;
            if (!open) UiClosedFrame = Time.frameCount;
            Time.timeScale = open ? 0f : 1f;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            UiOpenChanged?.Invoke(open);
        }

        [ContextMenu("Reset Journal")]
        private void ResetJournal()
        {
            if (Journal != null) Journal.ResetJournal();
            else Debug.Log("[WildLens] Enter Play mode to reset the journal.");
        }
    }
}
