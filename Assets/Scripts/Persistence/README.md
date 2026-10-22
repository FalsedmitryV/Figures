# Persistence

Сохранение прогресса, которое переживает перезагрузку страницы в WebGL.

Два независимых вопроса, два слоя:

| Вопрос | Кто отвечает |
|---|---|
| **Куда** писать | `ISaveStore<T>` + реализации, выбирает `SaveStore.Create` |
| **Когда** писать | `SaveManager<TData>` |
| **Что** писать | твой `[Serializable]`-класс, отдаёт `Capture()` |

Всё остальное здесь про то, почему именно так.

## Быстрый старт

Через менеджер — он же берёт на себя моменты записи:

```csharp
[Serializable] public class SaveData { public int currentLevelIndex; }

public class GameSaves : SaveManager<SaveData>
{
    protected override SaveData Capture() => GameManager.Instance?.GetSaveData();
}

// загрузка на старте
if (GameSaves.Instance.TryLoad(out var data)) Apply(data);
```

Или напрямую, если момент записи ты контролируешь сам:

```csharp
ISaveStore<SaveData> store = SaveStore.Create<SaveData>("save.json", "save_json");

SaveData data = store.TryLoad(out var loaded) ? loaded : new SaveData();
data.currentLevelIndex = 3;
store.Save(data);

if (store.Exists) store.Delete();
```

`T` обязан быть `[Serializable]` с конструктором без параметров.

## WebGL: почему нельзя писать в файл

`Application.persistentDataPath` в WebGL — файловая система в памяти (IDBFS).
Unity сбрасывает её в IndexedDB только при корректном завершении игры, поэтому
при перезагрузке страницы (F5) всё, записанное `File.WriteAllText` за сессию,
теряется вместе с прогрессом.

Ловушка в том, что при разработке баг не виден: запись проходит без ошибок и
предупреждений, а в редакторе `persistentDataPath` настоящий, и всё работает.
Всплывает только на живом билде — «игрок обновил страницу и потерял прогресс».

`localStorage` пишется синхронно и доступен сразу после перезагрузки.

### Ограничения localStorage
- Данные привязаны к origin и браузеру игрока: чистка данных сайта, другой
  браузер или устройство — прогресса не будет.
- Кросс-платформенные сейвы Яндекса (облако игрока, `player.setData/getData`)
  требуют модуль Storage плагина YG2 — по умолчанию он не установлен. Ставится
  через окно YG2 (Instance → Saves), после чего добавляется третья реализация
  `ISaveStore` — остальной код не меняется.

## ISaveStore&lt;T&gt;
Интерфейс хранилища: `Exists`, `Save`, `TryLoad`, `Delete`. Отделяет «куда
пишем» от «что пишем»: игра держит ссылку только на него и о платформе не знает.

## SaveStore
Фабрика, выбирающая реализацию. **Единственное место, где должен стоять
`#if UNITY_WEBGL`** — расползание платформенных `#if` по игровому коду и есть
то, что этот слой предотвращает.

- **WebGL-билд** — `LocalStorageStore`, файл передан ему как legacy-источник:
  так не теряются сейвы, записанные до перехода на localStorage.
- **Редактор и стендэлоны** — `JsonFileStore`: читаемый файл, который удобно
  смотреть и править руками.

## JsonFileStore&lt;T&gt;
Сохранение/загрузка сериализуемого класса в JSON-файл через `JsonUtility`.

- Папка по умолчанию — `Application.persistentDataPath`.
- Для «рядом с .exe / корень проекта»:
  `new JsonFileStore<SaveData>("save.json", JsonFileStore<SaveData>.NextToExecutableDir)`.
- Ошибки ввода-вывода логируются (Warning), не кидают исключений.
- Зависимости: только UnityEngine.

## LocalStorageStore&lt;T&gt;
Та же сериализация, но значение кладётся в браузерный `localStorage` под
строковым ключом.

Вторым параметром принимает legacy-хранилище: из него **читаем**, если ключа
ещё нет, и его же удаляем в `Delete` — иначе сброс прогресса оставил бы на
диске живой старый сейв.

Зависимости: UnityEngine + `YG.Utils.LocalStorage` из плагина YG2
(`Assets/PluginYourGames`) — обёртка над `LocalStorage.jslib`. Вне WebGL она
сама уходит в `PlayerPrefs`, так что класс компилируется на всех платформах.

## SaveManager&lt;TData&gt;
Владеет **моментом** записи. Что писать, знает наследник (`Capture()`), куда —
`ISaveStore<TData>` из `CreateStore()`. Abstract generic MonoBehaviour: на сцену
вешается конкретный наследник, объект переживает загрузку сцен.

`Capture()` вернул `null` — сейв пропускается (менеджер ещё не готов).

По умолчанию `CreateStore()` зовёт `SaveStore.Create<TData>(fileName, localStorageKey)`,
то есть в WebGL сам садится на localStorage. Переопредели его, чтобы уехать в
облако или сменить папку.

### Когда пишется
- по таймеру `saveInterval` (инспектор, `0` — автосейв выключен);
- `OnApplicationFocus(false)`;
- `OnApplicationPause(true)`;
- `OnApplicationQuit()`;
- вручную — `Save()`.

В браузере игру закрывают вкладкой: `OnApplicationQuit` там не вызывается, а
`OnApplicationPause` приходит не всегда. Единственное событие, которое приходит
и при переключении вкладки, и при сворачивании — `OnApplicationFocus(false)`,
на нём и висит «финальный» сейв. Таймер на несколько секунд — чтобы не терять
прогресс при вылете.

Там же `Save()` дёргает `PlayerPrefs.Save()`: настройки (громкость, язык) живут
в PlayerPrefs отдельно от прогресса, но в WebGL попадают в IndexedDB только
после явного вызова.

### Сброс прогресса
`DeleteSave()` удаляет сейв **и глушит автосейв**. Второе обязательно: между
удалением и перезагрузкой сцены проходит ещё кадр, и таймер успел бы записать
всё заново.

`ReleaseInstance()` снимает статическую ссылку до `Destroy`. Destroy отложен до
конца кадра, и `Awake` менеджера из перезагруженной сцены успел бы увидеть живой
`Instance` и уничтожить уже себя — игра осталась бы вообще без менеджера.

Сброс делается **холодным стартом**, а не обнулением полей:

```csharp
saves?.DeleteSave();        // сначала хранилище: автосейв уже заглушен
game?.ReleaseInstance();    // ссылки снимаем синхронно...
saves?.ReleaseInstance();
Destroy(game.gameObject);   // ...объекты уничтожаем следом
Destroy(saves.gameObject);
SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
```

Состояние собирается теми же путями, что и при первом запуске, поэтому новое
сохраняемое поле не может пережить сброс по забывчивости.

Настройки (громкость, мьюты, язык) живут в PlayerPrefs отдельно и сбросу не
подлежат — вместе с ними уехал бы и выбранный игроком язык.

## Чего JsonUtility не умеет

`JsonUtility` **молча** пропускает поля, которые не умеет сериализовать: ни
ошибки, ни предупреждения — просто в JSON их нет, а после загрузки там значения
по умолчанию. Чаще всего на этом теряют:

- `Dictionary<,>` — любой словарь;
- свойства (`public int Gold { get; set; }`) — сохраняются только поля;
- `private` без `[SerializeField]`;
- многомерные и вложенные массивы (`int[,]`, `List<List<int>>`).

Лечится плоским списком `[Serializable]`-записей плюс
`ISerializationCallbackReceiver`: в игре остаётся удобный тип, в JSON уходит
список.

```csharp
[Serializable] public class Entry { public string key; public int value; }

[Serializable]
public class SaveData : ISerializationCallbackReceiver
{
    [SerializeField] private List<Entry> entriesSerialized = new();
    [NonSerialized] public Dictionary<string, int> entries = new();

    public void OnBeforeSerialize()
    {
        entriesSerialized.Clear();
        foreach (var pair in entries)
            entriesSerialized.Add(new Entry { key = pair.Key, value = pair.Value });
    }

    public void OnAfterDeserialize()
    {
        entries = new Dictionary<string, int>();
        foreach (var entry in entriesSerialized)
            entries[entry.key] = entry.value;
    }
}
```

`OnAfterDeserialize` всегда создаёт коллекцию заново — тогда старый сейв без
этого поля даёт пустую коллекцию, а не `null`.
