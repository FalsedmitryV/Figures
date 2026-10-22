using System;

/// <summary>
/// Интерфейс провайдера определения языка платформы.
/// Реализация опрашивает платформу асинхронно и вызывает onResult.
/// </summary>
public interface ILocalizationProvider
{
    bool IsAvailable { get; }
    void RequestLanguage(Action<LangCode> onResult);
}
