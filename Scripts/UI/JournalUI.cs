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
    /// Field journal screen (J / Tab). Lists every species in the reserve: undiscovered ones show as "???"
    /// with a habitat hint; discovered ones show photo count, first sighting, best shot and field notes.
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
        private bool _dirty = true;

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

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            bool open = _canvas.gameObject.activeSelf;
            if (!open)
            {
                if (GameInput.JournalTogglePressed && !Game.IsUiOpen && Game.UiClosedFrame != Time.frameCount) SetOpen(true);
                return;
            }

            if (GameInput.JournalTogglePressed || GameInput.CancelPressed) { SetOpen(false); return; }

            int count = Game.Species.Count;
            if (count > 0)
            {
                if (GameInput.NavigateUpPressed) { _selected = (_selected - 1 + count) % count; _dirty = true; }
                if (GameInput.NavigateDownPressed) { _selected = (_selected + 1) % count; _dirty = true; }
            }
            if (_dirty) Refresh();
        }

        public void SetOpen(bool open)
        {
            _canvas.gameObject.SetActive(open);
            Game.SetUiOpen(open);
            if (open) Refresh();
        }

        // ------------------------------------------------------------------ content

        private void Refresh()
        {
            _dirty = false;
            var journal = Game.Journal;
            var db = Game.Species;
            _headerStats.text = $"Species {journal.DiscoveredCount} of {journal.TotalSpecies}     Photographs {journal.TotalPhotos}";

            for (int i = 0; i < _rowTexts.Count && i < db.Count; i++)
            {
                var s = db.species[i];
                var rec = s != null ? journal.GetRecord(s.speciesId) : null;
                string num = (i + 1).ToString("00");
                _rowTexts[i].text = rec != null
                    ? $"{num}   <b>{s.displayName}</b>\n<size=18>     {rec.photoCount} photo{(rec.photoCount == 1 ? "" : "s")}</size>"
                    : $"{num}   ???\n<size=18>     not yet observed</size>";
                _rowBackgrounds[i].color = i == _selected ? UIFactory.PaperDark : new Color(0f, 0f, 0f, 0f);
            }

            var species = _selected < db.Count ? db.species[_selected] : null;
            if (species == null) return;
            var record = journal.GetRecord(species.speciesId);

            if (record == null)
            {
                _nameText.text = "Unknown species";
                _sciText.text = "";
                _detailText.text = "<b>Photographs</b>   0\n<b>First sighting</b>   —";
                _notesText.text = "<i>Not yet observed.</i>  " + HabitatHint(species.habitat);
                ShowPhoto(null);
                return;
            }

            _nameText.text = species.displayName;
            _sciText.text = species.scientificName;
            var best = journal.GetPhoto(record.bestPhotoId);
            _detailText.text =
                $"<b>Photographs</b>   {record.photoCount}\n" +
                $"<b>First sighting</b>   {JournalService.FormatTimestamp(record.firstSightedUtc)}\n" +
                $"      {Capitalize(record.firstSightingLocation)}, {PhotoAnalyzer.DescribeActivity(record.firstSightingActivity)}\n" +
                $"<b>Best photograph</b>   {(string.IsNullOrEmpty(record.bestQuality) ? "—" : record.bestQuality)}";
            _notesText.text = species.fieldNotes;
            ShowPhoto(best != null ? PhotoStorage.GetThumbnail(best.photoId, best.imagePath) : null);
        }

        private void ShowPhoto(Texture2D tex)
        {
            _photo.texture = tex;
            _photo.enabled = tex != null;
            _noPhotoText.enabled = tex == null;
            if (tex != null) _photoFitter.aspectRatio = tex.width / (float)tex.height;
        }

        private static string HabitatHint(HabitatPreference h)
        {
            switch (h)
            {
                case HabitatPreference.OpenMeadow: return "Field tip: scan open meadows, and move slowly.";
                case HabitatPreference.ForestEdge: return "Field tip: watch where the woods meet the grassland.";
                case HabitatPreference.Trees: return "Field tip: look around the base of trees — and up the trunks.";
                case HabitatPreference.Sky: return "Field tip: look up over open ground.";
                default: return "";
            }
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

            _headerStats = UIFactory.CreateText("Stats", b, "", 24, UIFactory.Ink, TextAnchor.UpperRight);
            UIFactory.Place(_headerStats.rectTransform, new Vector2(1f, 1f), new Vector2(-50f, -48f), new Vector2(600f, 40f));

            var rule = UIFactory.CreateImage("Rule", b, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.4f));
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1460f, 2f));

            // Species list
            for (int i = 0; i < speciesCount; i++)
            {
                var row = UIFactory.CreateImage("Row " + i, b, Color.clear);
                UIFactory.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -136f - i * 92f), new Vector2(420f, 84f));
                var text = UIFactory.CreateText("Label", row.transform, "", 26, UIFactory.Ink, TextAnchor.MiddleLeft);
                UIFactory.Stretch(text.rectTransform, 16f, 4f, 8f, 4f);
                _rowBackgrounds.Add(row);
                _rowTexts.Add(text);
            }

            var divider = UIFactory.CreateImage("Divider", b, new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.25f));
            UIFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(500f, -130f), new Vector2(2f, 680f));

            // Detail page
            _nameText = UIFactory.CreateText("Name", b, "", 40, UIFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(540f, -130f), new Vector2(940f, 52f));
            _sciText = UIFactory.CreateText("Scientific", b, "", 24, UIFactory.Accent, TextAnchor.UpperLeft, FontStyle.Italic);
            UIFactory.Place(_sciText.rectTransform, new Vector2(0f, 1f), new Vector2(542f, -182f), new Vector2(940f, 34f));

            var frame = UIFactory.CreateImage("Photo Frame", b, UIFactory.PaperDark);
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

            var footer = UIFactory.CreateText("Footer", b, "W / S  select species        J / Tab / Esc  close", 20,
                new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.7f), TextAnchor.LowerCenter);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(900f, 30f));
        }
    }
}
