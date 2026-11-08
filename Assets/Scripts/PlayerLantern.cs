using UnityEngine;

// Level 2: the diver carries a lantern. It glows warm around the diver and throws a beam
// in the aim direction (the same way the harpoon flies), cutting through the DarknessOverlay.
// No art needed: the lantern sprite is drawn in code as small pixel art.
public class PlayerLantern : MonoBehaviour
{
    public static PlayerLantern Instance { get; private set; }

    public PlayerMovement player;

    [Header("Beam")]
    public float beamLength = 7f;
    public float beamHalfAngle = 26f;      // Degrees
    public float beamTurnSpeed = 8f;       // How quickly the beam follows the stick
    [Range(0f, 1f)] public float beamStrength = 0.9f;

    [Header("Lantern")]
    public Vector2 handOffset = new Vector2(0.45f, -0.15f); // Mirrored when facing left
    public float lanternSize = 0.45f;      // World height of the lantern sprite
    public Color lightColor = new Color(1f, 0.82f, 0.45f, 1f);
    public float haloRadius = 1.3f;
    [Range(0f, 1f)] public float haloIntensity = 0.45f;
    public float flicker = 0.08f;
    public int sortingOrder = 112;

    public Vector3 BeamOrigin => lantern != null ? lantern.position : transform.position;
    public Vector3 BeamDirection { get; private set; } = Vector3.right;
    public float FlickerFactor { get; private set; } = 1f;

    private Transform lantern;
    private SpriteRenderer halo;
    private float swing;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (player == null) player = GetComponent<PlayerMovement>();
        BuildLantern();
    }

    void BuildLantern()
    {
        GameObject go = new GameObject("Lantern");
        go.transform.SetParent(transform, false);
        lantern = go.transform;
        Vector3 ps = transform.lossyScale;
        lantern.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(ps.x), 0.001f), 1f / Mathf.Max(Mathf.Abs(ps.y), 0.001f), 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Texture2D tex = DrawLanternTexture();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.85f), tex.height / lanternSize, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = sortingOrder;

        GameObject h = new GameObject("LanternHalo");
        h.transform.SetParent(lantern, false);
        h.transform.localPosition = new Vector3(0f, -lanternSize * 0.45f, 0f);
        halo = h.AddComponent<SpriteRenderer>();
        halo.sprite = LightTextures.Glow();
        halo.sortingOrder = sortingOrder - 1;
    }

    void LateUpdate()
    {
        if (lantern == null) return;

        bool left = player != null && player.FacingLeft;
        Vector3 aim = player != null ? player.AimDirection : Vector3.right;
        if (aim.sqrMagnitude < 0.01f) aim = left ? Vector3.left : Vector3.right;

        // Smoothly turn the beam toward the aim direction
        BeamDirection = Vector3.Slerp(BeamDirection, aim.normalized, Time.deltaTime * beamTurnSpeed).normalized;

        // Lantern hangs from the hand and swings a little
        swing = Mathf.Sin(Time.time * 3f) * 8f;
        lantern.localPosition = new Vector3(left ? -handOffset.x : handOffset.x, handOffset.y, -0.1f);
        lantern.localRotation = Quaternion.Euler(0f, 0f, swing);

        FlickerFactor = 1f + (Mathf.PerlinNoise(Time.time * 9f, 3f) - 0.5f) * 2f * flicker;

        Color c = lightColor;
        c.a = haloIntensity * FlickerFactor;
        halo.color = c;
        float s = haloRadius * 2f * FlickerFactor;
        halo.transform.localScale = new Vector3(s, s, 1f) / Mathf.Max(lantern.lossyScale.x, 0.001f);
    }

    // 0..1 light from the beam at a world position (used by DarknessOverlay)
    public float BeamLightAt(float px, float py)
    {
        Vector3 o = BeamOrigin;
        float vx = px - o.x, vy = py - o.y;
        float d = Mathf.Sqrt(vx * vx + vy * vy);
        if (d > beamLength || d < 0.0001f) return d < 0.0001f ? beamStrength : 0f;

        float cos = (vx * BeamDirection.x + vy * BeamDirection.y) / d;
        float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
        float half = beamHalfAngle * FlickerFactor;
        float edge = 1f - Mathf.Clamp01((angle - half * 0.6f) / (half * 0.4f));     // Soft sides
        float fall = 1f - Mathf.Clamp01((d - beamLength * 0.55f) / (beamLength * 0.45f)); // Fades at the tip
        return edge * fall * beamStrength;
    }

    // 8x12 pixel-art lantern: dark iron frame, handle, warm glowing glass
    static Texture2D DrawLanternTexture()
    {
        string[] rows =
        {
            "...HH...",
            "..H..H..",
            "...HH...",
            ".FFFFFF.",
            ".FYYYYF.",
            ".FYWWYF.",
            ".FYWWYF.",
            ".FYYYYF.",
            ".FOYYOF.",
            ".FFFFFF.",
            "..FFFF..",
            "........",
        };
        int w = 8, h = rows.Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y]; // Texture rows go bottom-up
            for (int x = 0; x < w; x++)
            {
                Color c;
                switch (row[x])
                {
                    case 'H': c = new Color(0.35f, 0.33f, 0.3f); break;   // Handle
                    case 'F': c = new Color(0.22f, 0.2f, 0.18f); break;   // Iron frame
                    case 'Y': c = new Color(1f, 0.78f, 0.3f); break;      // Glass
                    case 'W': c = new Color(1f, 0.97f, 0.8f); break;      // Flame core
                    case 'O': c = new Color(1f, 0.55f, 0.15f); break;     // Warm bottom
                    default: c = new Color(0f, 0f, 0f, 0f); break;
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }
}
