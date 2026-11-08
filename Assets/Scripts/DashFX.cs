using UnityEngine;
using System.Collections;

// Dash juice: fading afterimages of the diver + a burst of bubbles shooting out of both sides.
// Lives on the player; PlayerMovement calls Play() when a dash starts.
public class DashFX : MonoBehaviour
{
    [Header("Afterimages")]
    public int ghostCount = 4;
    public float ghostInterval = 0.04f;
    public float ghostLifetime = 0.25f;
    public Color ghostColor = new Color(0.55f, 0.95f, 1f, 0.6f);

    [Header("Side bubbles")]
    public Material bubbleMaterial;       // Same material as the swim bubbles
    public int bubbleCount = 22;
    public float bubbleSpeed = 4.5f;
    public float bubbleSize = 0.18f;
    public float sideOffset = 0.35f;      // How far from the body centre the bubbles appear

    private ParticleSystem bubbles;

    void Start()
    {
        BuildBubbles();
    }

    void BuildBubbles()
    {
        GameObject go = new GameObject("DashBubbles");
        go.transform.SetParent(transform, false);
        bubbles = go.AddComponent<ParticleSystem>();
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = bubbles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = 0f; // Velocity is set per particle in Emit
        main.startSize = new ParticleSystem.MinMaxCurve(bubbleSize * 0.5f, bubbleSize);
        main.startColor = new Color(0.85f, 0.97f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Bubbles stay where they were made
        main.gravityModifier = -0.15f; // Bubbles float up
        main.maxParticles = 200;

        var emission = bubbles.emission;
        emission.enabled = false;

        var shape = bubbles.shape;
        shape.enabled = false;

        var col = bubbles.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = bubbles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (bubbleMaterial != null) r.sharedMaterial = bubbleMaterial;
        r.sortingOrder = 6;
    }

    public void Play(Vector3 dashDir, SpriteRenderer source)
    {
        if (source != null) StartCoroutine(GhostRoutine(source));
        EmitSideBubbles(dashDir);
    }

    void EmitSideBubbles(Vector3 dashDir)
    {
        if (bubbles == null) return;
        Vector3 back = -dashDir.normalized;
        Vector3 side = Vector3.Cross(dashDir.normalized, Vector3.forward).normalized; // Perpendicular in 2D

        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < bubbleCount; i++)
        {
            float s = (i % 2 == 0) ? 1f : -1f; // Alternate left/right side of the body
            Vector3 pos = transform.position + side * sideOffset * s + (Vector3)Random.insideUnitCircle * 0.15f;
            pos.z = transform.position.z - 0.1f;
            p.position = pos;
            // Spray out sideways and backwards, like water pushed away by the dash
            Vector3 v = (side * s * Random.Range(0.6f, 1.2f) + back * Random.Range(0.4f, 1.1f)).normalized * bubbleSpeed * Random.Range(0.5f, 1f);
            p.velocity = v;
            p.startSize = Random.Range(bubbleSize * 0.4f, bubbleSize);
            p.startLifetime = Random.Range(0.35f, 0.7f);
            bubbles.Emit(p, 1);
        }
    }

    IEnumerator GhostRoutine(SpriteRenderer source)
    {
        for (int i = 0; i < ghostCount; i++)
        {
            SpawnGhost(source);
            yield return new WaitForSeconds(ghostInterval);
        }
    }

    void SpawnGhost(SpriteRenderer source)
    {
        GameObject g = new GameObject("DashGhost");
        g.transform.SetPositionAndRotation(source.transform.position + new Vector3(0f, 0f, 0.05f), source.transform.rotation);
        g.transform.localScale = source.transform.lossyScale;
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        sr.color = ghostColor;
        StartCoroutine(FadeGhost(sr));
    }

    IEnumerator FadeGhost(SpriteRenderer sr)
    {
        float t = 0f;
        while (t < ghostLifetime && sr != null)
        {
            t += Time.deltaTime;
            Color c = ghostColor;
            c.a = ghostColor.a * (1f - t / ghostLifetime);
            sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }
}
