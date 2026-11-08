using UnityEngine;

// Level 2 enemy: an anglerfish that hunts in the dark.
// - Only its lure (small glowing dot) is visible in the dark; its body appears only inside the player's light.
// - Lurk -> Stalk (creeps toward the player) -> Telegraph (lure flashes, short pause) -> Lunge (fast dash) -> Recover.
// - After a few lunges it gives up and swims back into the dark.
// Prefab needs: tag "Enemy", a trigger Collider (Sphere), and the fenerbalık sprite on a child SpriteRenderer.
public class DarkChaser : MonoBehaviour
{
    enum State { Lurk, Stalk, Telegraph, Lunge, Recover, Leave }

    [Header("Senses")]
    public float detectRange = 9f;      // Starts stalking inside this distance
    public float lungeRange = 4.5f;     // Starts the attack inside this distance

    [Header("Speeds (EnemyChaser is 2.5)")]
    public float lurkSpeed = 0.8f;
    public float stalkSpeed = 3.6f;
    public float lungeSpeed = 12f;
    public float leaveSpeed = 6f;

    [Header("Timing")]
    public float telegraphTime = 0.4f;  // Fair warning for the player before the lunge
    public float lungeTime = 0.45f;
    public float recoverTime = 0.9f;
    public int maxLunges = 3;

    [Header("Visuals")]
    public SpriteRenderer bodySprite;   // Hidden in the dark
    public GlowLight lure;              // Optional child GlowLight on the lure tip
    public float lureIdleIntensity = 0.35f;
    public float lureFlashIntensity = 1f;
    [Tooltip("Body alpha when outside the light circle. 0 = completely invisible.")]
    [Range(0f, 1f)] public float hiddenAlpha = 0f;
    public float revealRadiusNoOverlay = 4f; // Used in scenes without DarknessOverlay
    public bool spriteFacesRight = true;      // Set false if the fenerbalık art looks left

    private State state = State.Lurk;
    private Transform player;
    private Transform cam;
    private float stateTimer;
    private int lungesDone;
    private Vector3 lungeDir;
    private Vector3 driftDir;
    private Vector3 recoverVelocity;
    private Color bodyBaseColor = Color.white;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        if (Camera.main != null) cam = Camera.main.transform;

        if (bodySprite == null) bodySprite = GetComponentInChildren<SpriteRenderer>();
        if (bodySprite != null) bodyBaseColor = bodySprite.color;
        if (lure == null) lure = GetComponentInChildren<GlowLight>();

        driftDir = new Vector3(Random.value < 0.5f ? -1f : 1f, Random.Range(-0.3f, 0.3f), 0f).normalized;
        UpdateVisibility();
    }

    void Update()
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.z = 0f;
        float dist = toPlayer.magnitude;
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Lurk:
                Move(driftDir * lurkSpeed);
                SetLure(lureIdleIntensity + Mathf.Sin(Time.time * 2f) * 0.1f);
                if (dist <= detectRange) state = State.Stalk;
                break;

            case State.Stalk:
                // Curves in slightly from the side instead of a straight line: harder to read in the dark
                Vector3 side = Vector3.Cross(toPlayer.normalized, Vector3.forward) * Mathf.Sin(Time.time * 1.5f) * 0.6f;
                Move((toPlayer.normalized + side).normalized * stalkSpeed);
                SetLure(lureIdleIntensity);
                if (dist <= lungeRange) Enter(State.Telegraph, telegraphTime);
                break;

            case State.Telegraph:
                // Freeze, lure blinks bright: the arcade "tell" before the hit
                SetLure(Mathf.Repeat(Time.time * 12f, 1f) > 0.5f ? lureFlashIntensity : lureIdleIntensity);
                lungeDir = toPlayer.normalized; // Aim locks at the END of the tell, so dodging works
                if (stateTimer <= 0f)
                {
                    Enter(State.Lunge, lungeTime);
                    AudioManager.PlaySfx(Sfx.ChaserLunge);
                }
                break;

            case State.Lunge:
                Move(lungeDir * lungeSpeed);
                SetLure(lureFlashIntensity);
                if (stateTimer <= 0f)
                {
                    lungesDone++;
                    recoverVelocity = lungeDir * lungeSpeed * 0.5f;
                    Enter(State.Recover, recoverTime);
                }
                break;

            case State.Recover:
                // Glide out of the lunge
                recoverVelocity = Vector3.Lerp(recoverVelocity, Vector3.zero, Time.deltaTime * 4f);
                Move(recoverVelocity);
                SetLure(lureIdleIntensity * 0.5f);
                if (stateTimer <= 0f)
                {
                    if (lungesDone >= maxLunges) BeginLeave();
                    else state = dist <= detectRange ? State.Stalk : State.Lurk;
                }
                break;

            case State.Leave:
                Move(driftDir * leaveSpeed);
                SetLure(0f);
                break;
        }

        Face(state == State.Leave || state == State.Lurk ? driftDir.x : toPlayer.x);
        UpdateVisibility();
        CleanupOffscreen();
    }

    void Enter(State s, float time)
    {
        state = s;
        stateTimer = time;
    }

    void BeginLeave()
    {
        state = State.Leave;
        gameObject.tag = "Untagged"; // Harmless while leaving (same rule as EnemyChaser)
        driftDir = new Vector3(transform.position.x > 0f ? 1f : -1f, -0.4f, 0f).normalized;
    }

    void Move(Vector3 velocity)
    {
        Vector3 p = transform.position + velocity * Time.deltaTime;
        p.z = 0f;
        transform.position = p;
    }

    void Face(float dirX)
    {
        if (bodySprite == null || Mathf.Abs(dirX) < 0.01f) return;
        bool right = dirX > 0f;
        bodySprite.flipX = spriteFacesRight ? !right : right;

        // Keep the lure on the front of the head when flipping
        if (lure != null)
        {
            Vector3 lp = lure.transform.localPosition;
            lp.x = Mathf.Abs(lp.x) * (right ? 1f : -1f);
            lure.transform.localPosition = lp;
        }
    }

    void SetLure(float value)
    {
        if (lure != null) lure.SetIntensity(Mathf.Clamp01(value));
    }

    void UpdateVisibility()
    {
        if (bodySprite == null) return;

        float light;
        if (DarknessOverlay.Instance != null) light = DarknessOverlay.Instance.LightAt(transform.position);
        else if (player != null)
            light = 1f - Mathf.SmoothStep(0f, 1f, (Vector3.Distance(transform.position, player.position) - revealRadiusNoOverlay) / 1.5f);
        else light = 1f;

        Color c = bodyBaseColor;
        c.a = Mathf.Lerp(hiddenAlpha, bodyBaseColor.a, light);
        bodySprite.color = c;
    }

    void CleanupOffscreen()
    {
        if (cam != null && Mathf.Abs(transform.position.y - cam.position.y) > 15f) Destroy(gameObject);
        else if (Mathf.Abs(transform.position.x) > 15f) Destroy(gameObject);
    }
}
