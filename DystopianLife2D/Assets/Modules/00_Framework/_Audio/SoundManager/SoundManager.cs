// НАЗНАЧЕНИЕ: Управляет воспроизведением всех звуковых эффектов и музыки в игре. Использует пул AudioSource для эффективного воспроизведения множества одновременных звуков.
// ОСНОВНЫЕ ЗАВИСИСИМОСТИ: AudioClip.
// ПРИМЕЧАНИЕ: Система разделена на одноразовые (one-shot) и зацикленные (looped) звуки. Для добавления новых one-shot звуков достаточно отредактировать enum SoundType и настроить список в инспекторе.
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Типы всех одноразовых звуковых эффектов в игре.
/// </summary>
public enum SoundType
{
    None,
    Explosion,
    PlayerHit,
    EnemyHit,
    PlayerReady,
    PlayerHeal,
    UpgradePurchase,
    ButtonClick,
    ButtonClickAlternative1,
    GunShot,
    EnemyGunShot,
    PlayerDeath,
    Toasty,
    BulletWizz,
    BulletRicoshet,
    ReloadGun,
    ElectroShieldHit,
    ElectroShieldUp,
    IronClawHit,
    RoboticMove,
    EnemyMeleeHit,
    MeleeMiss,
    EnemyComing,
    Resurrection
}

/// <summary>
/// Контейнер для настроек одного звукового эффекта.
/// </summary>
[System.Serializable]
public class SoundEffect
{
    public SoundType type;
    public AudioClip[] clips;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
}

public class SoundManager : MonoBehaviour
{
    #region Поля
    [BoxGroup("SETTINGS"), Tooltip("Основная громкость для всех SFX."), Range(0f, 1f), SerializeField]
    private float _sfxVolume = 1f;
    [BoxGroup("SETTINGS"), Tooltip("Начальное количество AudioSource для одновременного воспроизведения one-shot звуков"), SerializeField]
    private int _oneShotPoolSize = 15;

    [BoxGroup("SETTINGS/One-Shot Sounds"), SerializeField]
    private List<SoundEffect> _oneShotEffects = new List<SoundEffect>();

    [BoxGroup("SETTINGS/Looped Sounds"), SerializeField]
    private AudioClip[] _footstepSounds;
    [BoxGroup("SETTINGS/Looped Sounds"), SerializeField]
    private AudioClip[] _cricketSounds;
    [BoxGroup("SETTINGS/Looped Sounds"), SerializeField]
    private AudioClip[] _birdSounds;
    [BoxGroup("SETTINGS/Looped Sounds"), SerializeField]
    private AudioClip[] _owlSounds;

    [BoxGroup("SETTINGS/Looped Sounds Volume"), Range(0f, 2f), SerializeField]
    private float _footstepsVolume = 1f;
    [BoxGroup("SETTINGS/Looped Sounds Volume"), Range(0f, 2f), SerializeField]
    private float _natureVolume = 1f;

    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug;
    private static SoundManager _instance;
    private List<AudioSource> _oneShotSources;
    private AudioSource _loopFootsteps;
    private AudioSource _loopCrickets;
    private AudioSource _loopBirds;
    private AudioSource _loopOwls;
    #endregion Поля

    #region Свойства
    /// <summary>
    /// Предоставляет глобальный доступ к экземпляру SoundManager.
    /// </summary>
    public static SoundManager Instance { get => _instance; }
    #endregion Свойства

    #region Методы UNITY
    private void Awake()
    {
        if (_instance != null) { DebugUtils.LogInstanceAlreadyExists(this, _instance); Destroy(transform.root.gameObject); return; }

        _instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        InitAudioSources();
    }
    #endregion Методы UNITY

    #region Публичные методы
    /// <summary>
    /// Воспроизводит случайный клип для указанного типа звукового эффекта.
    /// </summary>
    /// <param name="type">Тип звука для воспроизведения из enum SoundType.</param>
    public void PlayOneShot(SoundType type)
    {
        if (type == SoundType.None) return;

        SoundEffect effect = _oneShotEffects.FirstOrDefault(e => e.type == type);

        if (effect == null || effect.clips.Length == 0)
        {
            ColoredDebug.CLog(gameObject, "<color=orange>SoundManager:</color> Звуковой эффект для типа <color=yellow>{0}</color> не найден или не имеет клипов.", _ColoredDebug, type);
            return;
        }

        AudioClip clip = effect.clips[Random.Range(0, effect.clips.Length)];
        AudioSource source = GetAvailableOneShotSource();

        if (source != null)
        {
            source.PlayOneShot(clip, _sfxVolume * effect.volumeMultiplier);
            ColoredDebug.CLog(gameObject, "<color=cyan>SoundManager:</color> Воспроизведение one-shot: <color=lime>{0}</color>.", _ColoredDebug, type);
        }
    }

    /// <summary>
    /// Запускает зацикленное воспроизведение шагов.
    /// </summary>
    public void StartFootsteps() => PlayRandomLoop(_loopFootsteps, _footstepSounds, _footstepsVolume);
    /// <summary>
    /// Останавливает воспроизведение шагов.
    /// </summary>
    public void StopFootsteps() => _loopFootsteps.Stop();

    /// <summary>
    /// Запускает зацикленное воспроизведение сверчков.
    /// </summary>
    public void StartCrickets() => PlayRandomLoop(_loopCrickets, _cricketSounds, _natureVolume);
    /// <summary>
    /// Останавливает воспроизведение сверчков.
    /// </summary>
    public void StopCrickets() => _loopCrickets.Stop();

    /// <summary>
    /// Запускает зацикленное воспроизведение птиц.
    /// </summary>
    public void StartBirds() => PlayRandomLoop(_loopBirds, _birdSounds, _natureVolume);
    /// <summary>
    /// Останавливает воспроизведение птиц.
    /// </summary>
    public void StopBirds() => _loopBirds.Stop();

    /// <summary>
    /// Запускает зацикленное воспроизведение сов.
    /// </summary>
    public void StartOwls() => PlayRandomLoop(_loopOwls, _owlSounds, _natureVolume);
    /// <summary>
    /// Останавливает воспроизведение сов.
    /// </summary>
    public void StopOwls() => _loopOwls.Stop();
    #endregion Публичные методы

    #region Личные методы
    private void InitAudioSources()
    {
        _oneShotSources = new List<AudioSource>();
        for (int i = 0; i < _oneShotPoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            _oneShotSources.Add(source);
        }

        _loopFootsteps = gameObject.AddComponent<AudioSource>();
        _loopFootsteps.loop = true;

        _loopCrickets = gameObject.AddComponent<AudioSource>();
        _loopCrickets.loop = true;

        _loopBirds = gameObject.AddComponent<AudioSource>();
        _loopBirds.loop = true;

        _loopOwls = gameObject.AddComponent<AudioSource>();
        _loopOwls.loop = true;

        ColoredDebug.CLog(gameObject, "<color=cyan>SoundManager:</color> Аудио источники инициализированы. Размер пула one-shot: <color=yellow>{0}</color>.", _ColoredDebug, _oneShotPoolSize);
    }

    private AudioSource GetAvailableOneShotSource()
    {
        foreach (var source in _oneShotSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        ColoredDebug.CLog(gameObject, "<color=orange>SoundManager:</color> Пул one-shot источников звука исчерпан! Расширяем динамически. Рассмотрите увеличение _oneShotPoolSize.", _ColoredDebug);
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.loop = false;
        newSource.playOnAwake = false;
        _oneShotSources.Add(newSource);

        return newSource;
    }

    private void PlayRandomLoop(AudioSource source, AudioClip[] clips, float categoryVolume)
    {
        if (source == null)
        {
            ColoredDebug.CLog(gameObject, "<color=red>SoundManager:</color> AudioSource для зацикленного звука не найден!", _ColoredDebug);
            return;
        }

        if (clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (source.isPlaying && source.clip == clip) return;

        source.clip = clip;
        source.volume = _sfxVolume * categoryVolume;
        source.Play();
        ColoredDebug.CLog(gameObject, "<color=cyan>SoundManager:</color> Запуск зацикленного звука: <color=lime>{0}</color>.", _ColoredDebug, clip.name);
    }
    #endregion Личные методы
}