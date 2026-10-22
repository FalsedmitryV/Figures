using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Универсальное сохранение/загрузка сериализуемого класса <typeparamref name="T"/>
/// в JSON-файл (через <see cref="JsonUtility"/>). Не завязан на игру — задаёшь имя
/// файла и (опционально) папку.
///
/// <typeparamref name="T"/> должен быть помечен <c>[Serializable]</c> и иметь
/// конструктор без параметров (для дефолта при отсутствии файла).
///
/// Пример:
/// <code>
/// [Serializable] public class SaveData { public int level; }
/// var store = new JsonFileStore&lt;SaveData&gt;("save.json");
/// var data = store.Load();      // new SaveData(), если файла нет
/// data.level = 3; store.Save(data);
/// </code>
///
/// По умолчанию пишет в <see cref="Application.persistentDataPath"/> (правильно для
/// стендэлон-билдов). Для «рядом с .exe / в корне проекта» передай
/// <see cref="NextToExecutableDir"/> в конструктор.
///
/// <b>В WebGL напрямую не использовать</b> — там persistentDataPath не переживает
/// перезагрузку страницы. Хранилище выбирать через <see cref="SaveStore"/>.
///
/// Зависимости: только UnityEngine.
/// </summary>
public class JsonFileStore<T> : ISaveStore<T> where T : class, new()
{
    private readonly string _path;
    private readonly bool   _prettyPrint;

    /// <summary>Папка рядом с исполняемым файлом (в редакторе — корень проекта).</summary>
    public static string NextToExecutableDir =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));

    /// <param name="fileName">Имя файла, например "save.json".</param>
    /// <param name="directory">Папка. null — <see cref="Application.persistentDataPath"/>.</param>
    /// <param name="prettyPrint">Форматировать JSON с отступами.</param>
    public JsonFileStore(string fileName, string directory = null, bool prettyPrint = true)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("fileName is null or empty", nameof(fileName));

        directory = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
        _path = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, fileName));
        _prettyPrint = prettyPrint;
    }

    /// <summary>Полный путь к файлу.</summary>
    public string Path => _path;

    /// <summary>Существует ли файл сохранения.</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>Сохранить. Папка создаётся при необходимости. Ошибки логируются, не кидаются.</summary>
    public void Save(T data)
    {
        if (data == null) return;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonUtility.ToJson(data, _prettyPrint));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JsonFileStore] Save failed ({_path}): {e.Message}");
        }
    }

    /// <summary>Загрузить. Если файла нет или ошибка — вернёт <c>new T()</c>.</summary>
    public T Load() => TryLoad(out var data) ? data : new T();

    /// <summary>Попытаться загрузить. true — успешно; false — файла нет / ошибка.</summary>
    public bool TryLoad(out T data)
    {
        data = null;
        try
        {
            if (!File.Exists(_path)) return false;
            data = JsonUtility.FromJson<T>(File.ReadAllText(_path));
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JsonFileStore] Load failed ({_path}): {e.Message}");
            return false;
        }
    }

    /// <summary>Удалить файл сохранения, если он есть.</summary>
    public void Delete()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JsonFileStore] Delete failed ({_path}): {e.Message}");
        }
    }
}
