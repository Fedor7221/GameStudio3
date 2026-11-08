using UnityEngine;
using System.Collections;

// Pufferfish: swims toward the player, puffs up when close, then explodes.
// Sprites: balon balık 1 (normal) -> 2 (inflated) -> 3 (burst).
// The blast hurts the player if they're still inside it: see the puff, dash away.
// A harpoon hit makes it burst instantly (same blast, so don't shoot it point-blank).
public class PufferEnemy : MonoBehaviour
{
    [Header("Sprites")]
    public SpriteRenderer body;
    public Sprite normalSprite;
    public Sprite inflatedSprite;
    public Sprite burstSprite;
    public bool spriteFacesRight = true;

    [Header("Movement")]
    public float swimSpeed = 2.2f;
    public float detectDistance = 10f;
    public float wobbleAmount = 0.15f;
    public float wobbleSpeed = 4f;

    [Header("Puff & burst")]
    public float inflateRange = 3f;      // Starts puffing inside this distance
    public float inflateTime = 0.9f;     // Warning time before the burst
    public float inflatedScale = 1.6f;   // Body size at full puff
    public float blastRadius = 2.2f;
    public float blastDamage = 20f;
    public float burstShowTime = 0.3f;

    private enum State { Swim, Inflate, Burst }
    private State state = State.Swim;
    private Transform player;
    private PlayerMovement playerScript;
    private Transform cam;
    private CameraScroll camScroll;
    private Vector3 baseScale;
    private Color baseColor = Color.white;
    private float wobblePhase;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerScript = p.GetComponent<PlayerMovement>();
        }
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            camScroll = Camera.main.GetComponent<CameraScroll>();
        }

        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body != null)
        {
            baseScale = body.transform.localScale;
            baseColor = body.color;
            if (normalSprite != null) body.sprite = normalSprite;
        }
        wobblePhase = Random.Range(0f, 10f);
    }

    void Update()
    {
        if (state == State.Swim) Swim();
        CleanupOffscreen();
    }

    void Swim()
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.z = 0f;
        float dist = toPlayer.magnitude;

        if (dist <= detectDistance)
        {
            Vector3 pos = Vector3.MoveTowards(transform.position, player.position, swimSpeed * Time.deltaTime);
            pos.y += Mathf.Sin(Time.time * wobbleSpeed + wobblePhase) * wobbleAmount * Time.deltaTime * 3f;
            pos.z = 0f;
            transform.position = pos;
        }
        Face(toPlayer.x);

        if (dist <= inflateRange) StartCoroutine(InflateRoutine());
    }

    IEnumerator InflateRoutine()
    {
        state = State.Inflate;
        if (inflatedSprite != null && body != null) body.sprite = inflatedSprite;
        AudioManager.PlaySfx(Sfx.Bubble, 0.3f);

        float t = 0f;
        while (t < inflateTime)
        {
            t += Time.deltaTime;
            float k = t / inflateTime;
            if (body != null)
            {
                // Swell up with a nervous shake, blinking red faster and faster
                float swell = Mathf.Lerp(1f, inflatedScale, 1f - (1f - k) * (1f - k));
                body.transform.localScale = baseScale * swell;
                body.transform.localPosition = (Vector3)(Random.insideUnitCircle * 0.05f * k);
                bool red = Mathf.Sin(t * Mathf.Lerp(10f, 40f, k)) > 0f;
                body.color = red ? Color.Lerp(baseColor, new Color(1f, 0.3f, 0.3f), k) : baseColor;
            }
            yield return null;
        }
        Burst();
    }

    // Harpoon hit or fully inflated
    public void Burst()
    {
        if (state == State.Burst) return;
        state = State.Burst;
        StopAllCoroutines();
        StartCoroutine(BurstRoutine());
    }

    IEnumerator BurstRoutine()
    {
        gameObject.tag = "Untagged"; // The blast below does the damage, not contact
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        AudioManager.PlaySfx(Sfx.EnemyExplode);
        if (camScroll != null) camScroll.HitShake(0.2f, 0.25f);

        if (player != null && playerScript != null &&
            Vector2.Distance(player.position, transform.position) <= blastRadius)
        {
            playerScript.TakeHit(blastDamage);
        }

        if (body != null)
        {
            if (burstSprite != null) body.sprite = burstSprite;
            body.color = baseColor;
            body.transform.localPosition = Vector3.zero;

            // Pop outward and fade
            Vector3 start = baseScale * inflatedScale;
            float t = 0f;
            while (t < burstShowTime)
            {
                t += Time.deltaTime;
                float k = t / burstShowTime;
                body.transform.localScale = start * Mathf.Lerp(1f, 1.5f, k);
                Color c = baseColor;
                c.a = baseColor.a * (1f - k * k);
                body.color = c;
                yield return null;
            }
        }
        Destroy(gameObject);
    }

    void Face(float dirX)
    {
        if (body == null || Mathf.Abs(dirX) < 0.01f) return;
        bool right = dirX > 0f;
        body.flipX = spriteFacesRight ? !right : right;
    }

    void CleanupOffscreen()
    {
        if (cam != null && Mathf.Abs(transform.position.y - cam.position.y) > 15f) Destroy(gameObject);
        else if (Mathf.Abs(transform.position.x) > 15f) Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, inflateRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
