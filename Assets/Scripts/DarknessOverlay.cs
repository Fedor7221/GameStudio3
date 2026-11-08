using UnityEngine;
using System.Collections.Generic;

// Level 2 "dark ocean": a low-res darkness layer in front of the camera, recomputed every frame.
// Light sources cut holes into it:
//   - a soft circle around the player
//   - the lantern beam (PlayerLantern)
//   - small pools around glowing items (GlowLight.revealRadius)
// Works with the project's current URP Universal (3D) renderer - no Light2D / 2D Renderer needed.
//
// Draw order (Sorting Order, Default layer):
//   world, enemies, Dark Chaser body  <  darkness (100)  <  glowing items (105) + glows (110+)
public class DarknessOverlay : MonoBehaviour
{
    public static DarknessOverlay Instance { get; private set; }

    public Transform target;                 // The player
    public PlayerMovement player;            // Optional: light shrinks as oxygen runs out

    [Header("Player light circle (world units)")]
    public float lightRadius = 2.4f;         // Fully lit inside this radius
    public float softEdge = 1.4f;            // Fade-to-dark width outside it
    [Range(0f, 1f)] public float darkness = 0.95f; // 1 = pitch black outside the light
    public Color darkColor = new Color(0.0f, 0.01f, 0.04f, 1f); // Deep navy, not pure black

    [Header("Life feel")]
    public float flickerAmount = 0.06f;
    public float flickerSpeed = 7f;
    [Range(0f, 1f)] public float lowOxygenShrink = 0.35f; // Radius lost at 0% oxygen

    [Header("Rendering")]
    public int resolution = 192;             // Horizontal pixels of the darkness texture
    public float margin = 1.15f;             // Covers a bit more than the screen (camera shake)
    public int sortingOrder = 100;
    public float zOffset = -1f;              // In front of 3D meshes so they get darkened too

    public float CurrentRadius { get; private set; }

    private Camera cam;
    private Texture2D tex;
    private Color32[] pixels;
    private SpriteRenderer sr;
    private int texW, texH;
    private float viewW, viewH;
    private readonly List<GlowLight> glowCache = new List<GlowLight>();

    void Awake()
    {
        Instance = this;
        CurrentRadius = lightRadius;
    }

    void Start()
    {
        cam = Camera.main;
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (player == null && target != null) player = target.GetComponent<PlayerMovement>();

        BuildOverlay();
    }

    void BuildOverlay()
    {
        float orthoH = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        viewH = orthoH * margin;
        viewW = orthoH * aspect * margin;

        texW = resolution;
        texH = Mathf.Max(8, Mathf.RoundToInt(resolution * viewH / viewW));
        tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear; // Smooth gradients from a cheap texture
        pixels = new Color32[texW * texH];

        GameObject go = new GameObject("DarknessLayer");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        // ppu = texW -> sprite is 1 unit wide, texH/texW tall; scale by viewW fills the view
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), texW, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = sortingOrder;
        go.transform.localScale = new Vector3(viewW, viewW, 1f);
    }

    void LateUpdate()
    {
        if (tex == null) return;

        float flicker = (Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) - 0.5f) * 2f * flickerAmount;
        float oxygenFactor = player != null ? Mathf.Lerp(1f - lowOxygenShrink, 1f, player.OxygenPercent) : 1f;
        CurrentRadius = lightRadius * oxygenFactor * (1f + flicker);

        // Follow the camera (not the player): the layer always covers the screen
        Vector3 centre = cam != null ? cam.transform.position : (target != null ? target.position : Vector3.zero);
        transform.position = new Vector3(centre.x, centre.y, zOffset);

        Paint(centre.x, centre.y);
    }

    void Paint(float cx, float cy)
    {
        glowCache.Clear();
        foreach (GlowLight g in GlowLight.Active)
        {
            if (g != null && g.revealRadius > 0f) glowCache.Add(g);
        }

        byte r = (byte)(darkColor.r * 255f), gC = (byte)(darkColor.g * 255f), b = (byte)(darkColor.b * 255f);
        float left = cx - viewW * 0.5f;
        float bottom = cy - viewH * 0.5f;
        float stepX = viewW / texW;
        float stepY = viewH / texH;

        PlayerLantern lantern = PlayerLantern.Instance;
        bool hasTarget = target != null;
        float tx = hasTarget ? target.position.x : 0f, ty = hasTarget ? target.position.y : 0f;

        for (int y = 0; y < texH; y++)
        {
            float wy = bottom + (y + 0.5f) * stepY;
            int row = y * texW;
            for (int x = 0; x < texW; x++)
            {
                float wx = left + (x + 0.5f) * stepX;
                float light = hasTarget ? CircleLight(wx - tx, wy - ty, CurrentRadius, softEdge) : 1f;

                if (light < 1f && lantern != null && lantern.isActiveAndEnabled)
                    light = Mathf.Max(light, lantern.BeamLightAt(wx, wy));

                for (int i = 0; i < glowCache.Count && light < 1f; i++)
                {
                    Vector3 gp = glowCache[i].transform.position;
                    float rr = glowCache[i].revealRadius;
                    light = Mathf.Max(light, CircleLight(wx - gp.x, wy - gp.y, rr * 0.4f, rr * 0.6f) * 0.85f);
                }

                pixels[row + x] = new Color32(r, gC, b, (byte)(darkness * (1f - light) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    static float CircleLight(float dx, float dy, float radius, float soft)
    {
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d <= radius) return 1f;
        float t = (d - radius) / Mathf.Max(soft, 0.01f);
        if (t >= 1f) return 0f;
        return 1f - t * t * (3f - 2f * t); // smoothstep
    }

    // 0 = fully dark, 1 = fully lit, for any world position (used by the Dark Chaser)
    public float LightAt(Vector3 worldPos)
    {
        float light = target != null ? CircleLight(worldPos.x - target.position.x, worldPos.y - target.position.y, CurrentRadius, softEdge) : 1f;
        PlayerLantern lantern = PlayerLantern.Instance;
        if (lantern != null && lantern.isActiveAndEnabled) light = Mathf.Max(light, lantern.BeamLightAt(worldPos.x, worldPos.y));
        return light;
    }
}

// Procedural textures so no extra art is needed for lights.
public static class LightTextures
{
    private static Sprite glowSprite;

    // Soft round glow, bright centre -> transparent edge. Shared by every GlowLight.
    public static Sprite Glow()
    {
        if (glowSprite != null) return glowSprite;
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = (1f - d) * (1f - d); // Quadratic falloff looks like real light
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        return glowSprite;
    }
}
