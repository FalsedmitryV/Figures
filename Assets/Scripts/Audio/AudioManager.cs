using System.Collections;
using UnityEngine;

/// <summary>
/// Универсальное ядро аудио-менеджера: пул AudioSource для SFX (round-robin),
/// двойной источник музыки с плавным crossfade, мастер-громкости SFX/Music с
/// сохранением в PlayerPrefs. НЕ завязан на игровые события — подписки на события
/// и логику «сцена → музыка» добавляй в наследнике.
///
/// <typeparamref name="TId"/> — тип идентификатора звука (обычно ваш enum).
/// Наследник реализует <see cref="Resolve"/> (id → <see cref="SoundEntry"/>),
/// например из своего ScriptableObject-каталога, и вызывает <see cref="Play"/>.
///
/// Использование:
/// <code>
/// public class GameAudio : AudioManager&lt;SoundId&gt;
/// {
///     [SerializeField] private SoundCatalog catalog;
///     protected override SoundEntry Resolve(SoundId id) =&gt; catalog.Get(id);
///     private void OnEnable()  =&gt; Enemy.OnDied += _ =&gt; Play(SoundId.EnemyDeath);
/// }
/// </code>
///
/// Это abstract generic MonoBehaviour — на сцену вешается КОНКРЕТНЫЙ наследник.
/// Зависимости: только UnityEngine.
/// </summary>
public abstract class AudioManager<TId> : MonoBehaviour
{
    public static AudioManager<TId> Instance { get; private set; }

    [Header("SFX пул")]
    [SerializeField] private int sfxPoolSize = 12;
    [Tooltip("Затухание при обрезке звука по SoundEntry.maxDuration, сек.")]
    [SerializeField] private float cutoffFadeSeconds = 0.08f;

    [Header("Музыка")]
    [SerializeField] private float crossfadeSeconds = 1f;

    [Header("Дефолтные громкости (если PlayerPrefs пуст)")]
    [Range(0f, 1f)] [SerializeField] private float defaultSfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultMusicVolume = 0.6f;

    /// <summary>Ключи PlayerPrefs. Переопредели в наследнике, если нужно развести проекты.</summary>
    protected virtual string SfxPrefKey   => "audio.sfx";
    protected virtual string MusicPrefKey => "audio.music";
    protected virtual string SfxMutePrefKey   => "audio.sfx.mute";
    protected virtual string MusicMutePrefKey => "audio.music.mute";

    /// <summary>Маппинг id → запись каталога. Вернуть null, если звук не настроен.</summary>
    protected abstract SoundEntry Resolve(TId id);

    private AudioSource[] _sfxPool;
    private float[] _sfxCutoffAt;
    private float[] _sfxBaseVolume;
    private int _sfxIndex;

    private AudioSource _musicA, _musicB, _activeMusic;
    private Coroutine   _crossfade;

    private float _sfxVolume, _musicVolume;
    private bool _sfxMuted, _musicMuted;

    public float SfxVolume
    {
        get => _sfxVolume;
        set { _sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxPrefKey, _sfxVolume); }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicPrefKey, _musicVolume);
            ApplyMusicVolume();
        }
    }

    /// <summary>
    /// Отключение звука хранится отдельно от громкости: гасить через volume = 0
    /// значило бы затереть положение ползунка, и после включения его пришлось бы
    /// выставлять заново.
    /// </summary>
    public bool SfxMuted
    {
        get => _sfxMuted;
        set { _sfxMuted = value; PlayerPrefs.SetInt(SfxMutePrefKey, value ? 1 : 0); }
    }

    public bool MusicMuted
    {
        get => _musicMuted;
        set
        {
            _musicMuted = value;
            PlayerPrefs.SetInt(MusicMutePrefKey, value ? 1 : 0);
            ApplyMusicVolume();
        }
    }

    /// <summary>Громкость, которая реально уходит в источники.</summary>
    protected float EffectiveSfxVolume => _sfxMuted ? 0f : _sfxVolume;
    protected float EffectiveMusicVolume => _musicMuted ? 0f : _musicVolume;

    private void ApplyMusicVolume()
    {
        // во время crossfade громкость ведёт корутина — не мешаем ей
        if (_crossfade == null && _activeMusic != null) _activeMusic.volume = EffectiveMusicVolume;
    }

    // =========================================================================

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxVolume   = PlayerPrefs.GetFloat(SfxPrefKey,   defaultSfxVolume);
        _musicVolume = PlayerPrefs.GetFloat(MusicPrefKey, defaultMusicVolume);
        _sfxMuted    = PlayerPrefs.GetInt(SfxMutePrefKey,   0) == 1;
        _musicMuted  = PlayerPrefs.GetInt(MusicMutePrefKey, 0) == 1;

        BuildSfxPool();
        BuildMusicSources();
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Обрезка по <see cref="SoundEntry.maxDuration"/>: не жёсткий Stop (щёлкает),
    /// а короткое затухание. Наследник со своим Update обязан вызвать base.Update().
    /// </summary>
    protected virtual void Update()
    {
        if (_sfxPool == null) return;

        float now = Time.unscaledTime;
        float fade = Mathf.Max(0.01f, cutoffFadeSeconds);

        for (int i = 0; i < _sfxPool.Length; i++)
        {
            if (_sfxCutoffAt[i] < 0f || now < _sfxCutoffAt[i]) continue;

            var src = _sfxPool[i];
            float k = 1f - (now - _sfxCutoffAt[i]) / fade;

            if (k <= 0f || !src.isPlaying)
            {
                src.Stop();
                _sfxCutoffAt[i] = -1f;
                continue;
            }

            src.volume = _sfxBaseVolume[i] * k;
        }
    }

    // =========================================================================
    // ПУБЛИЧНОЕ API
    // =========================================================================

    /// <summary>Проиграть SFX (2D). worldPosition — опционально, для будущего spatial.</summary>
    public void Play(TId id, Vector3? worldPosition = null) => PlaySfx(id, null, worldPosition);

    /// <summary>Проиграть SFX с явной высотой вместо случайной из записи (мелодия из одного клипа).</summary>
    public void Play(TId id, float pitch, Vector3? worldPosition = null) => PlaySfx(id, pitch, worldPosition);

    /// <summary>Сменить музыку с плавным crossfade. null — выключить музыку.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (_activeMusic != null && _activeMusic.clip == clip) return;
        if (_crossfade != null) StopCoroutine(_crossfade);
        _crossfade = StartCoroutine(CrossfadeRoutine(clip));
    }

    // =========================================================================
    // ВНУТРЕННЕЕ
    // =========================================================================

    private void PlaySfx(TId id, float? pitch, Vector3? worldPosition)
    {
        var entry = Resolve(id);
        if (entry == null || entry.clip == null || _sfxPool == null) return;
        if (_sfxMuted) return;

        int index = _sfxIndex;
        var src = _sfxPool[index];
        _sfxIndex = (_sfxIndex + 1) % _sfxPool.Length;

        if (worldPosition.HasValue) src.transform.position = worldPosition.Value;

        src.clip   = entry.clip;
        src.volume = entry.volume * EffectiveSfxVolume;
        src.pitch  = pitch ?? entry.PickPitch();
        src.Play();

        _sfxBaseVolume[index] = src.volume;
        _sfxCutoffAt[index] = entry.maxDuration > 0f ? Time.unscaledTime + entry.maxDuration : -1f;
    }

    private void BuildSfxPool()
    {
        _sfxPool = new AudioSource[Mathf.Max(1, sfxPoolSize)];
        _sfxCutoffAt = new float[_sfxPool.Length];
        _sfxBaseVolume = new float[_sfxPool.Length];
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            var go = new GameObject($"SfxSource_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            _sfxPool[i] = src;
            _sfxCutoffAt[i] = -1f;
        }
    }

    private void BuildMusicSources()
    {
        _musicA = CreateMusicSource("MusicA");
        _musicB = CreateMusicSource("MusicB");
        _activeMusic = _musicA;
    }

    private AudioSource CreateMusicSource(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        return src;
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        var from = _activeMusic;
        var to   = from == _musicA ? _musicB : _musicA;

        to.clip = newClip;
        to.volume = 0f;
        if (newClip != null) to.Play();

        float duration = Mathf.Max(0.01f, crossfadeSeconds);
        float t = 0f;
        float startFrom = from != null ? from.volume : 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (from != null)    from.volume = Mathf.Lerp(startFrom, 0f, k);
            if (newClip != null) to.volume   = Mathf.Lerp(0f, EffectiveMusicVolume, k);
            yield return null;
        }

        if (from != null) { from.Stop(); from.volume = 0f; from.clip = null; }
        if (newClip != null) to.volume = EffectiveMusicVolume;

        _activeMusic = to;
        _crossfade = null;
    }
}
