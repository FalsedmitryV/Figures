/// <summary>
/// Хранилище одного сериализуемого объекта. Отделяет «куда пишем» от «что пишем»:
/// в комплекте файл (<see cref="JsonFileStore{T}"/>) и браузерный localStorage
/// (<see cref="LocalStorageStore{T}"/>), нужную под платформу выбирает
/// <see cref="SaveStore"/>. Для облака (модуль Storage плагина YG2) достаточно
/// третьей реализации — ни <see cref="SaveManager{TData}"/>, ни игра не меняются.
/// </summary>
public interface ISaveStore<T> where T : class, new()
{
    /// <summary>Есть ли что загружать.</summary>
    bool Exists { get; }

    /// <summary>Сохранить. Ошибки логируются, не кидаются.</summary>
    void Save(T data);

    /// <summary>Попытаться загрузить. false — данных нет или они битые.</summary>
    bool TryLoad(out T data);

    /// <summary>Удалить сохранение, если оно есть.</summary>
    void Delete();
}
