using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;

    [Header("Wandering Music (Random)")]
    public List<AudioClip> wanderingAroundMusic;

    [Header("Battle Music (Random)")]
    public List<AudioClip> battleMusic;

    [Header("Menu Music (Random)")]
    public List<AudioClip> menuMusic;

    [Header("Death Music")]
    public AudioClip deathMusic;

    [Header("Shop Music")]
    public AudioClip shopMusic;

    [Header("EndGame Music")]
    public AudioClip endGameMusic;

    [Header("Volume & Fade Settings")]
    [Range(0f, 1f)] public float maxVolume = 1f;
    public float fadeDuration = 1.5f;

    [Header("Category Volume Multipliers")]
    [Range(0f, 2f)] public float wanderingAroundMusicVolume = 1f;
    [Range(0f, 2f)] public float battleMusicVolume = 1f;
    [Range(0f, 2f)] public float menuMusicVolume = 1f;
    [Range(0f, 2f)] public float deathMusicVolume = 1f;
    [Range(0f, 2f)] public float shopMusicVolume = 1f;

    private Coroutine currentFade;

    private List<AudioClip> availableWanderingTracks = new List<AudioClip>();
    private List<AudioClip> availableBattleTracks = new List<AudioClip>();
    private List<AudioClip> availableMenuTracks = new List<AudioClip>();

    private float wanderingAroundTimePosition = 0f;
    private AudioClip lastWanderingAroundClip = null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        RefillPlaylist(wanderingAroundMusic, availableWanderingTracks);
        RefillPlaylist(battleMusic, availableBattleTracks);
        RefillPlaylist(menuMusic, availableMenuTracks);
    }

    public void ResetPlayedTracks()
    {
        Debug.Log("Сбрасываем и перемешиваем плейлисты...");
        RefillPlaylist(wanderingAroundMusic, availableWanderingTracks);
        RefillPlaylist(battleMusic, availableBattleTracks);
        RefillPlaylist(menuMusic, availableMenuTracks);
    }

    public void PlayWanderingAroundMusic(bool forceNew = false)
    {
        if (wanderingAroundMusic.Count == 0) return;

        if (lastWanderingAroundClip != null && !forceNew)
        {
            PlayMusic(lastWanderingAroundClip, false, wanderingAroundMusicVolume, wanderingAroundTimePosition);
        }
        else
        {
            lastWanderingAroundClip = null;
            wanderingAroundTimePosition = 0f;

            if (availableWanderingTracks.Count == 0)
            {
                RefillPlaylist(wanderingAroundMusic, availableWanderingTracks);
            }

            int lastIndex = availableWanderingTracks.Count - 1;
            AudioClip clip = availableWanderingTracks[lastIndex];

            availableWanderingTracks.RemoveAt(lastIndex);

            PlayMusic(clip, false, wanderingAroundMusicVolume);
        }
    }

    public void PlayBattleMusic()
    {
        if (battleMusic.Count == 0) return;

        if (availableBattleTracks.Count == 0)
        {
            RefillPlaylist(battleMusic, availableBattleTracks);
        }

        int lastIndex = availableBattleTracks.Count - 1;
        AudioClip clip = availableBattleTracks[lastIndex];

        availableBattleTracks.RemoveAt(lastIndex);

        PlayMusic(clip, false, battleMusicVolume);
    }

    public void PlayMenuMusic()
    {
        if (menuMusic.Count == 0) return;

        if (availableMenuTracks.Count == 0)
        {
            RefillPlaylist(menuMusic, availableMenuTracks);
        }

        int lastIndex = availableMenuTracks.Count - 1;
        AudioClip clip = availableMenuTracks[lastIndex];

        availableMenuTracks.RemoveAt(lastIndex);

        PlayMusic(clip, true, menuMusicVolume);
    }

    public void PlayEndGameMusic() => PlayMusic(endGameMusic, true, menuMusicVolume);
    public void PlayDeathMusic() => PlayMusic(deathMusic, false, deathMusicVolume);
    public void PlayShopMusic() => PlayMusic(shopMusic, true, shopMusicVolume);
    public void StopMusic()
    {
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeOutAndStop());
    }

    private void PlayMusic(AudioClip clip, bool loop, float categoryVolume, float startTime = 0f)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeMusic(clip, loop, categoryVolume, startTime));
    }

    private void RefillPlaylist(List<AudioClip> source, List<AudioClip> destination)
    {
        destination.Clear();
        destination.AddRange(source);

        System.Random rng = new System.Random();
        int n = destination.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            AudioClip value = destination[k];
            destination[k] = destination[n];
            destination[n] = value;
        }
    }

    private IEnumerator FadeMusic(AudioClip newClip, bool loop, float categoryVolume, float startTime)
    {
        if (musicSource.isPlaying)
        {
            if (wanderingAroundMusic.Contains(musicSource.clip))
            {
                lastWanderingAroundClip = musicSource.clip;
                wanderingAroundTimePosition = musicSource.time;
            }

            float startVolume = musicSource.volume;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.volume = 0;
        musicSource.clip = newClip;
        musicSource.loop = loop;
        musicSource.time = startTime;

        if (newClip == lastWanderingAroundClip)
        {
            lastWanderingAroundClip = null;
        }

        musicSource.Play();

        float targetVolume = maxVolume * categoryVolume;
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(0, targetVolume, t / fadeDuration);
            yield return null;
        }
        musicSource.volume = targetVolume;
    }

    private IEnumerator FadeOutAndStop()
    {
        if (musicSource.isPlaying && wanderingAroundMusic.Contains(musicSource.clip))
        {
            lastWanderingAroundClip = musicSource.clip;
            wanderingAroundTimePosition = musicSource.time;
        }

        float startVolume = musicSource.volume;
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
            yield return null;
        }
        musicSource.volume = 0;
        musicSource.Stop();
    }
}
