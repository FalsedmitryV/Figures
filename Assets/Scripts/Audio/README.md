# Audio

## AudioManager&lt;TId&gt;
Ядро аудио: пул `AudioSource` для SFX (round-robin), два источника музыки с
плавным crossfade, мастер-громкости и **раздельные мьюты** SFX/Music в
PlayerPrefs. Игровых зависимостей нет — подписки на события и логику
«сцена → музыка» добавляет наследник.

`TId` — тип идентификатора звука, обычно свой enum.

### Использование
```csharp
public enum SoundId { ButtonClick, Hit, Purchase }

public class GameAudio : AudioManager<SoundId>
{
    public static GameAudio Current { get; private set; }

    [SerializeField] private SoundCatalogSO catalog;   // свой ScriptableObject
    [SerializeField] private GameEventsSO events;

    protected override SoundEntry Resolve(SoundId id) => catalog?.Get(id);

    protected override void Awake()
    {
        base.Awake();
        // база уничтожает дубликат и выходит из Awake, не выставив Instance —
        // без этой проверки Current указывал бы на уже мёртвый объект
        if (Instance != this) return;
        Current = this;
    }

    protected override void OnDestroy()
    {
        if (Current == this) Current = null;
        base.OnDestroy();
    }

    private void OnEnable()  => events.OnHit += _ => Play(SoundId.Hit);
    private void Start()     => PlayMusic(catalog.battleMusic);
}
```

Это abstract generic MonoBehaviour — на сцену вешается КОНКРЕТНЫЙ наследник.
Объект переживает загрузку сцен (`DontDestroyOnLoad`).

`Awake`, `OnDestroy` и `Update` у базы `protected virtual`: если наследнику нужен
свой — `override` с вызовом `base.…()`. Особенно `Update`: без него не сработает
обрезка звуков по `maxDuration`.

### Воспроизведение
- `Play(id)` / `Play(id, worldPosition)` — pitch случайный из диапазона записи.
- `Play(id, pitch)` — явная высота: мелодия или нарастающая серия из одного клипа.
- `PlayMusic(clip)` — crossfade на новую музыку, `null` — выключить.

### Громкость и мьют
`SfxVolume` / `MusicVolume` (0..1) и `SfxMuted` / `MusicMuted` — отдельные
свойства, всё пишется в PlayerPrefs сразу при установке. Мьют хранится **отдельно
от громкости** намеренно: гасить через `volume = 0` значило бы затереть положение
ползунка, и после включения его пришлось бы выставлять заново.

Ключи PlayerPrefs (`audio.sfx`, `audio.music`, `audio.sfx.mute`,
`audio.music.mute`) переопределяются `protected virtual`-свойствами, если нужно
развести несколько проектов в одном билде.

> В WebGL PlayerPrefs уходят в IndexedDB только после явного `PlayerPrefs.Save()` —
> его дёргает `SaveManager<TData>` вместе с записью прогресса.

### SoundEntry
Одна запись каталога: `clip` + базовая `volume` + диапазон `pitchMin/pitchMax`
для вариативности повторяющихся звуков. `PickPitch()` берёт случайное значение
из диапазона.

`maxDuration` (сек, реальное время) — сколько звучать; `0` — клип целиком. По
истечении звук не обрывается, а затухает за `cutoffFadeSeconds` (поле менеджера,
0.08 по умолчанию) — жёсткий `Stop` щёлкает. Так один длинный клип годится и на
короткие щелчки, не нарезая аудиофайлы.

Маппинг `TId → SoundEntry` делает каталог в твоём проекте
(обычно `ScriptableObject` со `List<Entry>` и ленивым `Dictionary`).

### Зависимости
- Только UnityEngine.
