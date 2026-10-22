using System;
using UnityEngine;
using YG.Utils;

/// <summary>
/// Хранилище в браузерном <c>localStorage</c> — единственное, что переживает
/// перезагрузку страницы в WebGL.
///
/// <see cref="Application.persistentDataPath"/> в WebGL — файловая система в
/// памяти (IDBFS). Unity сбрасывает её в IndexedDB только при корректном
/// завершении игры, поэтому по F5 всё записанное за сессию пропадает. Это
/// стандартная ловушка Unity WebGL, а не баг игры. Запись в localStorage
/// синхронная и доступна сразу после перезагрузки.
///
/// Пишется всегда только localStorage. <c>legacy</c> — хранилище «прошлой
/// жизни» (файл), из которого читаем, если ключа ещё нет: так не теряются сейвы
/// игроков, которым Unity всё-таки успел сбросить IDBFS.
///
/// Зависимости: UnityEngine + <see cref="LocalStorage"/> из плагина YG2.
/// </summary>
public class LocalStorageStore<T> : ISaveStore<T> where T : class, new()
{
    private readonly string _key;
    private readonly bool _prettyPrint;
    private readonly ISaveStore<T> _legacy;

    /// <param name="key">Ключ в localStorage, например "save_json".</param>
    /// <param name="legacy">Откуда дочитывать старые сейвы. null — не дочитывать.</param>
    /// <param name="prettyPrint">Форматировать JSON. В localStorage не нужно — это лишние байты.</param>
    public LocalStorageStore(string key, ISaveStore<T> legacy = null, bool prettyPrint = false)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("key is null or empty", nameof(key));

        _key = key;
        _legacy = legacy;
        _prettyPrint = prettyPrint;
    }

    /// <summary>Ключ в localStorage.</summary>
    public string Key => _key;

    /// <summary>Есть ли что загружать — в localStorage или в старом хранилище.</summary>
    public bool Exists =>
        !string.IsNullOrEmpty(LocalStorage.GetKey(_key, string.Empty)) || (_legacy?.Exists ?? false);

    /// <summary>Сохранить. Ошибки логируются, не кидаются.</summary>
    public void Save(T data)
    {
        if (data == null) return;
        try
        {
            LocalStorage.SetKey(_key, JsonUtility.ToJson(data, _prettyPrint));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalStorageStore] Save failed ({_key}): {e.Message}");
        }
    }

    /// <summary>Попытаться загрузить: localStorage, при пустом или битом значении — старое хранилище.</summary>
    public bool TryLoad(out T data)
    {
        data = null;

        var json = LocalStorage.GetKey(_key, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                data = JsonUtility.FromJson<T>(json);
                if (data != null) return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalStorageStore] Broken save ({_key}): {e.Message}");
            }
        }

        return _legacy != null && _legacy.TryLoad(out data);
    }

    /// <summary>Удалить сейв в обоих хранилищах — иначе сброс прогресса воскресит старый файл.</summary>
    public void Delete()
    {
        try
        {
            LocalStorage.DeleteKey(_key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalStorageStore] Delete failed ({_key}): {e.Message}");
        }

        _legacy?.Delete();
    }
}
