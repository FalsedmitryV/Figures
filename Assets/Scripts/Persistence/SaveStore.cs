/// <summary>
/// Выбор хранилища под платформу — единственное место, где должен стоять
/// <c>#if UNITY_WEBGL</c>. Игра работает через <see cref="ISaveStore{T}"/>
/// и о платформе не знает.
///
/// <list type="bullet">
/// <item>WebGL-билд — <see cref="LocalStorageStore{T}"/>: только он переживает
/// перезагрузку страницы (почему — см. README в этой папке).</item>
/// <item>Редактор и стендэлоны — <see cref="JsonFileStore{T}"/>: читаемый файл
/// в <c>persistentDataPath</c>, который удобно смотреть руками.</item>
/// </list>
/// </summary>
public static class SaveStore
{
    /// <param name="fileName">Имя файла для редактора/стендэлона, например "save.json".</param>
    /// <param name="localStorageKey">Ключ localStorage для WebGL, например "save_json".</param>
    public static ISaveStore<T> Create<T>(string fileName, string localStorageKey) where T : class, new()
    {
        var file = new JsonFileStore<T>(fileName);

#if UNITY_WEBGL && !UNITY_EDITOR
        // файл отдаём как legacy: из него дочитываются сейвы, записанные до перехода на localStorage
        return new LocalStorageStore<T>(localStorageKey, file);
#else
        return file;
#endif
    }
}
