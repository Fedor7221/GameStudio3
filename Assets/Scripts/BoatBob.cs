using UnityEngine;

// The boat at the surface rocks gently on the waves.
public class BoatBob : MonoBehaviour
{
    public float bobAmount = 0.08f;
    public float bobSpeed = 1.6f;
    public float rockAngle = 3f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        float t = Time.time * bobSpeed;
        transform.localPosition = startPos + new Vector3(0f, Mathf.Sin(t) * bobAmount, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f + 1f) * rockAngle);
    }
}
