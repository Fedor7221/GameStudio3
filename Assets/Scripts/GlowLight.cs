using UnityEngine;

// Makes an item glow in the dark (pearls, oxygen tanks, the Dark Chaser's lure).
// Adds a soft light halo and draws the item's own sprites above the darkness overlay.
// Only switches on in scenes that have a DarknessOverlay, so Level 1 prefabs stay unchanged.
public class GlowLight : MonoBehaviour
{
    public Color glowColor = new Color(0.55f, 0.85f, 1f, 1f);
    public float radius = 1.6f;              // Halo radius in world units
    [Range(0f, 1f)] public float intensity = 0.7f;
    public float pulseSpeed = 3f;            // Match FloatingItem / PearlShine for a synced "breath"
    [Range(0f, 1f)] public float pulseAmount = 0.25f;
    public Vector3 offset = Vector3.zero;

    public bool onlyInDarkLevels = true;
    public bool drawItemAboveDarkness = true; // false = only the halo shows (used for the lure)
    public int itemSortingOrder = 105;
    public int glowSortingOrder = 110;

    [Tooltip("Also lights up the darkness around the item (world units). 0 = only the halo.")]
    public float revealRadius = 0f;

    public static readonly System.Collections.Generic.List<GlowLight> Active = new System.Collections.Generic.List<GlowLight>();

    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    private SpriteRenderer glow;
    private float phase;
    private Vector2 startScale = Vector2.one;

    void Start()
    {
        if (onlyInDarkLevels && DarknessOverlay.Instance == null)
        {
            enabled = false;
            return;
        }

        phase = Random.Range(0f, Mathf.PI * 2f);
        Vector3 s = transform.lossyScale;
        startScale = new Vector2(Mathf.Max(Mathf.Abs(s.x), 0.001f), Mathf.Max(Mathf.Abs(s.y), 0.001f));

        if (drawItemAboveDarkness)
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, itemSortingOrder);
            }
        }

        GameObject g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = offset + new Vector3(0f, 0f, -1.05f); // In front of the overlay
        glow = g.AddComponent<SpriteRenderer>();
        glow.sprite = LightTextures.Glow();
        glow.sortingOrder = glowSortingOrder;
    }

    void LateUpdate()
    {
        if (glow == null) return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
        Color c = glowColor;
        c.a = intensity * pulse;
        glow.color = c;

        // Sized against the scale at spawn, so the halo follows FloatingItem pulses and shrinks with a flying pearl
        float size = radius * 2f * pulse;
        glow.transform.localScale = new Vector3(size / startScale.x, size / startScale.y, 1f);
    }

    // Lets other scripts (the Dark Chaser) flash the light
    public void SetIntensity(float value) { intensity = value; }
}
