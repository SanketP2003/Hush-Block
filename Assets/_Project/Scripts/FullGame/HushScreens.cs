using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HushBlocks.FullGame
{
    internal static class HushLayout
    {
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static TextMeshProUGUI Text(
            Transform parent,
            string name,
            string value,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            bool heading,
            Vector2 anchor,
            Vector2 position,
            Vector2 box,
            bool wrap = false,
            float spacing = 0f)
        {
            var text = HushUI.Text(parent, name, value, size, color, alignment, heading, wrap);
            text.characterSpacing = spacing;
            float pivotX = alignment == TextAlignmentOptions.Center || alignment == TextAlignmentOptions.Top || alignment == TextAlignmentOptions.Bottom
                ? 0.5f
                : alignment == TextAlignmentOptions.Right || alignment == TextAlignmentOptions.TopRight || alignment == TextAlignmentOptions.BottomRight
                    ? 1f
                    : 0f;
            Place(text.rectTransform, anchor, new Vector2(pivotX, 0.5f), position, box);
            return text;
        }

        public static Image Image(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var image = HushUI.Image(parent, name, sprite, color);
            Place(image.rectTransform, anchor, pivot, position, size);
            return image;
        }

        public static Image Fullscreen(Transform parent, string name, Color color)
        {
            var image = HushUI.Image(parent, name, null, color);
            HushUI.Fill(image.rectTransform);
            return image;
        }
    }

    // ====================================================================== BOOT

    public sealed class BootScreen : HushScreen
    {
        private Image fill;
        private TextMeshProUGUI percent;
        private TextMeshProUGUI subtitle;
        private float progress;

        public BootScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Background", HushPalette.Ink);
            var glow = HushLayout.Image(Root, "Glow", HushArt.Glow, HushPalette.WithAlpha(HushPalette.Cyan, 0.12f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(900f, 500f));

            HushLayout.Text(Root, "Logo", "HUSH BLOCKS", 82f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1100f, 140f), false, 8f);
            subtitle = HushLayout.Text(Root, "Status", "TUNING THE CITY", 17f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(700f, 40f), false, 5f);

            var track = HushLayout.Image(Root, "Track", HushArt.Rounded, HushPalette.Border,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(520f, 5f));
            track.type = Image.Type.Sliced;
            fill = HushLayout.Image(track.transform, "Fill", HushArt.Rounded, HushPalette.Amber,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 5f));
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            percent = HushLayout.Text(Root, "Percent", "0%", 14f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -84f), new Vector2(300f, 30f), false, 3f);
        }

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            if (fill == null) return;
            fill.rectTransform.anchorMax = new Vector2(progress, 1f);
            if (percent != null) percent.text = Mathf.RoundToInt(progress * 100f) + "%";
        }

        public override void Tick()
        {
            if (subtitle != null)
            {
                int dots = Mathf.FloorToInt(Time.unscaledTime * 2.2f) % 4;
                subtitle.text = "TUNING THE CITY" + new string('.', dots);
            }
        }
    }

    // ====================================================================== TITLE

    public sealed class TitleScreen : HushScreen
    {
        private TextMeshProUGUI stats;
        private TextMeshProUGUI version;
        private HushButton play;
        private HushButton districts;
        private HushButton howTo;
        private HushButton settings;
        private HushButton quit;
        private Image[] bars;
        private RectTransform[] barRects;
        private RectTransform contentRect;
        private RectTransform scrimRect;

        public TitleScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            var scrim = HushLayout.Image(Root, "Scrim", HushArt.Scrim, Color.white,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(1320f, 1080f));
            scrim.rectTransform.anchorMin = new Vector2(0f, 0f);
            scrim.rectTransform.anchorMax = new Vector2(0f, 1f);
            scrim.rectTransform.offsetMin = new Vector2(0f, 0f);
            scrim.rectTransform.offsetMax = new Vector2(1320f, 0f);
            scrimRect = scrim.rectTransform;

            contentRect = HushLayout.Place(HushUI.NewRect(Root, "Title Content"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(138f, 0f), new Vector2(780f, 940f));

            HushLayout.Image(contentRect, "Logo Glow", HushArt.Glow, HushPalette.WithAlpha(HushPalette.Cyan, 0.10f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 280f), new Vector2(620f, 300f));

            HushLayout.Text(contentRect, "Wordmark A", "HUSH", 140f, HushPalette.Text, TextAlignmentOptions.Left, true,
                new Vector2(0f, 1f), new Vector2(-4f, -70f), new Vector2(700f, 150f), false, 12f);
            HushLayout.Text(contentRect, "Wordmark B", "BLOCKS", 140f, HushPalette.Amber, TextAlignmentOptions.Left, true,
                new Vector2(0f, 1f), new Vector2(-4f, -190f), new Vector2(700f, 150f), false, 12f);

            var line = HushLayout.Image(contentRect, "Line", HushArt.RoundedSmall, HushPalette.Cyan,
                new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -270f), new Vector2(82f, 3f));
            line.type = Image.Type.Sliced;
            HushLayout.Text(contentRect, "Tagline", "A RHYTHM STACKING NOCTURNE", 16f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(0f, -298f), new Vector2(620f, 30f), false, 5f);

            // Beat equalizer - procedural, driven by the menu loop.
            bars = new Image[9];
            barRects = new RectTransform[bars.Length];
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = HushLayout.Image(contentRect, "Eq " + i, HushArt.RoundedSmall,
                    i % 3 == 0 ? HushPalette.Amber : HushPalette.Cyan,
                    new Vector2(0f, 1f), new Vector2(0f, 0f),
                    new Vector2(i * 14f, -345f), new Vector2(7f, 18f));
                bar.type = Image.Type.Sliced;
                bar.color = new Color(bar.color.r, bar.color.g, bar.color.b, 0.7f);
                bars[i] = bar;
                barRects[i] = bar.rectTransform;
            }

            play = MakeButton(contentRect, "PLAY", new Vector2(0f, -415f), new Vector2(368f, 68f), HushButton.Style.Primary, 26f);
            play.Clicked = () => Game.Play();

            districts = MakeButton(contentRect, "DISTRICTS", new Vector2(0f, -485f), new Vector2(368f, 58f), HushButton.Style.Secondary, 21f);
            districts.Clicked = () => Game.OpenDistrictSelect();

            howTo = MakeButton(contentRect, "HOW TO PLAY", new Vector2(0f, -550f), new Vector2(368f, 58f), HushButton.Style.Secondary, 21f);
            howTo.Clicked = () => Game.OpenHowTo(HushReturnTarget.Title);

            settings = MakeButton(contentRect, "SETTINGS", new Vector2(0f, -615f), new Vector2(368f, 54f), HushButton.Style.Ghost, 19f);
            settings.Clicked = () => Game.OpenSettings(HushReturnTarget.Title);

            quit = MakeButton(contentRect, "QUIT", new Vector2(0f, -680f), new Vector2(200f, 48f), HushButton.Style.Ghost, 17f);
            quit.Clicked = () => Game.QuitGame();

            stats = HushLayout.Text(contentRect, "Stats", string.Empty, 15f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 0f), new Vector2(0f, 40f), new Vector2(700f, 32f), false, 2f);

            version = HushLayout.Text(Root, "Version", "v1.0.0  ·  PROCEDURAL EDITION", 13f, HushPalette.WithAlpha(HushPalette.Muted, 0.7f),
                TextAlignmentOptions.Right, false, new Vector2(1f, 0f), new Vector2(-40f, 26f), new Vector2(700f, 30f), false, 2f);
        }

        private HushButton MakeButton(Transform parent, string label, Vector2 position, Vector2 size, HushButton.Style style, float fontSize = 23f)
        {
            var button = HushUI.Button(parent, label, size, style, fontSize);
            HushLayout.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), position, size);
            return button;
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            Refresh();
            if (play != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(play.gameObject);
            }
        }

        private void Refresh()
        {
            var save = HushProgress.Data;
            bool hasProgress = save.runs > 0 || HushProgress.TotalStars > 0;
            play.SetLabel(hasProgress ? "CONTINUE" : "PLAY");

            int totalBest = 0;
            for (int i = 0; i < save.bestScores.Length; i++) totalBest += save.bestScores[i];

            stats.text =
                $"TOTAL BEST {totalBest:N0}   ·   STARS {HushProgress.TotalStars} / {DistrictCatalog.Count * 3}   ·   DISTRICTS {HushProgress.ClearedDistricts} / {DistrictCatalog.Count}";
        }

        public override void Tick()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < bars.Length; i++)
            {
                float phase = t * (1.4f + i * 0.07f) + i * 1.7f;
                float height = 10f + Mathf.Abs(Mathf.Sin(phase)) * (i % 3 == 0 ? 44f : 30f);
                height *= HushProgress.Data.reducedMotion ? 0.35f : 1f;
                barRects[i].sizeDelta = new Vector2(7f, height);
                var color = bars[i].color;
                color.a = 0.45f + Mathf.Abs(Mathf.Sin(phase)) * 0.45f;
                bars[i].color = color;
            }

            // Responsive positioning for portrait and narrow screens
            if (contentRect != null)
            {
                bool isPortrait = (float)Screen.width / Mathf.Max(1, Screen.height) < 1.25f;
                if (isPortrait)
                {
                    contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                    contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                    contentRect.pivot = new Vector2(0.5f, 0.5f);
                    contentRect.anchoredPosition = Vector2.zero;
                    float scale = Mathf.Min(1f, (Screen.width - 40f) / 780f);
                    contentRect.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    contentRect.anchorMin = new Vector2(0f, 0.5f);
                    contentRect.anchorMax = new Vector2(0f, 0.5f);
                    contentRect.pivot = new Vector2(0f, 0.5f);
                    contentRect.anchoredPosition = new Vector2(138f, 0f);
                    contentRect.localScale = Vector3.one;
                }
            }
        }
    }

    // ====================================================================== DISTRICT INTRO

    public sealed class DistrictIntroScreen : HushScreen
    {
        private TextMeshProUGUI number;
        private TextMeshProUGUI name;
        private TextMeshProUGUI tagline;
        private TextMeshProUGUI stats;
        private TextMeshProUGUI tip;
        private Image accent;
        private Image glow;
        private HushButton begin;

        public DistrictIntroScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            var dim = HushLayout.Fullscreen(Root, "Dim", new Color(0.008f, 0.012f, 0.03f, 0.96f));
            dim.raycastTarget = false;

            glow = HushLayout.Image(Root, "Glow", HushArt.Glow, HushPalette.WithAlpha(HushPalette.Cyan, 0.12f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1300f, 700f));

            number = HushLayout.Text(Root, "Number", "DISTRICT 01", 15f, HushPalette.Cyan, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 252f), new Vector2(900f, 30f), false, 7f);

            name = HushLayout.Text(Root, "Name", "WHISPER ROW", 92f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 148f), new Vector2(1300f, 150f), false, 4f);

            tagline = HushLayout.Text(Root, "Tagline", "THE CITY HOLDS ITS BREATH", 16f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 54f), new Vector2(1000f, 30f), false, 5f);

            accent = HushLayout.Image(Root, "Accent", HushArt.RoundedSmall, HushPalette.Cyan,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(120f, 3f));
            accent.type = Image.Type.Sliced;

            stats = HushLayout.Text(Root, "Stats", "96 BPM   ·   10 LAYERS   ·   THREE HEARTS", 18f, HushPalette.Text,
                TextAlignmentOptions.Center, false, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(900f, 32f), false, 2f);

            tip = HushLayout.Text(Root, "Tip", string.Empty, 17f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(900f, 66f), true, 0f);

            begin = HushUI.Button(Root, "BEGIN", new Vector2(280f, 64f), HushButton.Style.Primary, 24f);
            HushLayout.Place((RectTransform)begin.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -176f), new Vector2(280f, 64f));
            begin.Clicked = () => Game.SkipDistrictIntro();

            HushLayout.Text(Root, "Prompt", "TAP / SPACE TO BEGIN   ·   AUTO IN 2.5s", 12f, HushPalette.WithAlpha(HushPalette.Muted, 0.75f),
                TextAlignmentOptions.Center, false, new Vector2(0.5f, 0.5f), new Vector2(0f, -242f), new Vector2(800f, 26f), false, 3f);
        }

        public void Present(DistrictDefinition definition)
        {
            if (definition == null) return;

            number.text = definition.Number;
            number.color = definition.Accent;
            name.text = definition.Name.ToUpperInvariant();
            tagline.text = definition.Tagline;
            stats.text = $"{definition.Bpm} BPM   ·   {definition.TargetFloors} LAYERS   ·   THREE HEARTS";
            tip.text = definition.Tips != null && definition.Tips.Length > 0 ? definition.Tips[0] : string.Empty;

            accent.color = definition.Accent;
            glow.color = HushPalette.WithAlpha(definition.Accent, 0.12f);
            begin.SetAccent(definition.Accent);
        }
    }

    // ====================================================================== DISTRICT SELECT

    public sealed class DistrictSelectScreen : HushScreen
    {
        private sealed class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Border;
            public TextMeshProUGUI Badge;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Tagline;
            public TextMeshProUGUI Description;
            public TextMeshProUGUI Stats;
            public TextMeshProUGUI Best;
            public Image[] Stars;
            public HushButton Enter;
        }

        private readonly Card[] cards = new Card[DistrictCatalog.Count];
        private TextMeshProUGUI starTotal;
        private TextMeshProUGUI subtitle;
        private HushButton prevButton;
        private HushButton nextButton;
        private Image[] pageDots;
        private int activeCardIndex;
        private Vector2 touchStartPos;
        private bool isSwiping;

        public DistrictSelectScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Dim", new Color(0.012f, 0.02f, 0.05f, 0.86f));

            var back = HushUI.Button(Root, "<", new Vector2(64f, 64f), HushButton.Style.Icon, 28f);
            HushLayout.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -44f), new Vector2(64f, 64f));
            back.Clicked = () => Game.OpenTitle();

            HushLayout.Text(Root, "Header", "CHOOSE YOUR DESCENT", 52f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1200f, 80f), false, 4f);
            subtitle = HushLayout.Text(Root, "Subtitle", "THREE ROOFTOPS. ONE LONG NIGHT.", 15f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(1000f, 32f), false, 4f);
            starTotal = HushLayout.Text(Root, "Star Total", string.Empty, 16f, HushPalette.Amber, TextAlignmentOptions.Right, false,
                new Vector2(1f, 1f), new Vector2(-52f, -62f), new Vector2(400f, 34f), false, 2f);

            for (int i = 0; i < DistrictCatalog.Count; i++)
            {
                cards[i] = BuildCard(i);
            }

            prevButton = HushUI.Button(Root, "<", new Vector2(56f, 56f), HushButton.Style.Secondary, 26f);
            HushLayout.Place((RectTransform)prevButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, -36f), new Vector2(56f, 56f));
            prevButton.Clicked = () => CycleCard(-1);

            nextButton = HushUI.Button(Root, ">", new Vector2(56f, 56f), HushButton.Style.Secondary, 26f);
            HushLayout.Place((RectTransform)nextButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, -36f), new Vector2(56f, 56f));
            nextButton.Clicked = () => CycleCard(1);

            pageDots = new Image[DistrictCatalog.Count];
            for (int i = 0; i < DistrictCatalog.Count; i++)
            {
                pageDots[i] = HushLayout.Image(Root, "Dot " + i, HushArt.Circle, HushPalette.Border,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-24f + i * 24f, 82f), new Vector2(10f, 10f));
            }

            HushLayout.Text(Root, "Footer", "CLEAR A DISTRICT TO UNLOCK THE NEXT ROOFTOP", 13f, HushPalette.WithAlpha(HushPalette.Muted, 0.75f),
                TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1000f, 30f), false, 3f);
        }

        private Card BuildCard(int index)
        {
            var definition = DistrictCatalog.Get(index);
            var card = new Card();
            float x = index == 0 ? -470f : index == 1 ? 0f : 470f;
            var root = HushLayout.Place(HushUI.NewRect(Root, "Card " + index), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -36f), new Vector2(430f, 580f));
            card.Root = root;
            card.Group = root.gameObject.AddComponent<CanvasGroup>();

            var panel = HushUI.Panel(root, "Panel", HushPalette.WithAlpha(HushPalette.Panel, 0.94f), HushPalette.Border, 2f, true, definition.Accent);
            HushUI.Fill(panel);
            card.Border = panel.Find("Border").GetComponent<Image>();

            HushLayout.Text(root, "Number", (index + 1).ToString("00"), 84f, HushPalette.WithAlpha(definition.Accent, 0.22f),
                TextAlignmentOptions.Right, true, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(200f, 100f), false, 2f);

            card.Badge = HushLayout.Text(root, "Badge", string.Empty, 12f, definition.Accent, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(26f, -28f), new Vector2(240f, 24f), false, 5f);

            card.Name = HushLayout.Text(root, "Name", definition.Name, 38f, HushPalette.Text, TextAlignmentOptions.Left, true,
                new Vector2(0f, 1f), new Vector2(26f, -62f), new Vector2(370f, 56f), false, 2f);
            card.Tagline = HushLayout.Text(root, "Tagline", definition.Tagline, 11f, definition.Accent, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(26f, -112f), new Vector2(370f, 26f), false, 4f);
            card.Description = HushLayout.Text(root, "Description", definition.Description, 18f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(26f, -150f), new Vector2(374f, 78f), true, 0f);

            var rule = HushLayout.Image(root, "Rule", HushArt.RoundedSmall, HushPalette.WithAlpha(HushPalette.Border, 0.9f),
                new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(26f, -238f), new Vector2(374f, 2f));
            rule.type = Image.Type.Sliced;

            card.Stats = HushLayout.Text(root, "Stats", $"{definition.Bpm} BPM     ·     {definition.TargetFloors} LAYERS", 15f, HushPalette.Text,
                TextAlignmentOptions.Left, false, new Vector2(0f, 1f), new Vector2(26f, -276f), new Vector2(374f, 32f), false, 2f);

            card.Stars = new Image[3];
            for (int s = 0; s < 3; s++)
            {
                card.Stars[s] = HushLayout.Image(root, "Star " + s, HushArt.Star, HushPalette.Border,
                    new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(26f + s * 42f, -320f), new Vector2(30f, 30f));
            }

            card.Best = HushLayout.Text(root, "Best", string.Empty, 15f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(26f, -366f), new Vector2(374f, 30f), false, 2f);

            card.Enter = HushUI.Button(root, "ENTER DISTRICT", new Vector2(370f, 62f), HushButton.Style.Secondary, 21f);
            HushLayout.Place((RectTransform)card.Enter.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(370f, 62f));
            card.Enter.SetAccent(definition.Accent);
            int captured = index;
            card.Enter.Clicked = () => Game.StartDistrict(captured);

            return card;
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            activeCardIndex = Mathf.Clamp(HushProgress.NextDistrict, 0, DistrictCatalog.Count - 1);
            Refresh();
            if (activeCardIndex >= 0 && activeCardIndex < cards.Length && cards[activeCardIndex].Enter != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(cards[activeCardIndex].Enter.gameObject);
            }
        }

        private void CycleCard(int delta)
        {
            int target = Mathf.Clamp(activeCardIndex + delta, 0, DistrictCatalog.Count - 1);
            if (target != activeCardIndex)
            {
                activeCardIndex = target;
                HushAudio.Sfx(HushAudio.SfxId.UiClick, 0.6f);
                if (EventSystem.current != null && cards[activeCardIndex].Enter != null)
                {
                    EventSystem.current.SetSelectedGameObject(cards[activeCardIndex].Enter.gameObject);
                }
            }
        }

        public override void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            bool isPortrait = (float)Screen.width / Mathf.Max(1, Screen.height) < 1.35f;

            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(isPortrait);
                prevButton.SetInteractable(activeCardIndex > 0);
            }
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(isPortrait);
                nextButton.SetInteractable(activeCardIndex < DistrictCatalog.Count - 1);
            }

            for (int i = 0; i < pageDots.Length; i++)
            {
                if (pageDots[i] != null)
                {
                    pageDots[i].gameObject.SetActive(isPortrait);
                    pageDots[i].color = i == activeCardIndex ? HushPalette.Amber : HushPalette.Border;
                    pageDots[i].rectTransform.localScale = Vector3.one * (i == activeCardIndex ? 1.35f : 1f);
                }
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null || cards[i].Root == null) continue;
                bool unlocked = HushProgress.IsUnlocked(i);

                float targetX = isPortrait ? (i - activeCardIndex) * 460f : (i == 0 ? -470f : i == 1 ? 0f : 470f);
                float curX = cards[i].Root.anchoredPosition.x;
                float newX = Mathf.Lerp(curX, targetX, 1f - Mathf.Exp(-14f * dt));
                cards[i].Root.anchoredPosition = new Vector2(newX, -36f);

                float targetAlpha = isPortrait
                    ? (i == activeCardIndex ? (unlocked ? 1f : 0.42f) : 0.22f)
                    : (unlocked ? 1f : 0.42f);
                cards[i].Group.alpha = Mathf.Lerp(cards[i].Group.alpha, targetAlpha, 1f - Mathf.Exp(-14f * dt));
                cards[i].Group.blocksRaycasts = !isPortrait || i == activeCardIndex;
            }

            if (isPortrait)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) CycleCard(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) CycleCard(1);

                if (Input.GetMouseButtonDown(0))
                {
                    touchStartPos = Input.mousePosition;
                    isSwiping = true;
                }
                else if (Input.GetMouseButtonUp(0) && isSwiping)
                {
                    isSwiping = false;
                    float deltaX = Input.mousePosition.x - touchStartPos.x;
                    if (Mathf.Abs(deltaX) > 60f)
                    {
                        if (deltaX < 0) CycleCard(1);
                        else CycleCard(-1);
                    }
                }
            }
        }

        private void Refresh()
        {
            var save = HushProgress.Data;
            starTotal.text = $"STARS {HushProgress.TotalStars} / {DistrictCatalog.Count * 3}";
            int next = HushProgress.NextDistrict;

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var definition = DistrictCatalog.Get(i);
                bool unlocked = HushProgress.IsUnlocked(i);
                bool isNext = unlocked && i == next && save.stars[i] <= 0;

                card.Group.alpha = unlocked ? 1f : 0.42f;
                card.Badge.text = isNext ? "NEXT ROOFTOP" : unlocked ? (save.stars[i] > 0 ? "CLEARED" : "OPEN") : "LOCKED";
                card.Badge.color = isNext ? definition.Accent : HushPalette.WithAlpha(definition.Accent, 0.7f);
                card.Border.color = isNext ? definition.Accent : HushPalette.Border;
                card.Name.text = definition.Name;
                card.Tagline.text = definition.Tagline;
                card.Description.text = definition.Description;
                card.Stats.text = $"{definition.Bpm} BPM     ·     {definition.TargetFloors} LAYERS";
                card.Best.text = save.bestScores[i] > 0 ? $"BEST  {save.bestScores[i]:N0}" : "NOT YET CLEARED";

                for (int s = 0; s < 3; s++)
                {
                    bool filled = s < save.stars[i];
                    card.Stars[s].color = filled ? HushPalette.Amber : HushPalette.WithAlpha(HushPalette.Border, 0.65f);
                }

                card.Enter.SetInteractable(unlocked);
                card.Enter.SetLabel(unlocked ? "ENTER DISTRICT" : "LOCKED");
                card.Enter.SetAccent(definition.Accent);
            }
        }
    }

    // ====================================================================== HOW TO PLAY

    public sealed class HowToScreen : HushScreen
    {
        public HushReturnTarget ReturnTarget;
        private Image pulseRing;
        private RectTransform pulseRingRect;

        public HowToScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Dim", new Color(0.012f, 0.02f, 0.05f, 0.9f));
            var panel = HushUI.Panel(Root, "Panel", HushPalette.WithAlpha(HushPalette.Panel, 0.97f), HushPalette.Border, 2f, true, HushPalette.Cyan);
            HushLayout.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 720f));

            HushLayout.Text(panel, "Header", "HOW TO PLAY", 48f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(900f, 76f), false, 4f);
            HushLayout.Text(panel, "Sub", "THE RHYTHM IS THE FOUNDATION", 13f, HushPalette.Cyan, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(900f, 28f), false, 4f);

            BuildStep(panel, 0, "01", "LISTEN", "The ring pulses on every beat. The click track is your clock.", new Vector2(-380f, -6f), StepIcon.Ring);
            BuildStep(panel, 1, "02", "TAP", "Click, tap or press Space to drop the hovering block.", new Vector2(0f, -6f), StepIcon.Tap);
            BuildStep(panel, 2, "03", "STACK", "Center the block to keep its width. Perfect timing turns it amber and adds score.", new Vector2(380f, -6f), StepIcon.Stack);

            HushLayout.Text(panel, "Rule", "AMBER IS AT REST  ·  CYAN IS NOISY  ·  THREE MISSES END THE RUN", 13f,
                HushPalette.Amber, TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(1100f, 30f), false, 3f);

            var gotIt = HushUI.Button(panel, "GOT IT", new Vector2(300f, 64f), HushButton.Style.Primary, 23f);
            HushLayout.Place((RectTransform)gotIt.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(300f, 64f));
            gotIt.Clicked = () => Game.CloseHowTo();
        }

        private enum StepIcon { Ring, Tap, Stack }

        private void BuildStep(Transform panel, int index, string number, string title, string body, Vector2 position, StepIcon icon)
        {
            var column = HushLayout.Place(HushUI.NewRect(panel, "Step " + index), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(350f, 380f));

            HushLayout.Text(column, "Number", number, 58f, HushPalette.WithAlpha(HushPalette.Cyan, 0.18f), TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(300f, 70f), false, 6f);

            switch (icon)
            {
                case StepIcon.Ring:
                    pulseRing = HushLayout.Image(column, "Ring", HushArt.Ring, HushPalette.Cyan,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(88f, 88f));
                    pulseRingRect = pulseRing.rectTransform;
                    HushLayout.Image(column, "Dot", HushArt.Circle, HushPalette.Amber,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(22f, 22f));
                    break;
                case StepIcon.Tap:
                    var block = HushLayout.Image(column, "Block", HushArt.RoundedSmall, HushPalette.Cyan,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(120f, 62f));
                    block.type = Image.Type.Sliced;
                    HushLayout.Image(column, "Guide", HushArt.RoundedSmall, HushPalette.Amber,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -146f), new Vector2(126f, 6f));
                    break;
                case StepIcon.Stack:
                    for (int i = 0; i < 3; i++)
                    {
                        var layer = HushLayout.Image(column, "Layer " + i, HushArt.RoundedSmall,
                            i == 1 ? HushPalette.Cyan : HushPalette.Amber,
                            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -150f + i * 28f), new Vector2(140f - i * 22f, 24f));
                        layer.type = Image.Type.Sliced;
                    }
                    break;
            }

            HushLayout.Text(column, "Title", title, 28f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(320f, 36f), false, 3f);
            var bodyText = HushLayout.Text(column, "Body", body, 17f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(310f, 110f), true, 0f);
            bodyText.lineSpacing = 6f;
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            if (EventSystem.current != null)
            {
                var gotIt = Root.GetComponentInChildren<HushButton>();
                if (gotIt != null) EventSystem.current.SetSelectedGameObject(gotIt.gameObject);
            }
        }

        public override void Tick()
        {
            if (pulseRingRect == null) return;
            float pulse = Mathf.Repeat(Time.unscaledTime / 1.25f, 1f);
            float scale = 0.86f + pulse * 0.2f;
            pulseRingRect.localScale = new Vector3(scale, scale, 1f);
            var color = pulseRing.color;
            color.a = 0.35f + (1f - pulse) * 0.65f;
            pulseRing.color = color;
        }
    }

    // ====================================================================== SETTINGS

    public sealed class SettingsScreen : HushScreen
    {
        public HushReturnTarget ReturnTarget;

        private TextMeshProUGUI masterValue;
        private TextMeshProUGUI musicValue;
        private TextMeshProUGUI sfxValue;
        private HushToggle clickToggle;
        private HushToggle shakeToggle;
        private HushToggle motionToggle;
        private HushButton resetButton;
        private HushButton backButton;
        private TextMeshProUGUI statusLine;
        private float resetArmedUntil;
        private float statusUntil;

        public SettingsScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Dim", new Color(0.012f, 0.02f, 0.05f, 0.9f));
            var panel = HushUI.Panel(Root, "Panel", HushPalette.WithAlpha(HushPalette.Panel, 0.97f), HushPalette.Border, 2f, true, HushPalette.Amber);
            HushLayout.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 820f));

            HushLayout.Text(panel, "Header", "SETTINGS", 48f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(700f, 50f), false, 4f);
            HushLayout.Text(panel, "Sub", "TUNE THE ROOM, NOT THE RHYTHM", 13f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(700f, 24f), false, 4f);

            BuildRule(panel, -112f);

            masterValue = BuildVolumeRow(panel, "MASTER", -148f, AdjustMaster);
            musicValue = BuildVolumeRow(panel, "MUSIC", -202f, AdjustMusic);
            sfxValue = BuildVolumeRow(panel, "SOUND EFFECTS", -256f, AdjustSfx);

            BuildRule(panel, -295f);

            clickToggle = HushUI.Toggle(panel, "BEAT CLICKS", HushProgress.Data.beatClicks, value =>
            {
                HushProgress.Data.beatClicks = value;
                HushProgress.NotifyChanged();
                HushAudio.ApplyVolumes();
            });
            HushLayout.Place(clickToggle.RowRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -336f), new Vector2(660f, 48f));

            shakeToggle = HushUI.Toggle(panel, "SCREEN SHAKE", HushProgress.Data.screenShake, value =>
            {
                HushProgress.Data.screenShake = value;
                HushProgress.NotifyChanged();
            });
            HushLayout.Place(shakeToggle.RowRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -394f), new Vector2(660f, 48f));

            motionToggle = HushUI.Toggle(panel, "REDUCED MOTION", HushProgress.Data.reducedMotion, value =>
            {
                HushProgress.Data.reducedMotion = value;
                HushProgress.NotifyChanged();
            });
            HushLayout.Place(motionToggle.RowRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -452f), new Vector2(660f, 48f));

            BuildRule(panel, -495f);

            resetButton = HushUI.Button(panel, "RESET PROGRESS", new Vector2(320f, 52f), HushButton.Style.Danger, 19f);
            HushLayout.Place((RectTransform)resetButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -545f), new Vector2(320f, 52f));
            resetButton.Clicked = OnResetPressed;

            statusLine = HushLayout.Text(panel, "Status", string.Empty, 14f, HushPalette.Amber, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -605f), new Vector2(760f, 28f), false, 2f);

            backButton = HushUI.Button(panel, "BACK", new Vector2(240f, 54f), HushButton.Style.Secondary, 21f);
            HushLayout.Place((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(240f, 54f));
            backButton.Clicked = () => Game.CloseSettings();
        }

        private TextMeshProUGUI BuildVolumeRow(Transform panel, string label, float y, Action<float> adjust)
        {
            var row = HushLayout.Place(HushUI.NewRect(panel, "Row " + label), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(660f, 48f));

            HushLayout.Text(row, "Label", label, 20f, HushPalette.Text, TextAlignmentOptions.Left, false,
                new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(360f, 48f), false, 2f);

            var minus = HushUI.Button(row, "-", new Vector2(48f, 48f), HushButton.Style.Icon, 24f);
            HushLayout.Place((RectTransform)minus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(48f, 48f));
            minus.Clicked = () => adjust(-0.05f);

            var value = HushLayout.Text(row, "Value", "0%", 20f, HushPalette.Amber, TextAlignmentOptions.Center, false,
                new Vector2(1f, 0.5f), new Vector2(-94f, 0f), new Vector2(86f, 40f), false, 0f);

            var plus = HushUI.Button(row, "+", new Vector2(48f, 48f), HushButton.Style.Icon, 24f);
            HushLayout.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(48f, 48f));
            plus.Clicked = () => adjust(0.05f);

            return value;
        }

        private static void BuildRule(Transform panel, float y)
        {
            var rule = HushLayout.Image(panel, "Rule " + y, HushArt.RoundedSmall, HushPalette.WithAlpha(HushPalette.Border, 0.8f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, 1f));
            rule.type = Image.Type.Sliced;
        }

        private void AdjustMaster(float delta)
        {
            HushProgress.Data.masterVolume = Snap(HushProgress.Data.masterVolume + delta);
            CommitSettings();
        }

        private void AdjustMusic(float delta)
        {
            HushProgress.Data.musicVolume = Snap(HushProgress.Data.musicVolume + delta);
            CommitSettings();
        }

        private void AdjustSfx(float delta)
        {
            HushProgress.Data.sfxVolume = Snap(HushProgress.Data.sfxVolume + delta);
            CommitSettings();
            HushAudio.Sfx(HushAudio.SfxId.UiClick, 0.8f);
        }

        private static float Snap(float value)
        {
            return Mathf.Clamp01(Mathf.Round(value * 20f) / 20f);
        }

        private void CommitSettings()
        {
            HushProgress.NotifyChanged();
            HushAudio.ApplyVolumes();
            RefreshValues();
        }

        private void OnResetPressed()
        {
            if (Time.unscaledTime < resetArmedUntil)
            {
                HushProgress.ResetProgress();
                resetArmedUntil = 0f;
                resetButton.SetLabel("PROGRESS ERASED");
                statusLine.text = "PROFILE ERASED. THE ROOFTOPS ARE SILENT AGAIN.";
                statusLine.color = HushPalette.Danger;
                statusUntil = Time.unscaledTime + 3.2f;
                RefreshValues();
            }
            else
            {
                resetArmedUntil = Time.unscaledTime + 4f;
                resetButton.SetLabel("CONFIRM RESET");
                statusLine.text = "PRESS AGAIN TO ERASE ALL DISTRICT PROGRESS";
                statusLine.color = HushPalette.Danger;
                statusUntil = Time.unscaledTime + 4f;
            }
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            RefreshValues();
            statusLine.text = string.Empty;
            resetButton.SetLabel("RESET PROGRESS");
            resetArmedUntil = 0f;
            statusUntil = 0f;
        }

        private void RefreshValues()
        {
            var data = HushProgress.Data;
            masterValue.text = Mathf.RoundToInt(data.masterVolume * 100f) + "%";
            musicValue.text = Mathf.RoundToInt(data.musicVolume * 100f) + "%";
            sfxValue.text = Mathf.RoundToInt(data.sfxVolume * 100f) + "%";
            if (clickToggle != null) clickToggle.SetValue(data.beatClicks);
            if (shakeToggle != null) shakeToggle.SetValue(data.screenShake);
            if (motionToggle != null) motionToggle.SetValue(data.reducedMotion);
        }

        public override void Tick()
        {
            if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
            {
                resetArmedUntil = 0f;
                resetButton.SetLabel("RESET PROGRESS");
                if (statusLine.text.StartsWith("PRESS AGAIN")) statusLine.text = string.Empty;
            }

            if (statusUntil > 0f && Time.unscaledTime > statusUntil)
            {
                statusUntil = 0f;
                statusLine.text = string.Empty;
                resetButton.SetLabel("RESET PROGRESS");
            }
        }
    }

    // ====================================================================== HUD

    public sealed class HudScreen : HushScreen
    {
        private StackGame run;

        private TextMeshProUGUI districtLabel;
        private TextMeshProUGUI districtName;
        private TextMeshProUGUI floorLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI objectiveLabel;
        private TextMeshProUGUI progressLabel;
        private RectTransform progressFill;
        private Image[] hearts;
        private RectTransform scoreRect;

        private Image ring;
        private RectTransform ringRect;
        private Image ringInner;
        private Image[] beatDots;
        private TextMeshProUGUI countdown;
        private TextMeshProUGUI hint;
        private CanvasGroup comboGroup;
        private TextMeshProUGUI comboLabel;
        private TextMeshProUGUI multiplierLabel;
        private RectTransform comboRect;

        private CanvasGroup feedbackGroup;
        private TextMeshProUGUI feedbackTitle;
        private TextMeshProUGUI feedbackPoints;
        private RectTransform feedbackRect;
        private TextMeshProUGUI feedbackCenter;
        private float feedbackTime = 99f;
        private float lifeFlash;
        private Image lifeFlashImage;

        private float displayScore;
        private int lastScore;
        private float scorePop;
        private float multiplierPop;
        private int lastCombo;

        public HudScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            // Top-left identity plate.
            var plate = HushUI.Panel(Root, "Identity", HushPalette.WithAlpha(HushPalette.Panel, 0.55f), HushPalette.WithAlpha(HushPalette.Border, 0.7f), 1.5f);
            HushLayout.Place(plate, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(370f, 100f));

            var pause = HushUI.Button(plate, "II", new Vector2(54f, 54f), HushButton.Style.Icon, 20f);
            HushLayout.Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -23f), new Vector2(54f, 54f));
            pause.Clicked = () => Game.Pause();

            districtLabel = HushLayout.Text(plate, "District", "DISTRICT 01", 12f, HushPalette.Cyan, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(84f, -22f), new Vector2(270f, 22f), false, 4f);
            districtName = HushLayout.Text(plate, "Name", "Whisper Row", 23f, HushPalette.Text, TextAlignmentOptions.Left, true,
                new Vector2(0f, 1f), new Vector2(84f, -44f), new Vector2(280f, 34f), false, 1f);
            floorLabel = HushLayout.Text(plate, "Floor", "FLOOR 00 / 10", 13f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(84f, -74f), new Vector2(260f, 22f), false, 2f);

            // Score and hearts.
            HushLayout.Text(Root, "Score Cap", "SCORE", 13f, HushPalette.Muted, TextAlignmentOptions.Right, false,
                new Vector2(1f, 1f), new Vector2(-44f, -28f), new Vector2(260f, 20f), false, 4f);
            scoreLabel = HushLayout.Text(Root, "Score", "0", 56f, HushPalette.Text, TextAlignmentOptions.Right, true,
                new Vector2(1f, 1f), new Vector2(-44f, -72f), new Vector2(360f, 56f), false, 1f);
            scoreRect = scoreLabel.rectTransform;
            hearts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                hearts[i] = HushLayout.Image(Root, "Heart " + i, HushArt.Heart, HushPalette.Amber,
                    new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-44f - (2 - i) * 38f, -122f), new Vector2(28f, 28f));
            }

            // Objective and altitude gauge.
            objectiveLabel = HushLayout.Text(Root, "Objective", "STABILIZE 10 LAYERS", 13f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 28f), false, 4f);

            HushLayout.Text(Root, "Altitude", "ALTITUDE", 11f, HushPalette.Muted, TextAlignmentOptions.Right, false,
                new Vector2(1f, 0.5f), new Vector2(-30f, 194f), new Vector2(140f, 22f), false, 3f);
            var gauge = HushLayout.Image(Root, "Gauge", HushArt.Rounded, HushPalette.WithAlpha(HushPalette.Border, 0.55f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(18f, 340f));
            gauge.type = Image.Type.Sliced;
            var gaugeFill = HushLayout.Image(gauge.transform, "Fill", HushArt.Rounded, HushPalette.Amber,
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(18f, 0f));
            gaugeFill.type = Image.Type.Sliced;
            gaugeFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            gaugeFill.rectTransform.anchorMax = new Vector2(1f, 0f);
            gaugeFill.rectTransform.offsetMin = Vector2.zero;
            gaugeFill.rectTransform.offsetMax = Vector2.zero;
            progressFill = gaugeFill.rectTransform;

            progressLabel = HushLayout.Text(Root, "Gauge Label", "00 / 10", 12f, HushPalette.Amber, TextAlignmentOptions.Right, false,
                new Vector2(1f, 0.5f), new Vector2(-30f, -194f), new Vector2(140f, 22f), false, 2f);

            // Beat ring cluster.
            var cluster = HushLayout.Place(HushUI.NewRect(Root, "Beat Cluster"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(520f, 232f));
            ring = HushLayout.Image(cluster, "Ring", HushArt.Ring, HushPalette.Cyan,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(152f, 152f));
            ringRect = ring.rectTransform;
            ringInner = HushLayout.Image(cluster, "Core", HushArt.Circle, HushPalette.WithAlpha(HushPalette.Cyan, 0.6f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(46f, 46f));

            beatDots = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                beatDots[i] = HushLayout.Image(cluster, "Beat " + i, HushArt.Circle, HushPalette.WithAlpha(HushPalette.Border, 0.8f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-33f + i * 22f, -164f), new Vector2(8f, 8f));
            }

            HushLayout.Text(cluster, "Prompt", "TAP  /  SPACE", 11f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(400f, 22f), false, 5f);
            hint = HushLayout.Text(Root, "Hint", string.Empty, 18f, HushPalette.Amber, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(900f, 36f), false, 2f);

            // Combo.
            var combo = HushLayout.Place(HushUI.NewRect(Root, "Combo"), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(42f, 40f), new Vector2(280f, 96f));
            comboGroup = combo.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;
            comboRect = combo;
            HushLayout.Text(combo, "Cap", "COMBO", 11f, HushPalette.Muted, TextAlignmentOptions.Left, false,
                new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(240f, 20f), false, 4f);
            comboLabel = HushLayout.Text(combo, "Value", "×2", 36f, HushPalette.Amber, TextAlignmentOptions.Left, true,
                new Vector2(0f, 0f), new Vector2(0f, 26f), new Vector2(120f, 48f), false, 1f);
            multiplierLabel = HushLayout.Text(combo, "Mult", "x1", 15f, HushPalette.Cyan, TextAlignmentOptions.Left, false,
                new Vector2(0f, 0f), new Vector2(126f, 32f), new Vector2(100f, 28f), false, 1f);

            // Feedback.
            var feedback = HushLayout.Place(HushUI.NewRect(Root, "Feedback"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 96f), new Vector2(900f, 160f));
            feedbackGroup = feedback.gameObject.AddComponent<CanvasGroup>();
            feedbackGroup.alpha = 0f;
            feedbackRect = feedback;
            feedbackTitle = HushLayout.Text(feedback, "Title", "PERFECT", 54f, HushPalette.Amber, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(800f, 70f), false, 6f);
            feedbackPoints = HushLayout.Text(feedback, "Points", "+150", 24f, HushPalette.Text, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(500f, 36f), false, 2f);
            feedbackCenter = HushLayout.Text(feedback, "Center", "CENTER", 12f, HushPalette.Cyan, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(400f, 24f), false, 5f);

            countdown = HushLayout.Text(Root, "Countdown", string.Empty, 78f, HushPalette.Cyan, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(700f, 100f), false, 8f);

            lifeFlashImage = HushLayout.Fullscreen(Root, "Life Flash", new Color(HushPalette.Danger.r, HushPalette.Danger.g, HushPalette.Danger.b, 0f));
            lifeFlashImage.sprite = HushArt.Vignette;
            lifeFlashImage.type = Image.Type.Simple;
            lifeFlashImage.raycastTarget = false;
        }

        public void Bind(StackGame stackGame)
        {
            if (run != null)
            {
                run.Placed -= OnPlaced;
                run.LifeLost -= OnLifeLost;
            }
            run = stackGame;
            if (run == null) return;
            run.Placed += OnPlaced;
            run.LifeLost += OnLifeLost;
            displayScore = 0f;
            lastScore = 0;
            lastCombo = 0;
            feedbackGroup.alpha = 0f;
            feedbackTime = 99f;
            lifeFlash = 0f;
            RefreshStatic();
        }

        public void Unbind()
        {
            if (run != null)
            {
                run.Placed -= OnPlaced;
                run.LifeLost -= OnLifeLost;
            }
            run = null;
        }

        private void RefreshStatic()
        {
            if (run == null) return;
            districtLabel.text = run.District.Number;
            districtLabel.color = run.District.Accent;
            districtName.text = run.District.Name;
            objectiveLabel.text = $"STABILIZE {run.TargetFloors} LAYERS";
            objectiveLabel.color = run.District.Accent;
            ring.color = run.District.Accent;
            foreach (var dot in beatDots) dot.color = HushPalette.WithAlpha(HushPalette.Border, 0.8f);
            RefreshHearts(run.Lives);
        }

        private void RefreshHearts(int lives)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].color = i < lives ? HushPalette.Amber : HushPalette.WithAlpha(HushPalette.Muted, 0.22f);
            }
        }

        private void OnPlaced(PlacementFeedback feedback)
        {
            feedbackTime = 0f;
            feedbackGroup.alpha = 1f;
            feedbackRect.localScale = Vector3.one * 1.12f;
            switch (feedback.Grade)
            {
                case TimingGrade.Perfect:
                    feedbackTitle.text = feedback.Centered ? "PERFECT SEAL" : "PERFECT BEAT";
                    feedbackTitle.color = HushPalette.Amber;
                    feedbackCenter.text = feedback.Centered ? "100% BALLAST ANCHORED" : "RESONANCE BALANCED";
                    feedbackPoints.text = "+" + feedback.Points;
                    break;
                case TimingGrade.Good:
                    feedbackTitle.text = "GOOD";
                    feedbackTitle.color = HushPalette.Cyan;
                    feedbackCenter.text = feedback.Centered ? "CENTERED" : "LIGHT SLICE";
                    feedbackPoints.text = "+" + feedback.Points;
                    break;
                case TimingGrade.Offbeat:
                    feedbackTitle.text = "OFFBEAT";
                    feedbackTitle.color = HushPalette.Muted;
                    feedbackCenter.text = "OVERHANG SLICED";
                    feedbackPoints.text = "+" + feedback.Points;
                    break;
                default:
                    feedbackTitle.text = "MISS";
                    feedbackTitle.color = HushPalette.Danger;
                    feedbackPoints.text = "TOWER BREACH";
                    feedbackCenter.text = "ACOUSTIC TREMOR";
                    break;
            }

            scorePop = 1f;
            RefreshHearts(run.Lives);
        }

        private void OnLifeLost(int lives)
        {
            lifeFlash = 1f;
            RefreshHearts(lives);
        }

        public override void Tick()
        {
            if (run == null) return;
            float dt = Time.unscaledDeltaTime;

            float delta = run.Score - displayScore;
            displayScore = Mathf.Abs(delta) < 1f ? run.Score : Mathf.MoveTowards(displayScore, run.Score, Mathf.Max(80f, Mathf.Abs(delta) * 5f) * dt);
            scoreLabel.text = Mathf.RoundToInt(displayScore).ToString("N0");
            scorePop = Mathf.MoveTowards(scorePop, 0f, dt * 4f);
            scoreRect.localScale = Vector3.one * (1f + scorePop * 0.08f);

            floorLabel.text = $"FLOOR {run.Floors:00} / {run.TargetFloors:00}";
            progressLabel.text = $"{run.Floors:00} / {run.TargetFloors:00}";
            float fraction = run.TargetFloors <= 0 ? 0f : Mathf.Clamp01(run.Floors / (float)run.TargetFloors);
            progressFill.anchorMax = new Vector2(1f, Mathf.Lerp(0.01f, 1f, fraction));

            float beat = run.BeatPulse;
            float scale = 1f + beat * 0.16f;
            ringRect.localScale = new Vector3(scale, scale, 1f);
            ringRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Repeat(Time.unscaledTime * 20f, 360f));
            var ringColor = Color.Lerp(run.District.Accent, HushPalette.Amber, beat * 0.6f);
            ringColor.a = 0.45f + beat * 0.55f;
            ring.color = ringColor;
            ringInner.color = HushPalette.WithAlpha(ringColor, 0.35f + beat * 0.65f);
            ringInner.rectTransform.localScale = Vector3.one * (0.8f + beat * 0.5f);

            int activeBeat = run.BeatIndex % 4;
            for (int i = 0; i < beatDots.Length; i++)
            {
                bool active = i == activeBeat && run.BeatSeconds >= 0f;
                beatDots[i].color = active ? run.District.Accent : HushPalette.WithAlpha(HushPalette.Border, 0.8f);
                beatDots[i].rectTransform.localScale = Vector3.one * (active ? 1.6f : 1f);
            }

            int combo = run.Combo;
            if (combo != lastCombo)
            {
                if (combo > lastCombo) multiplierPop = 1f;
                lastCombo = combo;
            }
            float targetComboAlpha = combo >= 2 ? 1f : 0f;
            comboGroup.alpha = Mathf.MoveTowards(comboGroup.alpha, targetComboAlpha, dt * 8f);
            comboLabel.text = $"×{Mathf.Max(1, run.Multiplier)}";
            multiplierLabel.text = $"COMBO {combo}";
            multiplierPop = Mathf.MoveTowards(multiplierPop, 0f, dt * 5f);
            comboRect.localScale = Vector3.one * (1f + multiplierPop * 0.12f);

            string intro = run.IntroLabel;
            if (!string.IsNullOrEmpty(intro))
            {
                countdown.text = intro;
                countdown.color = intro == "GO" ? HushPalette.Amber : run.District.Accent;
                float s = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 0.04f;
                countdown.rectTransform.localScale = new Vector3(s, s, 1f);
                countdown.alpha = 1f;
            }
            else if (countdown.alpha > 0f)
            {
                countdown.alpha = Mathf.MoveTowards(countdown.alpha, 0f, dt * 3f);
            }

            hint.text = run.CurrentHint;
            if (!string.IsNullOrEmpty(hint.text))
            {
                bool isWarning = hint.text.Contains("HOWLER") || hint.text.Contains("CRITICAL");
                hint.color = isWarning ? Color.Lerp(HushPalette.Danger, HushPalette.Amber, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : run.District.Accent;
                float pulse = run.BeatPulse;
                hint.alpha = 0.82f + pulse * 0.18f;
            }
            else
            {
                hint.alpha = 0f;
            }

            if (feedbackTime < 90f)
            {
                feedbackTime += dt;
                float t = feedbackTime;
                float alpha = t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.5f) / 0.65f);
                feedbackGroup.alpha = alpha;
                float rise = Mathf.Clamp01(t / 0.8f) * 34f;
                feedbackRect.anchoredPosition = new Vector2(0f, 96f + rise);
                float pop = 1f + Mathf.Exp(-t * 9f) * 0.16f;
                feedbackRect.localScale = Vector3.one * pop;
                if (t > 1.3f) feedbackTime = 99f;
            }

            lifeFlash = Mathf.MoveTowards(lifeFlash, 0f, dt * 1.8f);
            lifeFlashImage.color = new Color(HushPalette.Danger.r, HushPalette.Danger.g, HushPalette.Danger.b, lifeFlash * 0.55f);
        }
    }

    // ====================================================================== PAUSE

    public sealed class PauseScreen : HushScreen
    {
        private TextMeshProUGUI stats;
        private TextMeshProUGUI caption;

        public PauseScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Dim", new Color(0.01f, 0.015f, 0.04f, 0.78f));
            var panel = HushUI.Panel(Root, "Panel", HushPalette.WithAlpha(HushPalette.Panel, 0.98f), HushPalette.Border, 2f, true, HushPalette.Cyan);
            HushLayout.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 640f));

            HushLayout.Text(panel, "Header", "PAUSED", 52f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(500f, 72f), false, 6f);
            caption = HushLayout.Text(panel, "Caption", "THE CITY WAITS", 12f, HushPalette.Cyan, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(500f, 26f), false, 4f);
            stats = HushLayout.Text(panel, "Stats", string.Empty, 16f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(500f, 30f), false, 2f);

            var resume = Button(panel, "RESUME", 56f, HushButton.Style.Primary, 22f);
            resume.Clicked = () => Game.Resume();
            var restart = Button(panel, "RESTART DISTRICT", -16f, HushButton.Style.Secondary, 20f);
            restart.Clicked = () => Game.RestartDistrict();
            var settings = Button(panel, "SETTINGS", -88f, HushButton.Style.Ghost, 19f);
            settings.Clicked = () => Game.OpenSettings(HushReturnTarget.Pause);
            var menu = Button(panel, "QUIT TO DISTRICT SELECT", -160f, HushButton.Style.Danger, 17f);
            menu.Clicked = () => Game.OpenDistrictSelect();

            HushLayout.Text(panel, "Escape", "ESC  —  RESUME", 12f, HushPalette.WithAlpha(HushPalette.Muted, 0.7f), TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(400f, 24f), false, 3f);
        }

        private HushButton Button(Transform panel, string label, float y, HushButton.Style style, float fontSize)
        {
            var button = HushUI.Button(panel, label, new Vector2(400f, 58f), style, fontSize);
            HushLayout.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(400f, 58f));
            return button;
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            var run = Game.CurrentRun;
            if (run != null)
            {
                stats.text = $"{run.District.Name.ToUpperInvariant()}   ·   FLOOR {run.Floors:00} / {run.TargetFloors:00}   ·   SCORE {run.Score:N0}";
                caption.text = "THE CITY HOLDS ITS BREATH";
            }
            if (EventSystem.current != null)
            {
                var first = Root.GetComponentInChildren<HushButton>();
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
    }

    // ====================================================================== RESULTS

    public sealed class ResultsScreen : HushScreen
    {
        private TextMeshProUGUI header;
        private TextMeshProUGUI subtitle;
        private TextMeshProUGUI score;
        private TextMeshProUGUI newBest;
        private TextMeshProUGUI best;
        private TextMeshProUGUI statFloors;
        private TextMeshProUGUI statAccuracy;
        private TextMeshProUGUI statPerfect;
        private TextMeshProUGUI statCombo;
        private Image[] stars;
        private HushButton primary;
        private HushButton secondary;
        private HushButton tertiary;
        private HushButton quaternary;
        private RunResult result;
        private bool hasResult;

        public ResultsScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            var dim = HushLayout.Fullscreen(Root, "Dim", new Color(0.008f, 0.012f, 0.03f, 0.9f));
            dim.raycastTarget = true;

            var panel = HushUI.Panel(Root, "Panel", HushPalette.WithAlpha(HushPalette.Panel, 0.98f), HushPalette.Border, 2f, true, HushPalette.Amber);
            HushLayout.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 820f));

            header = HushLayout.Text(panel, "Header", "DISTRICT CLEARED", 48f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(820f, 70f), false, 5f);
            subtitle = HushLayout.Text(panel, "Subtitle", "WHISPER ROW IS AT REST", 13f, HushPalette.Amber, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(820f, 28f), false, 4f);

            stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = HushLayout.Image(panel, "Star " + i, HushArt.Star, HushPalette.Border,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-108f + i * 108f, -150f), new Vector2(80f, 80f));
            }

            score = HushLayout.Text(panel, "Score", "0", 72f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(700f, 72f), false, 2f);
            newBest = HushLayout.Text(panel, "New Best", "NEW BEST", 13f, HushPalette.Amber, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(500f, 24f), false, 5f);

            statFloors = BuildStat(panel, "FLOORS", -330f, -345f);
            statAccuracy = BuildStat(panel, "ACCURACY", -110f, -345f);
            statPerfect = BuildStat(panel, "PERFECT", 110f, -345f);
            statCombo = BuildStat(panel, "MAX COMBO", 330f, -345f);
            HushLayout.Place(statFloors.transform.parent as RectTransform ?? statFloors.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -345f), new Vector2(200f, 80f));
            HushLayout.Place(statAccuracy.transform.parent as RectTransform ?? statAccuracy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-110f, -345f), new Vector2(200f, 80f));
            HushLayout.Place(statPerfect.transform.parent as RectTransform ?? statPerfect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, -345f), new Vector2(200f, 80f));
            HushLayout.Place(statCombo.transform.parent as RectTransform ?? statCombo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(330f, -345f), new Vector2(200f, 80f));

            best = HushLayout.Text(panel, "Best", "BEST 0", 14f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -408f), new Vector2(600f, 24f), false, 3f);

            primary = HushUI.Button(panel, "NEXT DISTRICT", new Vector2(380f, 62f), HushButton.Style.Primary, 22f);
            HushLayout.Place((RectTransform)primary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 216f), new Vector2(380f, 62f));
            primary.Clicked = () => Game.AdvanceAfterResults();

            secondary = HushUI.Button(panel, "RETRY", new Vector2(300f, 54f), HushButton.Style.Secondary, 19f);
            HushLayout.Place((RectTransform)secondary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 146f), new Vector2(300f, 54f));
            secondary.Clicked = () => Game.RetryDistrict();

            tertiary = HushUI.Button(panel, "DISTRICT SELECT", new Vector2(280f, 48f), HushButton.Style.Ghost, 16f);
            HushLayout.Place((RectTransform)tertiary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 86f), new Vector2(280f, 48f));
            tertiary.Clicked = () => Game.OpenDistrictSelect();

            quaternary = HushUI.Button(panel, "TITLE", new Vector2(200f, 44f), HushButton.Style.Ghost, 15f);
            HushLayout.Place((RectTransform)quaternary.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(200f, 44f));
            quaternary.Clicked = () => Game.OpenTitle();
        }

        private TextMeshProUGUI BuildStat(Transform parent, string label, float x, float y)
        {
            var container = HushLayout.Place(HushUI.NewRect(parent, "Stat " + label), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(x, y), new Vector2(200f, 80f));
            HushLayout.Text(container, "Cap", label, 11f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(190f, 20f), false, 3f);
            return HushLayout.Text(container, "Value", "0", 28f, HushPalette.Text, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(190f, 40f), false, 1f);
        }

        public void Present(RunResult runResult)
        {
            result = runResult;
            hasResult = true;
            var definition = DistrictCatalog.Get(runResult.DistrictIndex);

            header.text = runResult.Success ? "DISTRICT CLEARED" : "SIGNAL LOST";
            header.color = runResult.Success ? HushPalette.Text : HushPalette.Danger;
            subtitle.text = runResult.Success
                ? definition.Name.ToUpperInvariant() + " IS AT REST"
                : "THE TOWER RETURNED TO THE CITY";
            subtitle.color = runResult.Success ? definition.Accent : HushPalette.Danger;
            score.text = runResult.Score.ToString("N0");
            newBest.enabled = runResult.NewBest && runResult.Success;
            best.text = $"BEST  {runResult.Best:N0}     ·     {definition.Name.ToUpperInvariant()}";
            statFloors.text = $"{runResult.Floors}/{runResult.Target}";
            statAccuracy.text = Mathf.RoundToInt(runResult.Accuracy * 100f) + "%";
            statPerfect.text = runResult.Perfects.ToString();
            statCombo.text = "×" + runResult.MaxCombo;

            for (int i = 0; i < 3; i++)
            {
                bool filled = i < runResult.Stars;
                stars[i].color = filled ? HushPalette.Amber : HushPalette.WithAlpha(HushPalette.Border, 0.8f);
                stars[i].rectTransform.localScale = Vector3.one * (filled ? 1f : 0.82f);
            }

            bool last = runResult.DistrictIndex >= DistrictCatalog.Count - 1;
            if (runResult.Success)
            {
                primary.gameObject.SetActive(true);
                primary.SetLabel(last ? "EPILOGUE" : "NEXT DISTRICT");
                primary.SetInteractable(true);
                secondary.gameObject.SetActive(true);
                secondary.SetLabel("RETRY DISTRICT");
                tertiary.SetLabel("DISTRICT SELECT");
                quaternary.gameObject.SetActive(false);
            }
            else
            {
                primary.gameObject.SetActive(false);
                secondary.gameObject.SetActive(true);
                secondary.SetLabel("TRY AGAIN");
                tertiary.SetLabel("DISTRICT SELECT");
                quaternary.gameObject.SetActive(true);
            }
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            if (hasResult)
            {
                Game.StartCoroutine(RevealStars(instant));
            }
            if (EventSystem.current != null)
            {
                var targetBtn = (primary != null && primary.gameObject.activeSelf && primary.IsInteractable) ? primary : secondary;
                if (targetBtn != null) EventSystem.current.SetSelectedGameObject(targetBtn.gameObject);
            }
        }

        private IEnumerator RevealStars(bool instant)
        {
            int earned = result.Stars;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].rectTransform.localScale = Vector3.one * 0.2f;
                stars[i].color = HushPalette.WithAlpha(HushPalette.Border, 0.6f);
            }

            for (int i = 0; i < earned; i++)
            {
                if (!instant) yield return new WaitForSecondsRealtime(0.16f);
                stars[i].color = HushPalette.Amber;
                HushAudio.Sfx(HushAudio.SfxId.Star, 0.7f);
                float t = 0f;
                while (t < 0.32f)
                {
                    t += Time.unscaledDeltaTime;
                    float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.32f), 3f);
                    float s = Mathf.LerpUnclamped(0.2f, 1.12f, k);
                    stars[i].rectTransform.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                stars[i].rectTransform.localScale = Vector3.one;
            }
        }
    }

    // ====================================================================== ENDING

    public sealed class EndingScreen : HushScreen
    {
        private TextMeshProUGUI stats;
        private Image[] stars;

        public EndingScreen(HushGame game) : base(game) { }

        protected override void OnBuild()
        {
            HushLayout.Fullscreen(Root, "Night", new Color(0.008f, 0.012f, 0.028f, 0.98f));
            var glow = HushLayout.Image(Root, "Dawn", HushArt.Glow, HushPalette.WithAlpha(HushPalette.Amber, 0.2f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -80f), new Vector2(1300f, 700f));
            var random = new System.Random(99);
            for (int i = 0; i < 40; i++)
            {
                var star = HushLayout.Image(Root, "Star " + i, HushArt.Star,
                    HushPalette.WithAlpha(i % 4 == 0 ? HushPalette.Amber : HushPalette.Cyan, 0.15f + (float)random.NextDouble() * 0.45f),
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble()),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f + (float)random.NextDouble() * 10f, 6f + (float)random.NextDouble() * 10f));
            }

            HushLayout.Text(Root, "Kicker", "THE NIGHT HAS ENDED", 14f, HushPalette.Cyan, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(900f, 30f), false, 6f);
            HushLayout.Text(Root, "Title", "THE CITY\nSLEEPS", 110f, HushPalette.Amber, TextAlignmentOptions.Center, true,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1200f, 260f), false, 10f);

            HushLayout.Text(Root, "Epilogue",
                "Three rooftops stabilized. The resonance fades into the mortar.\nNoa sets down the trowel. For the first time in a long time, the city is quiet.",
                18f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 70f), true, 0f);

            stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = HushLayout.Image(Root, "Final Star " + i, HushArt.Star, HushPalette.Amber,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56f + i * 56f, -110f), new Vector2(38f, 38f));
            }

            stats = HushLayout.Text(Root, "Stats", string.Empty, 14f, HushPalette.Muted, TextAlignmentOptions.Center, false,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(900f, 30f), false, 3f);

            var again = HushUI.Button(Root, "RETURN TO TITLE", new Vector2(360f, 64f), HushButton.Style.Primary, 22f);
            HushLayout.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(360f, 64f));
            again.Clicked = () => Game.OpenTitle();

            var districts = HushUI.Button(Root, "DISTRICT SELECT", new Vector2(300f, 52f), HushButton.Style.Ghost, 17f);
            HushLayout.Place((RectTransform)districts.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(300f, 52f));
            districts.Clicked = () => Game.OpenDistrictSelect();

            HushLayout.Text(Root, "Credit", "HUSH BLOCKS  ·  A PROCEDURAL NOCTURNE  ·  v1.0", 11f, HushPalette.WithAlpha(HushPalette.Muted, 0.55f),
                TextAlignmentOptions.Center, false, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 24f), false, 3f);
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            var save = HushProgress.Data;
            stats.text =
                $"{HushProgress.TotalStars} / {DistrictCatalog.Count * 3} STARS     ·     {save.totalFloors} LAYERS     ·     {save.totalPerfects} PERFECT     ·     {HushProgress.ClearedDistricts} / {DistrictCatalog.Count} DISTRICTS";
            for (int i = 0; i < stars.Length; i++) stars[i].rectTransform.localScale = Vector3.one * 1f;
            if (EventSystem.current != null)
            {
                var again = Root.GetComponentInChildren<HushButton>();
                if (again != null) EventSystem.current.SetSelectedGameObject(again.gameObject);
            }
        }
    }

    public static class HushScreens
    {
        public static void ShowTitleScreen() =>
            HushGame.EnsureInstance().ShowTitleScreen();

        public static void ShowDistrictSelect(
            int currentIndex,
            DistrictDefinition[] districts) =>
            HushGame.EnsureInstance().ShowDistrictSelect();

        public static void ShowGameplayHUD() =>
            HushGame.EnsureInstance().ShowGameplayHUD();

        public static void ShowHazardWarning() =>
            HushGame.EnsureInstance().ShowHazardWarning();

        public static void ShowSettings() =>
            HushGame.EnsureInstance().ShowSettings(HushReturnTarget.Title);

        public static void ShowHowTo() =>
            HushGame.EnsureInstance().ShowHowTo(HushReturnTarget.Title);

        public static void UpdateHeightDisplay(int height) =>
            HushGame.EnsureInstance().UpdateHeightDisplay(height);

        public static void UpdateSilenceChain(int chain) =>
            HushGame.EnsureInstance().UpdateSilenceChain(chain);
    }
}
