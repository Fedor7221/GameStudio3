using UnityEngine;
using System.Collections;

// Shark boss.
// Patrol: swims side to side in the upper half of the screen and spits bullets at the player.
// Every few seconds: Telegraph (stops, shakes, flashes, roar) -> Charge (bites straight at where the
// player was) -> Return (swims back up). Gets faster and charges more often below half health.
// Contact and bullets DAMAGE the player (PlayerMovement.bossContactDamage / BossProjectile.damage),
// they no longer restart the level.
public class BossAI : MonoBehaviour
{
    public int health = 5;
    public float moveSpeed = 1.2f;        // Patrol sweep speed
    public float patrolWidth = 5.5f;

    [Header("Bullets")]
    public GameObject projectilePrefab;
    public float shootCooldown = 2.2f;

    [Header("Charge attack")]
    public float chargeInterval = 5f;     // Seconds of patrol between charges
    public float telegraphTime = 0.8f;    // Fair warning before the bite
    public float chargeSpeed = 11f;
    public float chargeMaxDistance = 9f;
    public float returnSpeed = 5f;

    [Header("Visuals")]
    public bool spriteFacesRight = true;

    private enum State { Patrol, Telegraph, Charge, Return }
    private State state = State.Patrol;

    private Transform player;
    private Transform cam;
    private Vector3 patrolOffset;          // Patrol height relative to the camera
    private float patrolPhase;
    private float shootTimer;
    private float chargeTimer;
    private float stateTimer;
    private Vector3 chargeStart;
    private Vector3 chargeDir;
    private int maxHealth;

    private Renderer rend;
    private SpriteRenderer spriteRend;
    private Transform spriteTf;
    private Vector3 spriteBasePos;
    private Color originalColor;
    private bool isDefeated = false;

    // Hitbox drawn in the Scene view when the shark is selected
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (Collider c in GetComponentsInChildren<Collider>())
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        if (Camera.main != null) cam = Camera.main.transform;

        // Patrol line = where we spawned, kept relative to the camera
        patrolOffset = cam != null ? transform.position - cam.position : transform.position;
        patrolOffset.z = 0f;
        maxHealth = health;
        shootTimer = shootCooldown;
        chargeTimer = chargeInterval * 0.6f;

        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null)
        {
            originalColor = spriteRend.color;
            spriteTf = spriteRend.transform;
            spriteBasePos = spriteTf.localPosition;
        }
        else
        {
            rend = GetComponent<Renderer>();
            if (rend != null) originalColor = rend.material.color;
        }
    }

    bool Enraged => health <= maxHealth / 2;

    Vector3 PatrolPoint()
    {
        Vector3 basePos = (cam != null ? new Vector3(0f, cam.position.y, 0f) : Vector3.zero) + new Vector3(0f, patrolOffset.y, 0f);
        float x = Mathf.Sin(patrolPhase) * patrolWidth;
        float bob = Mathf.Sin(patrolPhase * 2f) * 0.2f;
        return new Vector3(x, basePos.y + bob, 0f);
    }

    void Update()
    {
        if (player == null || isDefeated) return;

        float speedMul = Enraged ? 1.35f : 1f;
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:
            {
                Vector3 before = transform.position;
                patrolPhase += Time.deltaTime * moveSpeed * speedMul;
                transform.position = Vector3.MoveTowards(transform.position, PatrolPoint(), 12f * Time.deltaTime);
                FaceDirection(transform.position.x - before.x);
                SetTilt(0f);

                shootTimer -= Time.deltaTime;
                if (shootTimer <= 0f)
                {
                    ShootProjectile();
                    shootTimer = shootCooldown / speedMul;
                }

                chargeTimer -= Time.deltaTime;
                if (chargeTimer <= 0f)
                {
                    state = State.Telegraph;
                    stateTimer = telegraphTime / speedMul;
                    AudioManager.PlaySfx(Sfx.ChaserLunge, -0.4f); // Low roar
                }
                break;
            }

            case State.Telegraph:
            {
                // Stop, face the player, shake and flash: "I'm coming for you"
                Vector3 toPlayer = player.position - transform.position;
                FaceDirection(toPlayer.x);
                SetTilt(toPlayer);
                if (spriteTf != null) spriteTf.localPosition = spriteBasePos + (Vector3)(Random.insideUnitCircle * 0.08f);
                if (spriteRend != null)
                    spriteRend.color = Mathf.Repeat(Time.time * 10f, 1f) > 0.5f ? new Color(1f, 0.55f, 0.55f) : originalColor;

                if (stateTimer <= 0f)
                {
                    chargeDir = toPlayer.normalized; // Locks at the end of the tell, so the player can dodge
                    chargeStart = transform.position;
                    state = State.Charge;
                    if (spriteTf != null) spriteTf.localPosition = spriteBasePos;
                    if (spriteRend != null) spriteRend.color = originalColor;
                }
                break;
            }

            case State.Charge:
                transform.position += chargeDir * chargeSpeed * speedMul * Time.deltaTime;
                if (Vector3.Distance(chargeStart, transform.position) >= chargeMaxDistance || OutOfView())
                {
                    state = State.Return;
                }
                break;

            case State.Return:
            {
                Vector3 target = PatrolPoint();
                Vector3 before = transform.position;
                transform.position = Vector3.MoveTowards(transform.position, target, returnSpeed * speedMul * Time.deltaTime);
                FaceDirection(transform.position.x - before.x);
                SetTilt(target - transform.position);
                if (Vector3.Distance(transform.position, target) < 0.2f)
                {
                    state = State.Patrol;
                    chargeTimer = chargeInterval / speedMul;
                    SetTilt(0f);
                }
                break;
            }
        }

        Vector3 p = transform.position;
        p.z = 0f;
        transform.position = p;
    }

    bool OutOfView()
    {
        if (cam == null) return false;
        Vector3 d = transform.position - cam.position;
        return Mathf.Abs(d.x) > 8f || Mathf.Abs(d.y) > 4.5f;
    }

    void FaceDirection(float dx)
    {
        if (spriteRend == null || Mathf.Abs(dx) < 0.001f) return;
        spriteRend.flipX = spriteFacesRight ? dx < 0f : dx > 0f;
    }

    // Nose points toward the movement direction (small angle, looks like diving)
    void SetTilt(Vector3 dir)
    {
        if (spriteTf == null || dir.sqrMagnitude < 0.0001f) return;
        float facing = spriteRend.flipX ? -1f : 1f;
        float angle = Mathf.Clamp(Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg, -35f, 35f) * facing;
        spriteTf.localRotation = Quaternion.Slerp(spriteTf.localRotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 8f);
    }

    void SetTilt(float angle)
    {
        if (spriteTf == null) return;
        spriteTf.localRotation = Quaternion.Slerp(spriteTf.localRotation, Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 8f);
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null) return;
        Vector3 spawnPos = transform.position + new Vector3(0f, -0.8f, 0f);
        Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (Enraged)
        {
            // Second bullet slightly to the side when angry
            Instantiate(projectilePrefab, spawnPos + new Vector3(spriteRend != null && spriteRend.flipX ? -1f : 1f, 0f, 0f), Quaternion.identity);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDefeated) return;

        if (other.CompareTag("Harpoon"))
        {
            health--;
            Destroy(other.gameObject);
            StartCoroutine(FlashRed());

            if (health <= 0) LevelComplete();
            else AudioManager.PlaySfx(Sfx.BossHit);
        }
    }

    IEnumerator FlashRed()
    {
        if (spriteRend != null)
        {
            spriteRend.color = Color.red;
            yield return new WaitForSeconds(0.15f);
            spriteRend.color = originalColor;
        }
        else if (rend != null)
        {
            rend.material.color = Color.red;
            yield return new WaitForSeconds(0.15f);
            rend.material.color = originalColor;
        }
    }

    void LevelComplete()
    {
        Debug.Log("BOSS DEFEATED! TRANSITIONING TO NEXT LEVEL...");
        isDefeated = true;

        AudioManager.PlaySfx(Sfx.BossHit);
        AudioManager.PlaySfx(Sfx.EnemyExplode);

        // Clear bullets still in the air so nothing can hurt the player during the victory delay
        foreach (BossProjectile p in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
        {
            Destroy(p.gameObject);
        }

        // Hide instead of destroying right away: the scene change is handled by LevelSettings
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        LevelSettings.Complete();
    }
}
