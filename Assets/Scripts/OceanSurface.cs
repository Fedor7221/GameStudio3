using UnityEngine;

// The ocean entry: a band of waves at the surface, drawn in front of the boat hull and the diver
// so things look half-submerged. Rolls side to side and throws a splash when the diver dives in.
[ExecuteAlways]
public class OceanSurface : MonoBehaviour
{
    public Sprite tile;
    public float tileWorldSize = 1.6f;
    public float width = 28f;
    public int rows = 1;
    [Tooltip("Height of the wave band in world units. 0 = full tile rows.")]
    public float bandHeight = 0.55f;
    public Color tint = new Color(1f, 1f, 1f, 1f);
    public float waveSpeed = 0.6f;
    public float waveBob = 0.06f;
    public int sortingOrder = 5;

    [Header("Splash")]
    public Transform player;
    public Material splashMaterial;     // Same material as the swim bubbles
    public int splashCount = 40;

    private SpriteRenderer sr;
    private Vector3 basePos;
    private float tileW;
    private ParticleSystem splash;
    private bool playerWasAbove = true;

    // Also runs in the editor so the Scene/Game view shows the real surface while not playing
    void OnEnable()
    {
        if (!Application.isPlaying) Configure();
    }

    void Configure()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (tile != null) sr.sprite = tile;
        if (sr.sprite == null) return;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = sortingOrder;
        sr.color = tint;
        float s = tileWorldSize / sr.sprite.bounds.size.x;
        transform.localScale = new Vector3(s, s, 1f);
        if (bandHeight > 0f) sr.size = new Vector2(width / s, bandHeight / s);
        else sr.size = new Vector2(width / s, sr.sprite.bounds.size.y * rows);
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (tile != null) sr.sprite = tile;
        if (sr.sprite == null) { enabled = false; return; }

        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = sortingOrder;
        sr.color = tint;
        float s = tileWorldSize / sr.sprite.bounds.size.x;
        transform.localScale = new Vector3(s, s, 1f);
        tileW = tileWorldSize;
        basePos = transform.position;

        if (bandHeight > 0f)
        {
            // Thin band: only the top strip of waves, top edge stays at the surface
            float topY = basePos.y + sr.sprite.bounds.size.y * rows * s * 0.5f;
            sr.size = new Vector2(width / s, bandHeight / s);
            basePos.y = topY - bandHeight * 0.5f;
        }
        else
        {
            sr.size = new Vector2(width / s, sr.sprite.bounds.size.y * rows);
        }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) playerWasAbove = player.position.y > basePos.y;
        BuildSplash();
    }

    void Update()
    {
        if (!Application.isPlaying || sr == null) return;
        float x = Mathf.Repeat(-Time.time * waveSpeed, tileW) - tileW * 0.5f;
        float y = Mathf.Sin(Time.time * 1.7f) * waveBob;
        transform.position = new Vector3(basePos.x + x, basePos.y + y, basePos.z);

        if (player != null)
        {
            bool above = player.position.y > basePos.y;
            if (playerWasAbove && !above) Splash(player.position);
            playerWasAbove = above;
        }
    }

    void BuildSplash()
    {
        GameObject go = new GameObject("Splash");
        go.transform.SetParent(transform.parent, false);
        splash = go.AddComponent<ParticleSystem>();
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = splash.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;  // Droplets fall back down
        main.startColor = new Color(0.85f, 0.97f, 1f, 1f);
        main.maxParticles = 150;
        var emission = splash.emission;
        emission.enabled = false;
        var shape = splash.shape;
        shape.enabled = false;
        var col = splash.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (splashMaterial != null) r.sharedMaterial = splashMaterial;
        r.sortingOrder = sortingOrder + 1;
    }

    void Splash(Vector3 at)
    {
        if (splash == null) return;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < splashCount; i++)
        {
            p.position = new Vector3(at.x + Random.Range(-0.4f, 0.4f), basePos.y + 0.2f, at.z - 0.2f);
            p.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6.5f), 0f);
            p.startSize = Random.Range(0.08f, 0.22f);
            p.startLifetime = Random.Range(0.5f, 0.9f);
            splash.Emit(p, 1);
        }
    }
}
