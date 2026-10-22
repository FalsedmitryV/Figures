using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Полностью статическая система локализации (без MonoBehaviour).
/// Таблицы грузятся из Resources/Lang/{ru,en}.csv (2 колонки: key,value).
/// Публичный API: Lang.T(key), Lang.T(key, args), Lang.Get(key),
/// Lang.Name(key, fallback), Lang.Current, Lang.OnLanguageChanged.
/// </summary>
public static class Lang
{
    public static event Action OnLanguageChanged;

    private const string PREF_LANG = "lang.code";
    private const string PREF_LANG_CHOSEN = "lang.chosen";
    private const string RESOURCES_FOLDER = "Lang";

    private static readonly Dictionary<string, string> _table = new(StringComparer.Ordinal);
    private static LangCode _current;

    public static LangCode Current => _current;

    /// <summary>Игрок выбирал язык руками — платформа его больше не переопределяет.</summary>
    public static bool HasPlayerChoice => PlayerPrefs.GetInt(PREF_LANG_CHOSEN, 0) == 1;

    static Lang()
    {
        // Стартовая таблица до того, как платформа отдаст реальный язык.
        // Берём сохранённый выбор, иначе дефолт.
        LangCode start = TryGetSaved(out var saved) ? saved : LangCode.RU;
        SetLanguage(start);

        // Платформу спрашиваем только пока игрок не выбрал язык сам. Раньше её
        // ответ применялся всегда и на старте возвращал язык платформы поверх
        // выбранного — в настройках подсвечивался «не тот» язык.
        if (HasPlayerChoice)
        {
            DevLog.Info($"[Lang] Язык {start} выбран игроком, платформу не опрашиваю.");
            return;
        }

        DevLog.Info($"[Lang] Стартовая таблица {start} загружена, опрашиваю платформу...");
        LocalizationProviderSelector.Select(start).RequestLanguage(OnProviderResolved);
    }

    /// <summary>
    /// Выбор игрока из настроек: запоминается отдельным флагом, чтобы платформа
    /// не перебила его при следующем запуске.
    /// </summary>
    public static void SelectLanguage(LangCode code)
    {
        PlayerPrefs.SetInt(PREF_LANG_CHOSEN, 1);
        SetLanguage(code);
        // флаг сохраняем и тогда, когда выбран уже текущий язык и SetLanguage вышел сразу
        PlayerPrefs.Save();
    }

    // Автоинициализация при запуске приложения: статический конструктор
    // отрабатывает при первом обращении к Lang, но RuntimeInitializeOnLoadMethod
    // гарантирует, что язык определится ещё до первой сцены.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init() => _ = _table.Count; // форсирует static ctor

    // Вызывается обработчиком локализации, когда тот определил язык платформы.
    private static void OnProviderResolved(LangCode code)
    {
        DevLog.Info($"[Lang] Provider вернул язык: {code}");
        SetLanguage(code);
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        // Защита от случайных пробелов по краям ключа (опечатка в инспекторе).
        if (char.IsWhiteSpace(key[0]) || char.IsWhiteSpace(key[key.Length - 1]))
            key = key.Trim();

        return _table.TryGetValue(key, out var value) ? value : key;
    }

    public static string T(string key) => Get(key);

    // Локализованный шаблон с подстановкой аргументов ({0}, {1}, ...).
    public static string T(string key, params object[] args)
    {
        var template = Get(key);
        if (args == null || args.Length == 0) return template;
        try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, args); }
        catch { return template; }
    }

    // Разрешить имя/описание ScriptableObject: если перевод найден — берёт его,
    // иначе падает на значение по умолчанию (поле name/description из инспектора).
    // Get возвращает сам ключ, когда строки нет — этот случай и есть «перевода нет».
    public static string Name(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

        var value = Get(key);
        return value == key ? (fallback ?? value) : value;
    }

    public static void SetLanguage(LangCode code)
    {
        if (code == _current && _table.Count > 0) return;

        _current = code;
        PlayerPrefs.SetString(PREF_LANG, code.ToString());
        PlayerPrefs.Save(); // в WebGL без Save выбор не переживёт перезагрузку вкладки
        LoadTable(_current);
        OnLanguageChanged?.Invoke();
    }

    private static bool TryGetSaved(out LangCode code)
    {
        code = default;
        var raw = PlayerPrefs.GetString(PREF_LANG, null);
        if (string.IsNullOrEmpty(raw)) return false;
        return Enum.TryParse(raw, out code);
    }

    private static void LoadTable(LangCode code)
    {
        var resourcePath = $"{RESOURCES_FOLDER}/{code.ToString().ToLowerInvariant()}";
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogError($"[Lang] Не найдена таблица Resources/{resourcePath}.csv");
            return;
        }

        _table.Clear();
        ParseCsv(asset.text, _table);
        DevLog.Info($"[Lang] Загружена таблица {code}: {_table.Count} ключей");
    }

    // CSV: запятая-разделитель, поля в кавычках, удвоенная кавычка = экранирование,
    // переводы строк внутри кавычек поддерживаются. 3+ столбцы игнорируются.
    private static void ParseCsv(string text, Dictionary<string, string> into)
    {
        var fields = new List<string>(4);
        var sb = new StringBuilder();
        bool inQuotes  = false;
        bool firstRow  = true;
        int  fieldCount = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    fieldCount++;
                    break;

                case '\r':
                    break;

                case '\n':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    fieldCount++;
                    CommitRow(fields, into, ref firstRow);
                    fields.Clear();
                    fieldCount = 0;
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }

        if (sb.Length > 0 || fieldCount > 0)
        {
            fields.Add(sb.ToString());
            CommitRow(fields, into, ref firstRow);
        }
    }

    private static void CommitRow(List<string> fields, Dictionary<string, string> into, ref bool firstRow)
    {
        if (fields.Count == 0) return;

        var key = fields[0];
        if (string.IsNullOrEmpty(key)) return;
        if (key.StartsWith("#"))       return;

        if (firstRow)
        {
            firstRow = false;
            if (string.Equals(key, "key", StringComparison.OrdinalIgnoreCase)) return;
        }

        var value = fields.Count > 1 ? fields[1] : string.Empty;
        into[key] = value;
    }
}
