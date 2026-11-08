using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD bar that reads at a glance (used for oxygen AND pearls):
// - colour follows a gradient by fill level (oxygen: cyan -> green -> yellow -> orange -> red)
// - dark background + tick marks so the level is easy to judge
// - optional label (off by default)
// - lowWarning: below the warning level the bar pulses and blinks, faster the lower it gets
// - a white "lost" trail shows a sudden drop
// - punchOnGain: the bar pops and flashes when the value goes up (pearl collected)
[ExecuteAlways]
public class OxygenBarUI : MonoBehaviour
{
    // ---------- Editor preview (Game/Scene view while not playing) ----------
    private GameObject[] previewCells;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        if (slider == null) slider = GetComponent<Slider>();
        if (fill == null && slider != null && slider.fillRect != null) fill = slider.fillRect.GetComponent<Image>();
        if (background == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null) background = bg.GetComponent<Image>();
        }
        if (colors == null || colors.colorKeys.Length < 2) colors = DefaultGradient();
        if (background != null) background.color = backgroundColor;
        if (fill == null || slider == null) return;

        if (segmented)
        {
            // Empty boxes, never saved into the scene
            RectTransform area = (RectTransform)fill.transform.parent;
            bool vertical = slider.direction == Slider.Direction.BottomToTop || slider.direction == Slider.Direction.TopToBottom;
            int n = Mathf.Max(1, segments);
            previewCells = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = new GameObject("PreviewCell" + i, typeof(RectTransform), typeof(Image));
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(area, false);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = i < Mathf.RoundToInt(slider.normalizedValue * n) ? colors.Evaluate((i + 1f) / n) : emptySegmentColor;
                RectTransform t = (RectTransform)go.transform;
                float a = (float)i / n, b = (float)(i + 1) / n, half = segmentGap * 0.5f;
                if (vertical) { t.anchorMin = new Vector2(0f, a); t.anchorMax = new Vector2(1f, b); t.offsetMin = new Vector2(0f, half); t.offsetMax = new Vector2(0f, -half); }
                else { t.anchorMin = new Vector2(a, 0f); t.anchorMax = new Vector2(b, 1f); t.offsetMin = new Vector2(half, 0f); t.offsetMax = new Vector2(-half, 0f); }
                previewCells[i] = go;
            }
        }
        else
        {
            fill.color = colors.Evaluate(slider.normalizedValue);
        }
    }

    void OnDisable()
    {
        if (previewCells == null) return;
        foreach (GameObject g in previewCells) if (g != null) DestroyImmediate(g);
        previewCells = null;
    }

    public Slider slider;
    public Image fill;
    public Image background;
    public TextMeshProUGUI label;

    public Gradient colors;
    public bool lowWarning = true;
    [Range(0f, 1f)] public float warningLevel = 0.3f;
    public Color backgroundColor = new Color(0.03f, 0.06f, 0.1f, 0.9f);
    public bool addTicks = true;
    public int tickSections = 4;          // 4 = a line every 25%; pearls use 10 (one per pearl)
    public bool showLossTrail = true;
    public bool punchOnGain = false;
    public float punchScale = 1.25f;
    public bool glowWhenFull = false;     // Pearl bar shimmers when full (boss time)

    [Header("Segmented (pearls)")]
    [Tooltip("Draw the value as separate boxes; each step lights one whole box (no half fills).")]
    public bool segmented = false;
    public int segments = 10;
    public float segmentGap = 3f;
    public Color emptySegmentColor = new Color(1f, 1f, 1f, 0.07f);

    private Image[] cells;
    private float[] cellFlash;
    private int litCells = 0;

    private RectTransform rect;
    private Vector3 baseScale;
    private Image trail;
    private float trailValue = 1f;
    private float lastValue = -1f;
    private float punchTimer = 0f;
    private const float PunchTime = 0.3f;

    public static Gradient PearlGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.62f, 0.5f, 1f), 0f),     // Lavender
                new GradientColorKey(new Color(0.95f, 0.75f, 1f), 0.5f),  // Pearl pink
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.85f), // Pearl white
                new GradientColorKey(new Color(1f, 0.82f, 0.25f), 1f)     // Gold when full
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void Reset()
    {
        colors = DefaultGradient();
    }

    public static Gradient DefaultGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.12f, 0.12f), 0f),    // Empty: red
                new GradientColorKey(new Color(1f, 0.5f, 0.08f), 0.2f),   // Orange
                new GradientColorKey(new Color(1f, 0.88f, 0.15f), 0.4f),  // Yellow
                new GradientColorKey(new Color(0.3f, 1f, 0.45f), 0.65f),  // Green
                new GradientColorKey(new Color(0.15f, 0.9f, 1f), 1f)      // Full: bright cyan
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        if (slider == null) slider = GetComponent<Slider>();
        if (fill == null && slider != null && slider.fillRect != null) fill = slider.fillRect.GetComponent<Image>();
        if (background == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null) background = bg.GetComponent<Image>();
        }
        if (colors == null || colors.colorKeys.Length < 2) colors = DefaultGradient();

        rect = (RectTransform)transform;
        baseScale = rect.localScale;
        if (background != null) background.color = backgroundColor;
        if (fill != null) fill.sprite = null; // Flat, solid fill reads best

        if (segmented)
        {
            BuildCells();
        }
        else
        {
            if (showLossTrail) BuildTrail();
            if (addTicks) BuildTicks();
        }
        if (slider != null)
        {
            trailValue = slider.normalizedValue;
            lastValue = slider.normalizedValue;
        }
    }

    // White bar behind the fill that slowly catches up: shows how much was just lost
    void BuildTrail()
    {
        if (fill == null) return;
        GameObject go = new GameObject("LossTrail", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(fill.transform.parent, false);
        go.transform.SetSiblingIndex(fill.transform.GetSiblingIndex()); // Behind the fill
        trail = go.GetComponent<Image>();
        trail.color = new Color(1f, 1f, 1f, 0.55f);
        trail.raycastTarget = false;
        RectTransform t = (RectTransform)go.transform;
        RectTransform f = fill.rectTransform;
        t.anchorMin = f.anchorMin; t.anchorMax = f.anchorMax;
        t.offsetMin = f.offsetMin; t.offsetMax = f.offsetMax;
    }

    void BuildTicks()
    {
        if (fill == null) return;
        RectTransform area = (RectTransform)fill.transform.parent;
        bool vertical = slider != null && (slider.direction == Slider.Direction.BottomToTop || slider.direction == Slider.Direction.TopToBottom);
        int sections = Mathf.Max(2, tickSections);
        for (int i = 1; i < sections; i++)
        {
            GameObject go = new GameObject("Tick" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = false;
            RectTransform t = (RectTransform)go.transform;
            float p = (float)i / sections;
            if (vertical)
            {
                t.anchorMin = new Vector2(0f, p); t.anchorMax = new Vector2(1f, p);
                t.sizeDelta = new Vector2(0f, 2f);
            }
            else
            {
                t.anchorMin = new Vector2(p, 0f); t.anchorMax = new Vector2(p, 1f);
                t.sizeDelta = new Vector2(2f, 0f);
            }
            t.anchoredPosition = Vector2.zero;
        }
    }

    // One box per step, inside the slider's fill area, with small gaps between them
    void BuildCells()
    {
        if (fill == null) return;
        RectTransform area = (RectTransform)fill.transform.parent;
        fill.enabled = false; // The boxes replace the stretching fill
        bool vertical = slider != null && (slider.direction == Slider.Direction.BottomToTop || slider.direction == Slider.Direction.TopToBottom);
        bool reversed = slider != null && (slider.direction == Slider.Direction.TopToBottom || slider.direction == Slider.Direction.RightToLeft);

        int n = Mathf.Max(1, segments);
        cells = new Image[n];
        cellFlash = new float[n];
        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("Cell" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = emptySegmentColor;
            RectTransform t = (RectTransform)go.transform;
            int slot = reversed ? n - 1 - i : i;
            float a = (float)slot / n, b = (float)(slot + 1) / n;
            float half = segmentGap * 0.5f;
            if (vertical)
            {
                t.anchorMin = new Vector2(0f, a); t.anchorMax = new Vector2(1f, b);
                t.offsetMin = new Vector2(0f, half); t.offsetMax = new Vector2(0f, -half);
            }
            else
            {
                t.anchorMin = new Vector2(a, 0f); t.anchorMax = new Vector2(b, 1f);
                t.offsetMin = new Vector2(half, 0f); t.offsetMax = new Vector2(-half, 0f);
            }
            cells[i] = img;
        }
        litCells = slider != null ? Mathf.RoundToInt(slider.normalizedValue * n) : 0;
    }

    void UpdateCells(float v)
    {
        int n = cells.Length;
        int lit = Mathf.Clamp(Mathf.RoundToInt(v * n), 0, n);
        for (int i = litCells; i < lit; i++) cellFlash[i] = PunchTime * 1.5f; // New box: flash
        litCells = lit;

        bool full = lit == n;
        float shimmer = full && glowWhenFull ? (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f : 0f;
        for (int i = 0; i < n; i++)
        {
            Color c = i < lit ? colors.Evaluate((i + 1f) / n) : emptySegmentColor;
            if (i < lit && full) c = Color.Lerp(c, Color.white, shimmer * 0.4f * Mathf.Abs(Mathf.Sin(i * 0.7f + Time.unscaledTime * 3f)));
            if (cellFlash[i] > 0f)
            {
                cellFlash[i] -= Time.unscaledDeltaTime;
                c = Color.Lerp(c, Color.white, Mathf.Clamp01(cellFlash[i] / (PunchTime * 1.5f)));
            }
            cells[i].color = c;
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (slider == null || fill == null) return;
        float v = slider.normalizedValue;

        if (segmented && cells != null)
        {
            UpdateCells(v);
            // Whole bar still pops when a box lights up
            if (punchOnGain && lastValue >= 0f && v > lastValue + 0.001f) punchTimer = PunchTime;
            lastValue = v;
            if (punchTimer > 0f)
            {
                punchTimer -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(punchTimer / PunchTime);
                rect.localScale = baseScale * Mathf.Lerp(1f, punchScale, k * k);
            }
            else rect.localScale = baseScale;
            if (background != null) background.color = backgroundColor;
            return;
        }

        Color c = colors.Evaluate(v);

        // Value went up (pearl collected / tank picked up): pop
        if (punchOnGain && lastValue >= 0f && v > lastValue + 0.001f) punchTimer = PunchTime;
        lastValue = v;

        if (punchTimer > 0f)
        {
            punchTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(punchTimer / PunchTime);
            c = Color.Lerp(c, Color.white, k * 0.8f);
            rect.localScale = baseScale * Mathf.Lerp(1f, punchScale, k * k);
            if (background != null) background.color = backgroundColor;
        }
        else if (glowWhenFull && v >= 0.999f)
        {
            float wave = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
            c = Color.Lerp(c, Color.white, wave * 0.45f);
            rect.localScale = baseScale * (1f + wave * 0.05f);
            if (background != null) background.color = backgroundColor;
        }
        else if (lowWarning && v < warningLevel)
        {
            // Danger: blink toward white and pulse the whole bar, faster as it empties
            float urgency = 1f - v / warningLevel;          // 0..1
            float speed = Mathf.Lerp(4f, 12f, urgency);
            float wave = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
            c = Color.Lerp(c, Color.white, wave * 0.55f);
            rect.localScale = baseScale * (1f + wave * Mathf.Lerp(0.04f, 0.1f, urgency));
            if (background != null) background.color = Color.Lerp(backgroundColor, new Color(0.45f, 0f, 0f, 0.9f), wave * 0.6f);
        }
        else
        {
            rect.localScale = baseScale;
            if (background != null) background.color = backgroundColor;
        }
        fill.color = c;

        // Trail follows the value down after a short moment, snaps up on refill
        if (trail != null)
        {
            if (v >= trailValue) trailValue = v;
            else trailValue = Mathf.MoveTowards(trailValue, v, Time.deltaTime * 0.25f);
            RectTransform t = trail.rectTransform;
            RectTransform f = fill.rectTransform;
            bool vertical = slider.direction == Slider.Direction.BottomToTop || slider.direction == Slider.Direction.TopToBottom;
            Vector2 max = f.anchorMax;
            if (vertical) max.y = trailValue; else max.x = trailValue;
            t.anchorMin = f.anchorMin;
            t.anchorMax = max;
            t.offsetMin = f.offsetMin;
            t.offsetMax = f.offsetMax;
        }

        if (label != null)
        {
            label.text = "O2 " + Mathf.CeilToInt(v * 100f) + "%";
            label.color = v < warningLevel ? c : Color.white;
        }
    }
}
