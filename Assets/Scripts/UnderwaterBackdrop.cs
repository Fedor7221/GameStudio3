using UnityEngine;

// Realistic underwater look (no tiles): a depth colour gradient that follows the camera,
// sunlight shafts slanting down from the surface (fade out as you go deeper)
// and drifting particles ("marine snow") that sell the feeling of water.
// The wave tile is only used for the surface band (OceanSurface).
[ExecuteAlways]
public class UnderwaterBackdrop : MonoBehaviour
{
    public float surfaceY = 3.5f;         // Above this the backdrop is transparent (sky shows)

    [Header("Water colour by depth (0 = surface)")]
    public Gradient depthColors;
    public float gradientDepth = 90f;     // Depth (units) mapped to the end of the gradient

    [Header("Sun rays")]
    public bool sunRays = true;
    public int rayCount = 6;
    public float rayLength = 14f;
    [Range(0f, 1f)] public float rayAlpha = 0.12f;
    public float raysFadeDepth = 30f;     // Rays are gone this deep

    [Header("Marine snow")]
    public Material particleMaterial;
    public float snowRate = 14f;
    public Color snowColor = new Color(0.85f, 0.95f, 1f, 0.35f);

    public float z = 3f;

    private Camera cam;
    private Texture2D tex;
    private Color32[] px;
    private SpriteRenderer sr;
    private const int TexH = 128;
    private float viewW, viewH;
    private Transform[] rays;
    private SpriteRenderer[] rayRends;
    private float[] rayPhase;
    private ParticleSystem snow;

    void Reset()
    {
        depthColors = DefaultShallowGradient();
    }

    public static Gradient DefaultShallowGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.16f, 0.60f, 0.74f), 0f),
                new GradientColorKey(new Color(0.06f, 0.38f, 0.58f), 0.18f),
                new GradientColorKey(new Color(0.03f, 0.19f, 0.38f), 0.5f),
                new GradientColorKey(new Color(0.01f, 0.07f, 0.17f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // Visuals live under a generated root that is never saved into the scene, so the same code
    // can draw in the editor (Scene/Game view while not playing) and in play mode.
    private GameObject visRoot;

    void OnEnable()
    {
        Build();
    }

    void OnDisable()
    {
        Teardown();
    }

    void Build()
    {
        Teardown();
        cam = Camera.main;
        if (depthColors == null || depthColors.colorKeys.Length < 2) depthColors = DefaultShallowGradient();

        float orthoH = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        viewH = orthoH * 1.3f;
        viewW = orthoH * aspect * 1.3f;

        visRoot = new GameObject("UnderwaterBackdrop (generated)");
        if (!Application.isPlaying) visRoot.hideFlags = HideFlags.HideAndDontSave;

        // 1 x 128 vertical gradient stretched over the screen, recoloured every frame
        tex = new Texture2D(1, TexH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        px = new Color32[TexH];
        GameObject g = new GameObject("WaterGradient");
        g.hideFlags = visRoot.hideFlags;
        g.transform.SetParent(visRoot.transform, false);
        sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, TexH), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        sr.sprite.hideFlags = HideFlags.HideAndDontSave;
        sr.sortingOrder = -60;
        g.transform.localScale = new Vector3(viewW, viewH / TexH, 1f);

        if (sunRays) BuildRays();
        if (Application.isPlaying) BuildSnow(); // Particles only while playing
        LateUpdate();
    }

    void Teardown()
    {
        if (visRoot != null)
        {
            if (Application.isPlaying) Destroy(visRoot); else DestroyImmediate(visRoot);
        }
        visRoot = null;
        rays = null;
        snow = null;
        if (tex != null)
        {
            if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
            tex = null;
        }
    }

    void LateUpdate()
    {
        if (cam == null || visRoot == null || tex == null) return;
        Vector3 c = cam.transform.position;
        visRoot.transform.position = new Vector3(c.x, c.y, z);

        // Each texel's world height -> depth -> colour
        float bottom = c.y - viewH * 0.5f;
        for (int i = 0; i < TexH; i++)
        {
            float wy = bottom + (i + 0.5f) / TexH * viewH;
            float depth = surfaceY - wy;
            if (depth < 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
            Color col = depthColors.Evaluate(Mathf.Clamp01(depth / gradientDepth));
            px[i] = col;
        }
        tex.SetPixels32(px);
        tex.Apply(false);

        UpdateRays(c);
    }

    // ---------- Sun rays ----------
    void BuildRays()
    {
        Texture2D rt = new Texture2D(32, 128, TextureFormat.RGBA32, false);
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 128; y++)
        {
            float v = y / 127f;                       // 0 bottom .. 1 top
            float fadeV = Mathf.SmoothStep(0f, 1f, v); // Bright at the surface, gone at the bottom
            for (int x = 0; x < 32; x++)
            {
                float u = (x + 0.5f) / 32f * 2f - 1f;
                float fadeU = Mathf.Exp(-u * u * 4f);  // Soft edges
                rt.SetPixel(x, y, new Color(1f, 1f, 1f, fadeU * fadeV));
            }
        }
        rt.Apply();
        rt.hideFlags = HideFlags.HideAndDontSave;
        // Pivot at the top: the ray hangs down from the surface
        Sprite raySprite = Sprite.Create(rt, new Rect(0, 0, 32, 128), new Vector2(0.5f, 1f), 128f, 0, SpriteMeshType.FullRect);

        rays = new Transform[rayCount];
        rayRends = new SpriteRenderer[rayCount];
        rayPhase = new float[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            GameObject r = new GameObject("SunRay" + i);
            r.hideFlags = visRoot.hideFlags;
            r.transform.SetParent(visRoot.transform, false);
            SpriteRenderer rr = r.AddComponent<SpriteRenderer>();
            rr.sprite = raySprite;
            rr.sortingOrder = -55;
            float width = Random.Range(0.8f, 2.4f);
            r.transform.localScale = new Vector3(width / 0.25f, rayLength * Random.Range(0.7f, 1.1f), 1f); // Sprite is 0.25 x 1 unit
            rays[i] = r.transform;
            rayRends[i] = rr;
            rayPhase[i] = Random.Range(0f, 10f);
        }
    }

    void UpdateRays(Vector3 c)
    {
        if (rays == null) return;
        float depthFade = 1f - Mathf.Clamp01((surfaceY - c.y) / raysFadeDepth);
        for (int i = 0; i < rays.Length; i++)
        {
            float spread = viewW / rays.Length;
            float x = c.x - viewW * 0.5f + spread * (i + 0.5f) + Mathf.Sin(Time.time * 0.15f + rayPhase[i]) * 0.8f;
            rays[i].position = new Vector3(x, surfaceY, z - 0.1f);
            rays[i].rotation = Quaternion.Euler(0f, 0f, 18f + Mathf.Sin(Time.time * 0.3f + rayPhase[i]) * 4f);
            float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 0.7f + rayPhase[i] * 3f);
            rayRends[i].color = new Color(0.85f, 0.97f, 1f, rayAlpha * flicker * depthFade);
            rayRends[i].enabled = depthFade > 0.01f;
        }
    }

    // ---------- Marine snow ----------
    void BuildSnow()
    {
        GameObject go = new GameObject("MarineSnow");
        go.transform.SetParent(visRoot.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        snow = go.AddComponent<ParticleSystem>();
        snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = snow.main;
        main.loop = true;
        main.startLifetime = 9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = snowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Camera moves through them
        main.maxParticles = 300;
        main.prewarm = true;

        var emission = snow.emission;
        emission.rateOverTime = snowRate;

        var shape = snow.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(viewW, viewH, 0.1f);

        var vel = snow.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.05f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = snow.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial != null) r.sharedMaterial = particleMaterial;
        r.sortingOrder = -40;
        snow.Play();
    }
}
