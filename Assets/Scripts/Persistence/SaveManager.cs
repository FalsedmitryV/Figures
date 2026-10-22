using UnityEngine;

/// <summary>
/// Владеет МОМЕНТОМ записи. Что именно писать, знает наследник
/// (<see cref="Capture"/>), а куда — <see cref="ISaveStore{TData}"/>. Поэтому
/// переезд на облако YG2 не трогает ни игру, ни этот класс: меняется только
/// реализация хранилища в <see cref="CreateStore"/>.
///
/// Это abstract generic MonoBehaviour — на сцену вешается КОНКРЕТНЫЙ наследник:
/// <code>
/// public class GameSaves : SaveManager&lt;SaveData&gt;
/// {
///     protected override SaveData Capture() =&gt; GameManager.Instance?.GetSaveData();
/// }
/// </code>
///
/// Зависимости: UnityEngine, <see cref="ISaveStore{T}"/>, <see cref="SaveStore"/>.
/// </summary>
public abstract class SaveManager<TData> : MonoBehaviour where TData : class, new()
{
    public static SaveManager<TData> Instance { get; private set; }

    [Header("Хранилище")]
    [Tooltip("Имя файла в persistentDataPath — редактор и стендэлоны.")]
    [SerializeField] private string fileName = "save.json";

    [Tooltip("Ключ localStorage — WebGL-билд. Пусто — выводится из имени файла.")]
    [SerializeField] private string localStorageKey = "save_json";

    [Tooltip("Период автосейва, сек. 0 и меньше — автосейв выключен, пишем только по событиям приложения.")]
    [SerializeField] private float saveInterval = 5f;

    private ISaveStore<TData> _store;
    private float _nextSaveTime;

    /// <summary>После сброса прогресса писать нечего — иначе автосейв воскресит удалённый файл.</summary>
    private bool _suspended;

    /// <summary>Собрать текущее состояние игры. null — писать нечего, сейв пропускается.</summary>
    protected abstract TData Capture();

    /// <summary>
    /// Куда пишем. По умолчанию — <see cref="SaveStore"/>, то есть в WebGL-билде
    /// localStorage, иначе файл. Напрямую <see cref="JsonFileStore{T}"/> здесь брать
    /// нельзя: в WebGL он не переживает перезагрузку страницы (см. README).
    /// Переопредели, чтобы уехать в облако или сменить папку.
    /// </summary>
    protected virtual ISaveStore<TData> CreateStore() =>
        SaveStore.Create<TData>(fileName, ResolveLocalStorageKey());

    /// <summary>Ключ не задан — выводим из имени файла: save.json → save_json.</summary>
    private string ResolveLocalStorageKey() =>
        string.IsNullOrEmpty(localStorageKey) ? fileName.Replace('.', '_') : localStorageKey;

    /// <summary>Есть ли сохранение.</summary>
    public bool Exists => _store != null && _store.Exists;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _store = CreateStore();
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_suspended || saveInterval <= 0f || Time.unscaledTime < _nextSaveTime) return;

        _nextSaveTime = Time.unscaledTime + saveInterval;
        Save();
    }

    // ===== Моменты записи =====
    // В браузере игру закрывают вкладкой: OnApplicationQuit там не вызывается,
    // а OnApplicationPause приходит не всегда. Потеря фокуса — единственное
    // событие, которое приходит и при переключении вкладки, и при сворачивании,
    // поэтому основной «финальный» сейв висит на нём.

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();

    /// <summary>Записать прогресс немедленно.</summary>
    public void Save()
    {
        if (_suspended || _store == null) return;

        var data = Capture();
        if (data == null) return;

        _store.Save(data);

        // настройки (громкость, язык) живут в PlayerPrefs отдельно от прогресса,
        // но в WebGL они попадают в IndexedDB только после явного Save —
        // сбрасываем их тем же тактом, что и прогресс
        PlayerPrefs.Save();
    }

    public bool TryLoad(out TData data)
    {
        data = null;
        return _store != null && _store.TryLoad(out data);
    }

    /// <summary>
    /// Удалить сохранение и заглушить автосейв. Второе обязательно: между
    /// удалением и перезагрузкой сцены проходит ещё кадр, и таймер успел бы
    /// записать файл заново.
    /// </summary>
    public void DeleteSave()
    {
        _suspended = true;
        _store?.Delete();
    }

    /// <summary>
    /// Снять статическую ссылку до уничтожения объекта. Destroy отложен до конца
    /// кадра, и Awake менеджера из перезагруженной сцены успел бы увидеть живой
    /// Instance и уничтожить уже себя — игра осталась бы вообще без менеджера.
    /// </summary>
    public void ReleaseInstance()
    {
        if (Instance == this) Instance = null;
    }
}
