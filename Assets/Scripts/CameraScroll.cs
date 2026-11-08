using UnityEngine;

public class CameraScroll : MonoBehaviour
{
    public float speed = 2.0f;
    public bool isDiving = false;

    // Camera shake variables
    public bool isShaking = false;
    public float shakeAmount = 0.03f;
    public float shakeFrequency = 6f;

    // Stronger shake used when health is low
    public bool isStrongShaking = false;
    public float strongShakeAmount = 0.07f;

    // Short, sharp shake used when the player takes damage
    public float hitShakeFrequency = 25f;
    private float hitShakeAmount = 0f;
    private float hitShakeDuration = 0f;
    private float hitShakeTimer = 0f;

    // Offset applied last frame, removed before moving so shake never accumulates
    private Vector3 shakeOffset = Vector3.zero;

    public void HitShake(float amount, float duration)
    {
        hitShakeAmount = amount;
        hitShakeDuration = duration;
        hitShakeTimer = duration;
    }

    void Update()
    {
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;

        if (isDiving)
        {
            // Calculate normal downward movement
            transform.Translate(Vector3.down * speed * Time.deltaTime);

            // Add smooth shake offset if oxygen or health is low
            if (isShaking || isStrongShaking)
            {
                float amount = isStrongShaking ? strongShakeAmount : shakeAmount;
                float t = Time.time * shakeFrequency;
                shakeOffset.x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * amount;
                shakeOffset.y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * amount;
            }
        }

        // Damage shake fades out over its duration
        if (hitShakeTimer > 0f)
        {
            hitShakeTimer -= Time.deltaTime;
            float amount = hitShakeAmount * Mathf.Clamp01(hitShakeTimer / hitShakeDuration);
            float t = Time.time * hitShakeFrequency;
            shakeOffset.x += (Mathf.PerlinNoise(t, 10f) - 0.5f) * 2f * amount;
            shakeOffset.y += (Mathf.PerlinNoise(10f, t) - 0.5f) * 2f * amount;
        }

        transform.position += shakeOffset;
    }
}
