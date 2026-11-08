using UnityEngine;

// Endless tiled ocean behind everything. Follows the camera down in whole-tile steps (seamless),
// never goes above the water surface, drifts sideways like a current and darkens with depth.
public class OceanBackground : MonoBehaviour
{
    public Sprite tile;                     // açık sular tile (Level 1) / dark water tile (Level 2)
    public float tileWorldSize = 2.5f;      // One tile on screen, in world units (chunky pixels)
    public float surfaceY = 3.5f;           // Top of the water; set very high for "no surface"
    public float width = 28f;

    [Header("Depth colour")]
    public Color shallowTint = new Color(0.75f, 0.9f, 1f, 1f);
    public Color deepTint = new Color(0.25f, 0.38f, 0.55f, 1f);
    public float depthForDeepest = 90f;     // Units below the surface where it is darkest

    [Header("Current")]
    public float driftSpeed = 0.25f;

    public int sortingOrder = -50;
    public float z = 2f;

    private SpriteRenderer sr;
    private Camera cam;
    private float tileH, tileW, height;

    void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (tile != null) sr.sprite = tile;
        if (sr.sprite == null) { enabled = false; return; }

        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.sortingOrder = sortingOrder;

        // Scale so one tile = tileWorldSize; sr.size is in local (unscaled) units
        float spriteW = sr.sprite.bounds.size.x;
        float s = tileWorldSize / spriteW;
        transform.localScale = new Vector3(s, s, 1f);
        tileW = tileWorldSize;
        tileH = sr.sprite.bounds.size.y * s;

        float viewH = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
        height = viewH + tileH * 3f;
        sr.size = new Vector2(width / s, height / s);
        UpdatePosition();
    }

    void LateUpdate()
    {
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (cam == null || sr == null) return;
        float camY = cam.transform.position.y;
        float viewH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
        float camTop = camY + viewH * 0.5f;

        // Top edge moves in whole tiles so the pattern never visibly jumps
        float steps = Mathf.Max(0f, Mathf.Floor((surfaceY - camTop) / tileH));
        float top = surfaceY - steps * tileH;
        float x = Mathf.Repeat(Time.time * driftSpeed, tileW) - tileW * 0.5f;
        transform.position = new Vector3(x, top - height * 0.5f, z);

        float depth01 = Mathf.Clamp01((surfaceY - camY) / depthForDeepest);
        sr.color = Color.Lerp(shallowTint, deepTint, depth01);
    }
}
