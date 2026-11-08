using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// One per scene. Holds what makes this level different (depth pressure, music, next scene).
public class LevelSettings : MonoBehaviour
{
    public static LevelSettings Instance { get; private set; }

    [Header("Level flow")]
    public int levelNumber = 1;
    public string nextSceneName = "Level2";   // Must be in File > Build Profiles > Scene List
    public float levelCompleteDelay = 2.5f;   // Lets the "level clear" sound play before loading

    [Tooltip("Level 2+: start already underwater and diving (no boat, no jump needed).")]
    public bool startUnderwater = false;
    [Tooltip("Off = collecting 10 pearls does NOT summon a boss (Level 2 boss is not made yet).")]
    public bool hasBoss = true;

    [Header("Pressure (oxygen drain)")]
    [Tooltip("1 = normal. Level 2 is deeper, e.g. 1.6 = drains 60% faster.")]
    public float oxygenDrainMultiplier = 1f;
    [Tooltip("Extra multiplier added per minute of diving. Pressure keeps building the longer you stay down.")]
    public float pressureRampPerMinute = 0f;
    public float maxDrainMultiplier = 3f;

    [Header("Audio")]
    public AudioClip levelMusic;
    [Tooltip("Level 1: the ocean ambience starts when the diver jumps into the water.")]
    public bool musicStartsOnDive = true;

    public void StartDiveMusic()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(levelMusic);
    }

    [Header("Dark level look (optional)")]
    public bool darkLevel = false;
    public Color backgroundColor = new Color(0.01f, 0.02f, 0.06f);
    public float directionalLightIntensity = 0.05f;

    private bool completing = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (AudioManager.Instance != null)
        {
            // On the boat it's quiet; the ocean sound starts when we hit the water (PlayerMovement)
            if (musicStartsOnDive && !startUnderwater) AudioManager.Instance.StopMusic();
            else AudioManager.Instance.PlayMusic(levelMusic);
        }

        if (darkLevel)
        {
            if (Camera.main != null) Camera.main.backgroundColor = backgroundColor;
            RenderSettings.ambientLight = backgroundColor;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) l.intensity = directionalLightIntensity;
            }
        }
    }

    public float GetDrainMultiplier(float secondsDiving)
    {
        float m = oxygenDrainMultiplier + pressureRampPerMinute * (secondsDiving / 60f);
        return Mathf.Min(m, maxDrainMultiplier);
    }

    public void CompleteLevel()
    {
        if (completing) return;
        completing = true;
        StartCoroutine(CompleteRoutine());
    }

    IEnumerator CompleteRoutine()
    {
        AudioManager.PlaySfx(Sfx.LevelComplete);
        AudioManager.SetBreathing(false);

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.ShowBanner(string.IsNullOrEmpty(nextSceneName) ? "YOU WIN!" : "LEVEL " + (levelNumber + 1), levelCompleteDelay);

        // Realtime: still advances even if something paused the game (timeScale 0)
        yield return new WaitForSecondsRealtime(levelCompleteDelay);
        Time.timeScale = 1f;

        // No next scene yet (e.g. final level before menus exist): replay the current one
        string target = string.IsNullOrEmpty(nextSceneName) ? SceneManager.GetActiveScene().name : nextSceneName;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError("[LevelSettings] Scene '" + target + "' is not in File > Build Profiles > Scene List");
            target = SceneManager.GetActiveScene().name;
        }
        Debug.Log("[LevelSettings] Loading " + target);
        SceneManager.LoadScene(target);
    }

    public static bool IsCompleting => Instance != null && Instance.completing;

    // Static entry point so BossAI works even if a scene has no LevelSettings
    public static void Complete()
    {
        if (Instance != null) Instance.CompleteLevel();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
