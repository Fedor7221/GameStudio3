using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// Game over screen: fades in, pauses the game, any cabinet button restarts.
public class GameOverUI : MonoBehaviour
{
    public CanvasGroup panelGroup;
    public RectTransform titleText;
    public CanvasGroup restartHint;

    public float fadeDuration = 0.5f;
    public float titleStartScale = 1.6f;

    private bool isShown = false;

    void Awake()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        if (isShown) return;
        isShown = true;
        shownTime = Time.unscaledTime;
        Time.timeScale = 0f;
        panelGroup.gameObject.SetActive(true);
        panelGroup.blocksRaycasts = true;
        StartCoroutine(ShowRoutine());
    }

    // Arcade cabinet: no keyboard. Either cabinet button restarts (after a short pause so a
    // button still held from gameplay doesn't skip the screen).
    public float inputDelay = 1.2f;
    private float shownTime;

    void Update()
    {
        if (!isShown || Time.unscaledTime - shownTime < inputDelay) return;

        if (Input.GetButtonDown("Jump") || Input.GetButtonDown("Fire1"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    IEnumerator ShowRoutine()
    {
        // Unscaled time because the game is paused
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            panelGroup.alpha = k;
            if (titleText != null) titleText.localScale = Vector3.one * Mathf.Lerp(titleStartScale, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        panelGroup.alpha = 1f;
        if (titleText != null) titleText.localScale = Vector3.one;

        // Gently pulse the restart hint
        while (true)
        {
            if (restartHint != null) restartHint.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            yield return null;
        }
    }
}
