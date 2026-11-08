using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

// Every sound effect in the game. Add new ones at the END so Inspector assignments don't shift.
public enum Sfx
{
    HarpoonThrow,
    PearlFly,
    PearlCollect,
    OxygenPickup,
    Dash,
    Bubble,
    Splash,
    BossWarning,
    BossHit,
    LevelComplete,
    EnemyExplode,
    PlayerHurt,
    Death,
    ChaserLunge
}

[System.Serializable]
public class SoundDef
{
    public Sfx id;
    public AudioClip[] clips;             // More than one = random pick (less repetitive)
    [Range(0f, 2f)] public float volume = 1f;
    [Range(0.3f, 2f)] public float pitchMin = 0.92f;
    [Range(0.3f, 2f)] public float pitchMax = 1.0f;
    public float cooldown = 0.05f;        // Blocks the same sound from stacking in one frame
    public bool duckMusic = false;        // Big moments (boss, level clear) push the music down
    [Tooltip("Skip this many seconds at the start of the clip (silence / slow attack).")]
    public float startTime = 0f;
    [Tooltip("Cut the sound after this many seconds, with a short fade. 0 = play the whole clip.")]
    public float maxDuration = 0f;
}

// One persistent manager for all audio. Put it in the first scene; it survives scene loads.
// Call it from anywhere with AudioManager.PlaySfx(Sfx.Dash);
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer (optional)")]
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup musicGroup;

    [Header("Sounds")]
    public SoundDef[] sounds;

    [Header("Volumes")]
    [Range(0f, 1f)] public float masterSfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float duckedMusicVolume = 0.15f;
    public float duckTime = 1.2f;

    [Header("Breathing / bubbles loop")]
    public AudioClip breathingLoop;
    [Range(0f, 1f)] public float breathingVolume = 0.35f;

    public int voiceCount = 12;

    private readonly Dictionary<Sfx, SoundDef> lookup = new Dictionary<Sfx, SoundDef>();
    private readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
    private AudioSource[] voices;
    private Coroutine[] voiceFades;
    private int nextVoice = 0;
    private AudioSource musicSource;
    private AudioSource breathSource;
    private float duckTimer = 0f;
    private Coroutine musicFade;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // The Level 2 scene may contain a copy; keep the original
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (SoundDef s in sounds) lookup[s.id] = s;

        voices = new AudioSource[voiceCount];
        voiceFades = new Coroutine[voiceCount];
        for (int i = 0; i < voiceCount; i++) voices[i] = CreateSource(sfxGroup, false);

        musicSource = CreateSource(musicGroup, true);
        musicSource.volume = 0f;

        breathSource = CreateSource(sfxGroup, true);
        breathSource.clip = breathingLoop;
    }

    AudioSource CreateSource(AudioMixerGroup group, bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f; // 2D: arcade cabinets sound best "in your face"
        src.outputAudioMixerGroup = group;
        return src;
    }

    void Update()
    {
        // Music ducking: dip under big SFX, then come back smoothly
        if (musicFade == null && musicSource.clip != null)
        {
            if (duckTimer > 0f) duckTimer -= Time.unscaledDeltaTime;
            float target = duckTimer > 0f ? duckedMusicVolume : musicVolume;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 1.5f);
        }
    }

    // ---------- Static helpers: safe to call even if there is no AudioManager in the scene ----------
    public static void PlaySfx(Sfx id, float pitchOffset = 0f)
    {
        if (Instance != null) Instance.Play(id, pitchOffset);
    }

    public static void SetBreathing(bool on, float pitch = 1f)
    {
        if (Instance != null) Instance.Breathing(on, pitch);
    }

    // ---------- SFX ----------
    public void Play(Sfx id, float pitchOffset = 0f)
    {
        if (!lookup.TryGetValue(id, out SoundDef s) || s.clips == null || s.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < s.cooldown) return;
        lastPlayed[id] = now;

        AudioClip clip = s.clips[Random.Range(0, s.clips.Length)];
        if (clip == null) return;

        int idx = nextVoice;
        AudioSource src = voices[idx];
        nextVoice = (nextVoice + 1) % voices.Length;

        // Each sound gets its own voice so it can start mid-clip and be cut short
        if (voiceFades[idx] != null) { StopCoroutine(voiceFades[idx]); voiceFades[idx] = null; }
        src.Stop();
        src.clip = clip;
        src.pitch = Random.Range(s.pitchMin, s.pitchMax) + pitchOffset;
        src.volume = Mathf.Clamp01(s.volume * masterSfxVolume);
        src.time = Mathf.Clamp(s.startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        src.Play();

        float playLength = clip.length - src.time;
        if (s.maxDuration > 0f && s.maxDuration < playLength)
        {
            voiceFades[idx] = StartCoroutine(CutAfter(src, s.maxDuration / Mathf.Max(src.pitch, 0.1f)));
            playLength = s.maxDuration;
        }

        if (s.duckMusic) duckTimer = Mathf.Max(duckTimer, Mathf.Min(playLength, duckTime * 2f));
    }

    IEnumerator CutAfter(AudioSource src, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        float start = src.volume;
        for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(start, 0f, t / 0.08f);
            yield return null;
        }
        src.Stop();
        src.volume = start;
    }

    // ---------- Breathing loop (diver regulator / bubbles) ----------
    public void Breathing(bool on, float pitch)
    {
        if (breathSource.clip == null) return;
        breathSource.pitch = pitch;
        breathSource.volume = breathingVolume * masterSfxVolume;
        if (on && !breathSource.isPlaying) breathSource.Play();
        else if (!on && breathSource.isPlaying) breathSource.Stop();
    }

    // ---------- Music ----------
    public void StopMusic(float fadeTime = 1f)
    {
        if (musicSource.clip == null) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeOutMusic(fadeTime));
    }

    IEnumerator FadeOutMusic(float fadeTime)
    {
        float start = musicSource.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(start, 0f, t / fadeTime);
            yield return null;
        }
        musicSource.Stop();
        musicSource.clip = null;
        musicFade = null;
    }

    public void PlayMusic(AudioClip clip, float fadeTime = 1.5f)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(SwapMusic(clip, fadeTime));
    }

    IEnumerator SwapMusic(AudioClip clip, float fadeTime)
    {
        float half = fadeTime * 0.5f;
        float startVol = musicSource.volume;
        for (float t = 0f; t < half && musicSource.isPlaying; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / half);
            yield return null;
        }

        musicSource.clip = clip;
        musicSource.Play();
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / half);
            yield return null;
        }
        musicSource.volume = musicVolume;
        musicFade = null;
    }
}
