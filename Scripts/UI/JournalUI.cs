using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;
using WildLens.Data;
using WildLens.Journal;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>
    /// Field journal screen (J / Tab). Two pages, switched with A / D:
    ///   Wildlife        lists every species in the reserve: undiscovered ones show as "???" with a habitat hint;
    ///                   discovered ones show photo count, first sighting, best shot and field notes.
    ///   Field notebook  (1.9) the small subjects the player has photographed in macro: only what was found, grouped by
    ///                   kind, never a total, a "?" row or a completion figure.
    /// Opening it pauses the world.
    /// </summary>
    public class JournalUI : MonoBehaviour
    {
        [SerializeField] private string reserveName = "Meadow Loop Reserve";

        private Canvas _canvas;
        private Text _headerStats;
        private readonly List<Image> _rowBackgrounds = new List<Image>();
        private readonly List<Text> _rowTexts = new List<Text>();
        private Text _nameText, _sciText, _detailText, _notesText, _noPhotoText;
        private RawImage _photo;
        private AspectRatioFitter _photoFitter;
        private int _selected;
        private int _offset;                       // first species shown in the list window
        private Text _moreAbove, _moreBelow;
        private bool _dirty = true;

        // Field notebook page (1.9)
        private int _page;                         // 0 Wildlife, 1 Field notebook
        private int _selectedNote, _offsetNote;
        private bool _notesDirty = true;
        private readonly List<NotebookRecord> _notes = new List<NotebookRecord>();
        private Text _tabWildlife, _tabNotebook, _footer, _emptyText;
        private RectTransform _tabLine;
        private float _widthWildlife = 120f, _widthNotebook = 230f;
        private Image _photoFrame;
        private Dictionary<string, NaturalistSubject> _sceneSubjects, _defaultSubjects;
        private float _nextWheelTime;               // wheel steps are at least this far apart (unscaled seconds)
        private const float WheelCooldown = 0.12f;

        // Row pitch 92 px, row height 84 px, first row top at y = 136 below the book's top edge (book 880 px tall).
        // The list column ends at 810 px (divider) and the footer starts at 826 px, so 7 rows fit (last row ends at 772 px).
        private const int VisibleRows = 7;
        private const int RowNameSize = 26, RowNameMinSize = 16;
        private const float RowTextWidth = 420f - 16f - 8f - 4f;   // the label's width less its insets, and a little slack
        private Text _measure;                    // an invisible Text used only to measure a row's name line

        private WildLensGame Game => WildLensGame.Instance;

        private void Start()
        {
            if (Game == null)
            {
                Debug.LogWarning("[WildLens] JournalUI needs a WildLensGame in the scene.");
                enabled = false;
                return;
            }
            Build(Game.Species.Count);
            Game.Journal.Changed += MarkDirty;
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Game != null && Game.Journal != null) Game.Journal.Changed -= MarkDirty;
        }

        private void MarkDirty() { _dirty = true; _notesDirty = true; }

        private void Update()
        {
            bool open = _canvas.gameObject.activeSelf;
            if (!open)
            {
                if (GameInput.JournalTogglePressed && !Game.IsUiOpen && Game.UiClosedFrame != Time.frameCount) SetOpen(true);
                return;
            }

            if (GameInput.JournalTogglePressed || GameInput.CancelPressed) { SetOpen(false); return; }

            // A / D: between the two pages.
            if (GameInput.NavigateLeftPressed && _page != 0) { _page = 0; _dirty = true; }
            if (GameInput.NavigateRightPressed && _page != 1) { _page = 1; _dirty = true; }

            int count = _page == 0 ? Game.Species.Count : _notes.Count;
            if (count > 0)
            {
                // Wheel: direction only (backends report different scales). Wheel up = previous entry.
                float wheel = GameInput.ScrollRaw;
                bool wheelUp = wheel > 0.0001f, wheelDown = wheel < -0.0001f;
                if (wheelUp || wheelDown)
                {
                    // A free-spinning wheel reports many ticks a frame: one entry per cooldown, not a blur through the list.
                    if (Time.unscaledTime < _nextWheelTime) wheelUp = wheelDown = false;
                    else _nextWheelTime = Time.unscaledTime + WheelCooldown;
                }
                int sel = _page == 0 ? _selected : _selectedNote;
                if (GameInput.NavigateUpPressed || wheelUp) { sel = (sel - 1 + count) % count; _dirty = true; }
                if (GameInput.NavigateDownPressed || wheelDown) { sel = (sel + 1) % count; _dirty = true; }
                if (_page == 0) _selected = sel; else _selectedNote = sel;
            }
            if (_dirty) Refresh();
        }

        public void SetOpen(bool open)
        {
            _canvas.gameObject.SetActive(open);
            Game.SetUiOpen(open);
            if (open) { _sceneSubjects = null; _notesDirty = true; Refresh(); }
        }

        // ------------------------------------------------------------------ content

        private void Refresh()
        {
            _dirty = false;
            if (_notesDirty) RebuildNotebook();
            UpdateTabs();
            if (_page == 0) RefreshWildlife(); else RefreshNotebook();
        }

        private void RefreshWildlife()
        {
            var journal = Game.Journal;
            var db = Game.Species;
            _photoFrame.gameObject.SetActive(true);
            _emptyText.enabled = false;
            _footer.text = "W / S / wheel  select species        A / D  field notebook        J / Tab / Esc  close";
            _headerStats.text = $"Species {journal.DiscoveredCount} of {journal.TotalSpecies}     Photographs {journal.TotalPhotos}";

            // The list is a window of VisibleRows rows that follows the selection.
            int rows = _rowTexts.Count;
            if (_selected >= db.Count) _selected = Mathf.Max(0, db.Count - 1);
            if (_selected < _offset) _offset = _selected;
            if (_selected >= _offset + rows) _offset = _selected - rows + 1;
            _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, db.Count - rows));
            _moreAbove.enabled = _offset > 0;
            _moreBelow.enabled = _offset + rows < db.Count;

            for (int slot = 0; slot < rows; slot++)
            {
                int i = _offset + slot;
                if (i >= db.Count) { _rowTexts[slot].text = ""; _rowBackgrounds[slot].color = Color.clear; continue; }
                var s = db.species[i];
                var rec = s != null ? journal.GetRecord(s.speciesId) : null;
                string num = (i + 1).ToString("00");
                _rowTexts[slot].text = rec != null
                    ? $"{num}   {FitName(s.displayName, num + "   ")}\n<size=18>     {rec.photoCount} photo{(rec.photoCount == 1 ? "" : "s")}</size>"
                    : $"{num}   ???\n<size=18>     not yet observed</size>";
                if (rec != null && rec.photoCount == 0)
                    _rowTexts[slot].text = $"{num}   {FitName(s.displayName, num + "   ")}\n<size=18>     observed, no photo yet</size>";
                _rowBackgrounds[slot].color = i == _selected ? UIFactory.PaperDark : new Color(0f, 0f, 0f, 0f);
            }

            var species = _selected < db.Count ? db.species[_selected] : null;
            if (species == null) return;
            var record = journal.GetRecord(species.speciesId);

            if (record == null)
            {
                _nameText.text = "Unknown species";
                _sciText.text = "";
                _detailText.text = "<b>Photographs</b>   0\n<b>First sighting</b>   —";
                _notesText.text = "<i>Not yet observed.</i>  " + HabitatHint(species);
                ShowPhoto(null);
                return;
            }

            _nameText.text = species.displayName;
            _sciText.text = species.scientificName;
            var best = journal.GetPhoto(record.bestPhotoId);
            string photos = record.photoCount > 0 ? record.photoCount.ToString()
                : "0   <size=18><i>identified by observation</i></size>";
            _detailText.text =
                $"<b>Photographs</b>   {photos}\n" +
                $"<b>First sighting</b>   {JournalService.FormatTimestamp(record.firstSightedUtc)}\n" +
                $"      {Capitalize(record.firstSightingLocation)}, {PhotoAnalyzer.DescribeActivity(record.firstSightingActivity)}\n" +
                $"<b>Best photograph</b>   {(string.IsNullOrEmpty(record.bestQuality) ? "—" : record.bestQuality)}";
            _notesText.text = species.fieldNotes;
            ShowPhoto(best != null ? PhotoStorage.GetThumbnail(best.photoId, best.imagePath) : null);
        }

        // ------------------------------------------------------------------ field notebook

        /// <summary>Found subjects only, grouped by kind (the SubjectGroup order), then by name.</summary>
        private void RebuildNotebook()
        {
            _notesDirty = false;
            _notes.Clear();
            foreach (var n in Game.Journal.Notebook) if (n != null && !string.IsNullOrEmpty(n.subjectId)) _notes.Add(n);
            _notes.Sort((a, b) =>
            {
                int c = GroupOrder(a.group).CompareTo(GroupOrder(b.group));
                if (c != 0) return c;
                c = string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.subjectId, b.subjectId, StringComparison.Ordinal); // a stable order for equal names
            });
            if (_selectedNote >= _notes.Count) _selectedNote = Mathf.Max(0, _notes.Count - 1);
        }

        private static int GroupOrder(string group) =>
            Enum.TryParse(group, out SubjectGroup g) ? (int)g : int.MaxValue;

        private void RefreshNotebook()
        {
            var journal = Game.Journal;
            _headerStats.text = "";
            _footer.text = "W / S / wheel  select        A / D  wildlife        J / Tab / Esc  close";
            int rows = _rowTexts.Count;
            int total = _notes.Count;

            if (total == 0)
            {
                // Nothing found yet: one gentle line, no empty rows, no total.
                for (int slot = 0; slot < rows; slot++) { _rowTexts[slot].text = ""; _rowBackgrounds[slot].color = Color.clear; }
                _moreAbove.enabled = _moreBelow.enabled = false;
                _nameText.text = _sciText.text = _detailText.text = _notesText.text = "";
                _photoFrame.gameObject.SetActive(false);
                _emptyText.text = "Nothing noted yet. Try the macro lens (" + GameInput.Labels.Macro + ") on a flower.";
                _emptyText.enabled = true;
                return;
            }
            _emptyText.enabled = false;
            _photoFrame.gameObject.SetActive(true);

            Window(ref _selectedNote, ref _offsetNote, total, rows);
            for (int slot = 0; slot < rows; slot++)
            {
                int i = _offsetNote + slot;
                if (i >= total) { _rowTexts[slot].text = ""; _rowBackgrounds[slot].color = Color.clear; continue; }
                var n = _notes[i];
                _rowTexts[slot].text = $"{FitName(n.displayName, "")}\n<size=18>{n.group}  \u00b7  {(string.IsNullOrEmpty(n.bestQuality) ? "noted" : n.bestQuality)}</size>";
                _rowBackgrounds[slot].color = i == _selectedNote ? UIFactory.PaperDark : new Color(0f, 0f, 0f, 0f);
            }
            _moreAbove.enabled = _offsetNote > 0;
            _moreBelow.enabled = _offsetNote + rows < total;

            var rec = _notes[_selectedNote];
            var subject = FindSubject(rec.subjectId); // null when the scene holds none of this kind and it is not a shipped default
            _nameText.text = rec.displayName;
            _sciText.text = subject != null ? subject.scientificName : "";
            var best = journal.GetPhoto(rec.bestPhotoId);
            string where = subject != null && !string.IsNullOrEmpty(subject.habitat) ? $"\n<b>Where</b>   {Capitalize(subject.habitat)}" : "";
            _detailText.text =
                $"<b>Kind</b>   {rec.group}\n" +
                $"<b>Photographs</b>   {rec.photoCount}\n" +
                $"<b>First found</b>   {JournalService.FormatTimestamp(rec.firstFoundUtc)}\n" +
                $"<b>Best photograph</b>   {(string.IsNullOrEmpty(rec.bestQuality) ? "\u2014" : rec.bestQuality)}" + where;
            _notesText.text = subject != null ? subject.fieldNotes : "";
            ShowPhoto(best != null ? PhotoStorage.GetThumbnail(best.photoId, best.imagePath) : null);
        }

        /// <summary>
        /// The small subject's text (scientific name, field notes, habitat). Subject assets are editor-only, so this reads the
        /// ones placed in the scene (MacroSubject.Active), then WildLens's shipped defaults; null if neither knows the id.
        /// </summary>
        private NaturalistSubject FindSubject(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_sceneSubjects == null)
            {
                _sceneSubjects = new Dictionary<string, NaturalistSubject>();
                var active = MacroSubject.Active;
                for (int i = 0; i < active.Count; i++)
                    if (active[i] != null && active[i].subject != null && !string.IsNullOrEmpty(active[i].subject.subjectId))
                        _sceneSubjects[active[i].subject.subjectId] = active[i].subject;
            }
            if (_sceneSubjects.TryGetValue(id, out var found)) return found;
            if (_defaultSubjects == null)
            {
                _defaultSubjects = new Dictionary<string, NaturalistSubject>();
                foreach (var d in SubjectDefaults.CreateAll()) if (d != null) _defaultSubjects[d.subjectId] = d;
            }
            _defaultSubjects.TryGetValue(id, out found);
            return found;
        }

        /// <summary>
        /// A row's name on one line: bold at the normal size, or smaller (down to 16 px) when the longest names ("Eastern Red-backed
        /// Salamander") would not fit the 420 px row. Each row is then name + one small line, never three lines.
        /// </summary>
        private string FitName(string name, string prefix)
        {
            string shown = string.IsNullOrEmpty(name) ? "" : name;
            int size = RowNameSize;
            if (_measure != null)
            {
                _measure.text = prefix + "<b>" + shown + "</b>";
                float w = _measure.preferredWidth;
                if (w > RowTextWidth) size = Mathf.Clamp(Mathf.FloorToInt(RowNameSize * RowTextWidth / w), RowNameMinSize, RowNameSize);
            }
            return size < RowNameSize ? "<size=" + size + "><b>" + shown + "</b></size>" : "<b>" + shown + "</b>";
        }

        /// <summary>Keeps the selection inside a window of <paramref name="rows"/> rows.</summary>
        private static void Window(ref int selected, ref int offset, int total, int rows)
        {
            if (selected >= total) selected = Mathf.Max(0, total - 1);
            if (selected < offset) offset = selected;
            if (selected >= offset + rows) offset = selected - rows + 1;
            offset = Mathf.Clamp(offset, 0, Mathf.Max(0, total - rows));
        }

        private void UpdateTabs()
        {
            var ink = UIFactory.Ink;
            _tabWildlife.color = _page == 0 ? ink : new Color(ink.r, ink.g, ink.b, 0.45f);
            _tabNotebook.color = _page == 1 ? ink : new Color(ink.r, ink.g, ink.b, 0.45f);
            float right = _page == 1 ? 50f : 50f + _widthNotebook + 44f;
            float width = _page == 1 ? _widthNotebook : _widthWildlife;
            UIFactory.Place(_tabLine, new Vector2(1f, 1f), new Vector2(-right, -78f), new Vector2(width, 3f));
        }

        private void ShowPhoto(Texture2D tex)
        {
            _photo.texture = tex;
            _photo.enabled = tex != null;
            _noPhotoText.enabled = tex == null;
            if (tex != null) _photoFitter.aspectRatio = tex.width / (float)tex.height;
        }

        /// <summary>Field tip for an undiscovered species: where to look (hotspots first) and how to approach.</summary>
        private static string HabitatHint(SpeciesDefinition s)
        {
            string where;
            var h = s.preferredHotspots;
            if ((h & HotspotType.Meadow) != 0 && (h & HotspotType.BrushPile) != 0) where = "Check brush piles and the edges of open meadows.";
            else if ((h & HotspotType.Meadow) != 0) where = "Scan open flower meadows, especially from cover at their edges.";
            else if ((h & HotspotType.BrushPile) != 0) where = "Watch around brush piles near the grass.";
            else if ((h & HotspotType.MatureTree) != 0) where = "Look around big old trees, and up their trunks.";
            else if ((h & HotspotType.Perch) != 0) where = "Check dead snags and boulders overlooking open ground, or look up.";
            else
            {
                switch (s.habitat)
                {
                    case HabitatPreference.OpenMeadow: where = "Scan open meadows."; break;
                    case HabitatPreference.ForestEdge: where = "Watch where the woods meet the grassland."; break;
                    case HabitatPreference.Trees: where = "Look around the base of trees."; break;
                    default: where = "Look up over open ground."; break;
                }
            }
            return "Field tip: " + where + " Crouch to approach, and hold it in your viewfinder to identify it.";
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? "Location unknown" : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------ build

        private void Build(int speciesCount)
        {
            _canvas = UIFactory.CreateCanvas("Journal Canvas", 10, transform);
            var root = (RectTransform)_canvas.transform;

            var dim = UIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            var book = UIFactory.CreateImage("Book", root, UIFactory.Paper);
            UIFactory.Place(book.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 880f));
            var b = book.rectTransform;

            var spine = UIFactory.CreateImage("Spine", b, UIFactory.Accent);
            spine.rectTransform.anchorMin = new Vector2(0f, 0f);
            spine.rectTransform.anchorMax = new Vector2(0f, 1f);
            spine.rectTransform.pivot = new Vector2(0f, 0.5f);
            spine.rectTransform.sizeDelta = new Vector2(14f, 0f);

            var title = UIFactory.CreateText("Title", b, "Field Journal  <size=24><i>" + reserveName + "</i></size>", 44,
                UIFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -36f), new Vector2(900f, 60f));

            // Page tabs (top right) with a thin line under the current one, and the page's figures beneath them.
            _tabNotebook = UIFactory.CreateText("Tab Notebook", b, "Field notebook", 28, UIFactory.Ink, TextAnchor.UpperRight);
            _tabWildlife = UIFactory.CreateText("Tab Wildlife", b, "Wildlife", 28, UIFactory.Ink, TextAnchor.UpperRight);
            foreach (var t in new[] { _tabNotebook, _tabWildlife }) t.horizontalOverflow = HorizontalWrapMode.Overflow;
            _widthNotebook = _tabNotebook.preferredWidth > 10f ? _tabNotebook.preferredWidth + 4f : 230f;
            _widthWildlife = _tabWildlife.preferredWidth > 10f ? _tabWildlife.preferredWidth + 4f : 120f;
            UIFactory.Place(_tabNotebook.rectTransform, new Vector2(1f, 1f), new Vector2(-50f, -34f), new Vector2(_widthNotebook, 40f));
            UIFactory.Place(_tabWildlife.rectTransform, new Vector2(1f, 1f), new Vector2(-(50f + _widthNotebook + 44f), -34f), new Vector2(_widthWildlife, 40f));
            _tabLine = UIFactory.CreateImage("Tab Line", b, UIFactory.Accent).rectTransform;

            _headerStats = UIFactory.CreateText("Stats", b, "", 20, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.8f), TextAnchor.UpperRight);
            UIFactory.Place(_headerStats.rectTransform, new Vector2(1f, 1f), new Vector2(-50f, -84f), new Vector2(700f, 26f));

            var rule = UIFactory.CreateImage("Rule", b, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.4f));
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1460f, 2f));

            // Species list
            var faint = new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.6f);
            _moreAbove = UIFactory.CreateText("More Above", b, "^  more", 18, faint, TextAnchor.MiddleCenter);
            UIFactory.Place(_moreAbove.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -112f), new Vector2(420f, 22f));
            _moreBelow = UIFactory.CreateText("More Below", b, "v  more", 18, faint, TextAnchor.MiddleCenter);
            UIFactory.Place(_moreBelow.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -776f), new Vector2(420f, 24f));
            _moreAbove.enabled = _moreBelow.enabled = false;

            for (int i = 0; i < VisibleRows; i++)
            {
                var row = UIFactory.CreateImage("Row " + i, b, Color.clear);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -136f - i * 92f), new Vector2(420f, 84f));
                var text = UIFactory.CreateText("Label", row.transform, "", 26, UIFactory.Ink, TextAnchor.MiddleLeft);
                UIFactory.Stretch(text.rectTransform, 16f, 4f, 8f, 4f);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;   // a name line never wraps onto a third line
                _rowBackgrounds.Add(row);
                _rowTexts.Add(text);
            }

            _measure = UIFactory.CreateText("Measure", b, "", RowNameSize, UIFactory.Ink, TextAnchor.UpperLeft);
            _measure.horizontalOverflow = HorizontalWrapMode.Overflow;
            _measure.enabled = false;

            var divider = UIFactory.CreateImage("Divider", b, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.25f));
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(500f, -130f), new Vector2(2f, 680f));

            // Detail page
            _nameText = UIFactory.CreateText("Name", b, "", 40, UIFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(540f, -130f), new Vector2(940f, 52f));
            _sciText = UIFactory.CreateText("Scientific", b, "", 24, UIFactory.Accent, TextAnchor.UpperLeft, FontStyle.Italic);
            UIFactory.Place(_sciText.rectTransform, new Vector2(0f, 1f), new Vector2(542f, -182f), new Vector2(940f, 34f));

            var frame = UIFactory.CreateImage("Photo Frame", b, UIFactory.PaperDark);
            _photoFrame = frame;
            UIFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(540f, -230f), new Vector2(560f, 330f));
            _photo = UIFactory.CreateRawImage("Photo", frame.transform);
            UIFactory.Stretch(_photo.rectTransform, 10f, 10f, 10f, 10f);
            _photoFitter = _photo.gameObject.AddComponent<AspectRatioFitter>();
            _photoFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _photoFitter.aspectRatio = 16f / 9f;
            _noPhotoText = UIFactory.CreateText("No Photo", frame.transform, "No photograph yet", 24,
                new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.5f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.Stretch(_noPhotoText.rectTransform);

            _detailText = UIFactory.CreateText("Details", b, "", 23, UIFactory.Ink);
            UIFactory.Place(_detailText.rectTransform, new Vector2(0f, 1f), new Vector2(1124f, -236f), new Vector2(400f, 330f));
            _detailText.lineSpacing = 1.25f;

            _notesText = UIFactory.CreateText("Notes", b, "", 24, UIFactory.Ink);
            UIFactory.Place(_notesText.rectTransform, new Vector2(0f, 1f), new Vector2(540f, -590f), new Vector2(970f, 180f));
            _notesText.lineSpacing = 1.2f;

            _emptyText = UIFactory.CreateText("Empty", b, "", 28, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.6f), TextAnchor.UpperLeft, FontStyle.Italic);
            UIFactory.Place(_emptyText.rectTransform, new Vector2(0f, 1f), new Vector2(540f, -140f), new Vector2(940f, 120f));
            _emptyText.enabled = false;

            _footer = UIFactory.CreateText("Footer", b, "", 20,
                new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.7f), TextAnchor.LowerCenter);
            UIFactory.Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1300f, 30f));
        }
    }
}
