using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float dashSpeed = 15.0f;
    public float dashDuration = 0.2f;

    public CameraScroll mainCameraScript;
    public ObjectSpawner spawnerScript;

    public float screenMinX = -8.5f;
    public float screenMaxX = 8.5f;

    public SpriteRenderer spriteRend;

    // Harpoon Settings
    public GameObject harpoonPrefab;
    public float harpoonCooldown = 1.0f;
    private float nextHarpoonTime = 0f;
    private Vector3 lastAimDirection = Vector3.up;

    public GameObject bossPrefab;

    // UI Settings
    public Slider oxygenSlider;
    public Image oxygenFillImage;
    public OxygenBarUI oxygenBarUI;     // When set, it handles the bar's colours and warning effects
    public float maxOxygen = 100f;
    public float oxygenDrainRate = 5f;
    private float currentOxygen;

    // Low oxygen red screen flash
    public Image lowOxygenFlash;
    public float lowOxygenFlashDuration = 0.25f;
    public float lowOxygenFlashAlpha = 0.3f;
    private bool isLowOxygen = false;

    // Health Settings
    public Slider healthSlider;
    public TextMeshProUGUI healthText;
    public HealthBarUI healthBarUI;
    public float maxHealth = 100f;
    public float noOxygenDamage = 10f;
    public float noOxygenDamageInterval = 2f;
    public float enemyDamage = 15f;
    public float damageCooldown = 1f; // Prevents one enemy from hitting every frame
    private float currentHealth;
    private float nextDamageTime = 0f;
    private float noOxygenTimer = 0f;
    private Coroutine screenFlashRoutine;

    public GameOverUI gameOverUI;
    private bool isDead = false;

    // Damage feedback (screen shake + red flash)
    public float enemyHitShake = 0.25f;
    public float enemyHitFlashAlpha = 0.45f;
    public float noOxygenHitShake = 0.12f;
    public float noOxygenHitFlashAlpha = 0.3f;
    public float hitShakeDuration = 0.3f;

    // Low health warning: stronger shake and repeating red flash
    public float lowHealthThreshold = 25f;
    public float lowHealthFlashInterval = 1.5f;
    private float nextLowHealthFlashTime = 0f;

    public Slider pearlProgressBar;
    public TextMeshProUGUI bossWarningText;
    private int collectedPearls = 0;
    private bool bossSequenceStarted = false;

    // ---------- Animation ----------
    // Animator: state names must match the Aseprite tags (the Aseprite importer makes one state per tag).
    // States that don't exist are skipped, so the game runs fine before the tags are drawn.
    [Header("Animation")]
    public Animator animator;
    public string idleState = "Idle";
    public string swimState = "Swim";
    public string dashState = "Dash";
    public string shootState = "Shoot";
    public string hurtState = "Hurt";
    public float shootAnimTime = 0.25f;
    public float hurtAnimTime = 0.4f;

    // Procedural motion on the sprite child: bob, tilt, stroke squash, recoil, hurt blink.
    // Works with single-frame sprites; keep it on together with Aseprite animations for extra juice.
    public bool proceduralAnimation = true;
    public float idleBobAmount = 0.06f;
    public float idleBobSpeed = 2.5f;
    public float swimTiltAngle = 14f;
    public float swimStrokeSpeed = 10f;
    public float swimSquash = 0.06f;
    public float shootRecoilScale = 0.18f;
    public float hurtBlinkSpeed = 22f;
    public Color hurtTint = new Color(1f, 0.35f, 0.35f, 1f);

    private float shootAnimTimer = 0f;
    private float hurtAnimTimer = 0f;
    private int currentAnimHash = 0;
    private Transform spriteTf;
    private Vector3 spriteBasePos;
    private Vector3 spriteBaseScale;
    private Color spriteBaseColor = Color.white;
    private float tiltCurrent = 0f;

    // ---------- Audio ----------
    [Header("Audio")]
    public float bubbleSfxMinInterval = 1.2f;
    public float bubbleSfxMaxInterval = 2.8f;
    public float pearlPitchStep = 0.04f; // Each pearl rings a bit higher: arcade "combo" feel
    private float nextBubbleSfxTime = 0f;

    private float diveTime = 0f;

    public float OxygenPercent => maxOxygen > 0f ? currentOxygen / maxOxygen : 0f;
    public Vector3 AimDirection => lastAimDirection;
    public bool FacingLeft => spriteRend != null && spriteRend.flipX;

    [Tooltip("How far above the camera centre the boss appears (shark is wide and tall).")]
    public float bossSpawnHeight = 3.2f;

    [Header("Dash")]
    public float dashCooldown = 0.9f;   // Wait after a dash ends before the next one
    public DashFX dashFX;               // Afterimages + bubble burst from the diver's sides
    private float nextDashTime = 0f;
    private bool dashReady = true;
    private float readyFlashTimer = 0f;
    private Vector3 dashDirection = Vector3.right;

    [Header("Getting hit")]
    public float bossContactDamage = 30f;   // Shark bite: hurts more than a normal enemy (15)
    public float knockbackSpeed = 9f;
    public float knockbackTime = 0.2f;
    private float knockbackTimer = 0f;
    private Vector3 knockbackVelocity;

    private Rigidbody rb;
    private float dashTimer = 0.0f;
    private bool isDashing = false;
    private Vector3 movement;
    private bool hasJumped = false;
    private float introDropTimer = 0f;
    private Camera mainCam;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        currentOxygen = maxOxygen;
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        UpdateHealthUI();

        if (pearlProgressBar != null)
        {
            pearlProgressBar.maxValue = 10;
            pearlProgressBar.value = 0;
        }

        if (oxygenSlider != null)
        {
            oxygenSlider.maxValue = maxOxygen;
            oxygenSlider.value = currentOxygen;
        }

        if (bossWarningText != null) bossWarningText.gameObject.SetActive(false);

        AudioManager.SetBreathing(false); // AudioManager survives scene reloads; silence until the dive starts

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (dashFX == null) dashFX = GetComponent<DashFX>();
        if (spriteRend != null)
        {
            spriteTf = spriteRend.transform;
            spriteBasePos = spriteTf.localPosition;
            spriteBaseScale = spriteTf.localScale;
            spriteBaseColor = spriteRend.color;
        }

        // Level 2+: we arrive already underwater, the dive starts immediately
        if (LevelSettings.Instance != null && LevelSettings.Instance.startUnderwater)
        {
            hasJumped = true;
            lastAimDirection = Vector3.down; // Lantern beam and harpoon point the way we're diving
            if (mainCameraScript != null) mainCameraScript.isDiving = true;
            AudioManager.SetBreathing(true);
            ShowBanner("LEVEL " + LevelSettings.Instance.levelNumber, 2f);
        }
    }

    // Big centre text (reuses the boss warning label)
    public void ShowBanner(string text, float duration)
    {
        if (bossWarningText == null) return;
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(text, duration));
    }

    private Coroutine bannerRoutine;

    IEnumerator BannerRoutine(string text, float duration)
    {
        bossWarningText.gameObject.SetActive(true);
        bossWarningText.text = text;
        yield return new WaitForSecondsRealtime(duration);
        bossWarningText.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    void Update()
    {
        if (isDead) return;

        if (!hasJumped)
        {
            if (Input.GetButtonDown("Jump"))
            {
                hasJumped = true;
                introDropTimer = 0.5f;
                if (mainCameraScript != null) mainCameraScript.isDiving = true;
                AudioManager.PlaySfx(Sfx.Splash);
                AudioManager.SetBreathing(true);
                if (LevelSettings.Instance != null) LevelSettings.Instance.StartDiveMusic();
            }
            UpdateAnimation();
            return;
        }

        // Deeper levels drain oxygen faster (pressure), see LevelSettings
        diveTime += Time.deltaTime;
        float drainMultiplier = LevelSettings.Instance != null ? LevelSettings.Instance.GetDrainMultiplier(diveTime) : 1f;
        if (!bossSequenceStarted) currentOxygen -= oxygenDrainRate * drainMultiplier * Time.deltaTime;

        // Out of oxygen: lose a chunk of health every few seconds instead of dying instantly
        if (currentOxygen <= 0)
        {
            currentOxygen = 0;
            if (!bossSequenceStarted)
            {
                noOxygenTimer += Time.deltaTime;
                if (noOxygenTimer >= noOxygenDamageInterval)
                {
                    noOxygenTimer = 0f;
                    ApplyDamage(noOxygenDamage, noOxygenHitShake, noOxygenHitFlashAlpha);
                }
            }
        }
        else
        {
            noOxygenTimer = 0f;
        }

        if (oxygenSlider != null)
        {
            oxygenSlider.value = currentOxygen;
            if (currentOxygen < 25f)
            {
                if (oxygenFillImage != null && oxygenBarUI == null) oxygenFillImage.color = Color.red;
                if (mainCameraScript != null) mainCameraScript.isShaking = true;

                // Flash the screen red once when oxygen first drops below the limit
                if (!isLowOxygen) ScreenFlash(lowOxygenFlashAlpha);
                isLowOxygen = true;
            }
            else
            {
                if (oxygenFillImage != null && oxygenBarUI == null) oxygenFillImage.color = Color.cyan;
                if (mainCameraScript != null) mainCameraScript.isShaking = false;
                isLowOxygen = false;
            }
        }

        if (currentHealth < lowHealthThreshold && currentHealth > 0)
        {
            if (mainCameraScript != null) mainCameraScript.isStrongShaking = true;
            if (Time.time >= nextLowHealthFlashTime)
            {
                ScreenFlash(lowOxygenFlashAlpha);
                nextLowHealthFlashTime = Time.time + lowHealthFlashInterval;
            }
        }
        else if (mainCameraScript != null)
        {
            mainCameraScript.isStrongShaking = false;
        }

        if (currentHealth <= 0) Die();

        if (introDropTimer > 0)
        {
            introDropTimer -= Time.deltaTime;
            movement = new Vector3(0f, -1f, 0f);
            lastAimDirection = Vector3.down;
        }
        else
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveY = Input.GetAxis("Vertical");
            movement = new Vector3(moveX, moveY, 0f);

            if (movement.magnitude > 0.1f)
            {
                lastAimDirection = movement.normalized;
            }

            if (spriteRend != null)
            {
                if (moveX > 0) spriteRend.flipX = false;
                else if (moveX < 0) spriteRend.flipX = true;
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) isDashing = false;
        }

        if (introDropTimer <= 0 && Input.GetButtonDown("Jump"))
        {
            TryDash();
        }

        // Cooldown over: short white blink tells the player the dash is back
        if (!dashReady && Time.time >= nextDashTime)
        {
            dashReady = true;
            readyFlashTimer = 0.12f;
        }
        if (readyFlashTimer > 0f) readyFlashTimer -= Time.deltaTime;

        if (introDropTimer <= 0 && Input.GetButtonDown("Fire1") && Time.time >= nextHarpoonTime)
        {
            ShootHarpoon();
            nextHarpoonTime = Time.time + harpoonCooldown;
        }

        UpdateSwimAudio();
        UpdateAnimation();
    }

    // ---------- Dash ----------
    void TryDash()
    {
        if (isDashing || Time.time < nextDashTime) return; // Still cooling down: no spamming

        // Dash where the stick points; standing still dashes the way we're facing
        dashDirection = movement.magnitude > 0.1f ? movement.normalized
                      : (lastAimDirection.sqrMagnitude > 0.01f ? lastAimDirection.normalized : (FacingLeft ? Vector3.left : Vector3.right));

        isDashing = true;
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashDuration + dashCooldown;
        dashReady = false;

        AudioManager.PlaySfx(Sfx.Dash);
        if (mainCameraScript != null) mainCameraScript.HitShake(0.06f, 0.12f);
        if (dashFX != null) dashFX.Play(dashDirection, spriteRend);
    }

    // 0 = just used, 1 = ready (for a HUD icon later)
    public float DashCharge => dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (nextDashTime - Time.time) / (dashDuration + dashCooldown));

    // ---------- Audio while swimming ----------
    void UpdateSwimAudio()
    {
        // Breathing speeds up when oxygen is low: the player hears the panic
        float breathPitch = isLowOxygen ? 1.25f : 1f;
        AudioManager.SetBreathing(!isDead, breathPitch);

        if (movement.magnitude > 0.1f && Time.time >= nextBubbleSfxTime)
        {
            AudioManager.PlaySfx(Sfx.Bubble);
            nextBubbleSfxTime = Time.time + Random.Range(bubbleSfxMinInterval, bubbleSfxMaxInterval);
        }
    }

    // ---------- Animation ----------
    void UpdateAnimation()
    {
        if (shootAnimTimer > 0f) shootAnimTimer -= Time.deltaTime;
        if (hurtAnimTimer > 0f) hurtAnimTimer -= Time.deltaTime;

        bool moving = hasJumped && movement.magnitude > 0.1f;

        // Priority: hurt > shoot > dash > swim > idle
        string wanted = idleState;
        if (hurtAnimTimer > 0f) wanted = hurtState;
        else if (shootAnimTimer > 0f) wanted = shootState;
        else if (isDashing) wanted = dashState;
        else if (moving) wanted = swimState;

        PlayAnimState(wanted, moving ? swimState : idleState);

        if (proceduralAnimation) UpdateProceduralAnimation(moving);
    }

    void PlayAnimState(string stateName, string fallbackState)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
        {
            hash = Animator.StringToHash(fallbackState); // e.g. no "Dash" tag yet: keep swimming
            if (!animator.HasState(0, hash)) return;
        }

        if (hash == currentAnimHash) return;
        currentAnimHash = hash;
        animator.Play(hash, 0, 0f); // Hard cut: pixel art looks wrong with blending
    }

    void UpdateProceduralAnimation(bool moving)
    {
        if (spriteTf == null) return;

        float t = Time.time;
        Vector3 pos = spriteBasePos;
        Vector3 scale = spriteBaseScale;

        if (moving)
        {
            // Swim stroke: rhythmic stretch/squash, faster while dashing
            float stroke = Mathf.Sin(t * swimStrokeSpeed * (isDashing ? 1.8f : 1f));
            scale.x *= 1f + stroke * swimSquash;
            scale.y *= 1f - stroke * swimSquash;
        }
        else
        {
            // Idle: gentle floating bob
            pos.y += Mathf.Sin(t * idleBobSpeed) * idleBobAmount;
        }

        // Tilt nose up/down with vertical input, mirrored when facing left
        float facing = spriteRend.flipX ? -1f : 1f;
        float targetTilt = moving ? movement.normalized.y * swimTiltAngle * facing : 0f;
        if (isDashing) targetTilt *= 1.5f;
        tiltCurrent = Mathf.Lerp(tiltCurrent, targetTilt, Time.deltaTime * 10f);

        // Shoot recoil: quick punch that settles over shootAnimTime
        if (shootAnimTimer > 0f)
        {
            float k = shootAnimTimer / shootAnimTime;
            scale.x *= 1f - shootRecoilScale * k;
            scale.y *= 1f + shootRecoilScale * k;
            pos -= lastAimDirection * 0.15f * k;
        }

        if (readyFlashTimer > 0f) scale *= 1.12f; // "Dash ready" pop

        spriteTf.localPosition = pos;
        spriteTf.localScale = scale;
        spriteTf.localRotation = Quaternion.Euler(0f, 0f, tiltCurrent);

        // Hurt: red tint + fast blink
        if (hurtAnimTimer > 0f)
        {
            bool visible = Mathf.Sin(t * hurtBlinkSpeed) > -0.3f;
            Color c = hurtTint;
            c.a = visible ? spriteBaseColor.a : spriteBaseColor.a * 0.25f;
            spriteRend.color = c;
        }
        else
        {
            spriteRend.color = spriteBaseColor;
        }
    }

    void FixedUpdate()
    {
        if (!hasJumped) return;
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = knockbackVelocity;
        }
        else if (isDashing) rb.linearVelocity = dashDirection * dashSpeed;
        else rb.linearVelocity = movement.normalized * moveSpeed;
    }

    void LateUpdate()
    {
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, screenMinX, screenMaxX);
        transform.position = clampedPosition;
    }

    void ShootHarpoon()
    {
        if (harpoonPrefab != null)
        {
            shootAnimTimer = shootAnimTime;
            AudioManager.PlaySfx(Sfx.HarpoonThrow);

            Vector3 spawnPosition = transform.position + (lastAimDirection * 1.5f);
            GameObject newHarpoon = Instantiate(harpoonPrefab, spawnPosition, Quaternion.identity);

            newHarpoon.transform.up = lastAimDirection;

            Harpoon harpoonScript = newHarpoon.GetComponent<Harpoon>();
            if (harpoonScript != null)
            {
                harpoonScript.direction = lastAimDirection;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("DeathWall"))
        {
            if (!bossSequenceStarted) Die();
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            TakeDamage(enemyDamage);
        }
        else if (collision.gameObject.CompareTag("Boss"))
        {
            TakeHit(bossContactDamage, collision.transform.position);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Oxygen"))
        {
            currentOxygen += 40f;
            if (currentOxygen > maxOxygen) currentOxygen = maxOxygen;
            AudioManager.PlaySfx(Sfx.OxygenPickup);
            Destroy(other.gameObject);
        }

        if (other.CompareTag("Enemy")) TakeDamage(enemyDamage);
        if (other.CompareTag("Boss")) TakeHit(bossContactDamage, other.transform.position);

        if (other.CompareTag("Pearl"))
        {
            // 1. Disable collision immediately so it isn't collected twice
            Collider col = other.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 2. Start the flying animation to the UI slider
            AudioManager.PlaySfx(Sfx.PearlFly);
            StartCoroutine(FlyPearlToUI(other.gameObject));
        }
    }

    // NEW: Pearl Flying Coroutine
    private IEnumerator FlyPearlToUI(GameObject pearl)
    {
        // Fallback if UI components are missing
        if (pearlProgressBar == null || mainCam == null)
        {
            CollectPearlLogic();
            Destroy(pearl);
            yield break;
        }

        Vector3 startPos = pearl.transform.position;
        Vector3 startScale = pearl.transform.localScale;
        float duration = 0.5f; // Flight time (half a second)
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (pearl == null) yield break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Smooth ease-in effect
            float smoothT = t * t * (3f - 2f * t);

            // Convert UI screen position to a World position
            Vector3 screenPos = pearlProgressBar.transform.position;
            screenPos.z = Mathf.Abs(mainCam.transform.position.z);
            Vector3 targetWorldPos = mainCam.ScreenToWorldPoint(screenPos);
            targetWorldPos.z = 0f;

            // Move and shrink the pearl towards the slider
            pearl.transform.position = Vector3.Lerp(startPos, targetWorldPos, smoothT);
            pearl.transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, smoothT);

            yield return null;
        }

        Destroy(pearl);
        CollectPearlLogic(); // Add to UI counter AFTER the flight is done
    }

    private void CollectPearlLogic()
    {
        collectedPearls++;
        if (pearlProgressBar != null) pearlProgressBar.value = collectedPearls;
        AudioManager.PlaySfx(Sfx.PearlCollect, collectedPearls * pearlPitchStep);

        bool levelHasBoss = LevelSettings.Instance == null || LevelSettings.Instance.hasBoss;
        if (collectedPearls >= 10 && !bossSequenceStarted && levelHasBoss)
        {
            StartBossSequence();
        }
    }

    void StartBossSequence()
    {
        bossSequenceStarted = true;
        if (spawnerScript != null) spawnerScript.isSpawningActive = false;
        if (mainCameraScript != null) mainCameraScript.isDiving = false;
        StartCoroutine(BossCountdownRoutine());
    }

    private IEnumerator BossCountdownRoutine()
    {
        AudioManager.PlaySfx(Sfx.BossWarning);

        if (bossWarningText != null)
        {
            bossWarningText.gameObject.SetActive(true);
            bossWarningText.text = "BOSS COMING!";
            yield return new WaitForSeconds(1.5f);
            bossWarningText.text = "3";
            yield return new WaitForSeconds(1f);
            bossWarningText.text = "2";
            yield return new WaitForSeconds(1f);
            bossWarningText.text = "1";
            yield return new WaitForSeconds(1f);
            bossWarningText.text = "FIGHT!";
            yield return new WaitForSeconds(1f);
            bossWarningText.gameObject.SetActive(false);
        }

        if (bossPrefab != null && mainCam != null)
        {
            Vector3 spawnPos = new Vector3(0f, mainCam.transform.position.y + bossSpawnHeight, 0f);
            Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        }
    }

    // Used by enemies that hurt from a distance (pufferfish blast)
    public void TakeHit(float amount)
    {
        if (isDead) return;
        TakeDamage(amount);
    }

    // Hit with a push away from the attacker (shark bite, boss bullets)
    public void TakeHit(float amount, Vector3 fromPosition)
    {
        if (isDead || LevelSettings.IsCompleting || Time.time < nextDamageTime) return;
        Vector3 away = transform.position - fromPosition;
        away.z = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.down;
        knockbackVelocity = away.normalized * knockbackSpeed;
        knockbackTimer = knockbackTime;
        TakeDamage(amount);
    }

    void TakeDamage(float amount)
    {
        if (LevelSettings.IsCompleting) return; // Boss is dead: nothing can hurt us during the victory
        if (Time.time < nextDamageTime) return;
        nextDamageTime = Time.time + damageCooldown;

        ApplyDamage(amount, enemyHitShake, enemyHitFlashAlpha);
    }

    // Every health loss goes through here so it always gets the same feedback
    void ApplyDamage(float amount, float shake, float flashAlpha)
    {
        currentHealth -= amount;
        UpdateHealthUI();

        hurtAnimTimer = hurtAnimTime;
        AudioManager.PlaySfx(Sfx.PlayerHurt);

        if (mainCameraScript != null) mainCameraScript.HitShake(shake, hitShakeDuration);
        ScreenFlash(flashAlpha);
        nextLowHealthFlashTime = Time.time + lowHealthFlashInterval; // Don't cut the hit flash short
        if (healthBarUI != null) healthBarUI.PlayHit(shake / noOxygenHitShake);

        if (currentHealth <= 0) Die();
    }

    void ScreenFlash(float alpha)
    {
        if (lowOxygenFlash == null) return;
        if (screenFlashRoutine != null) StopCoroutine(screenFlashRoutine);
        screenFlashRoutine = StartCoroutine(ScreenFlashRoutine(alpha));
    }

    void UpdateHealthUI()
    {
        if (currentHealth < 0) currentHealth = 0;
        if (healthSlider != null) healthSlider.value = currentHealth;
        if (healthText != null) healthText.text = Mathf.CeilToInt(currentHealth).ToString();
    }

    IEnumerator ScreenFlashRoutine(float alpha)
    {
        Color c = lowOxygenFlash.color;
        float t = 0f;
        while (t < lowOxygenFlashDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(alpha, 0f, t / lowOxygenFlashDuration);
            lowOxygenFlash.color = c;
            yield return null;
        }
        c.a = 0f;
        lowOxygenFlash.color = c;
    }

    void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Out of health or out of bounds: show the game over screen
    void Die()
    {
        if (isDead) return;
        isDead = true;

        AudioManager.SetBreathing(false);
        AudioManager.PlaySfx(Sfx.Death);

        if (gameOverUI == null)
        {
            RestartLevel();
            return;
        }

        // Clear warning effects so they don't freeze on screen while paused
        if (screenFlashRoutine != null) StopCoroutine(screenFlashRoutine);
        if (lowOxygenFlash != null)
        {
            Color c = lowOxygenFlash.color;
            c.a = 0f;
            lowOxygenFlash.color = c;
        }
        if (mainCameraScript != null)
        {
            mainCameraScript.isShaking = false;
            mainCameraScript.isStrongShaking = false;
        }

        gameOverUI.Show();
    }
}