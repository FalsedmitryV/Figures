using System;

/// <summary>
/// Провайдер по умолчанию: сразу отдаёт заданный язык (из PlayerPrefs/дефолт).
/// Используется, когда платформенный SDK недоступен.
/// </summary>
public class DefaultLocalizationProvider : ILocalizationProvider
{
    private readonly LangCode _language;

    public bool IsAvailable => true;

    public DefaultLocalizationProvider(LangCode language) => _language = language;

    public void RequestLanguage(Action<LangCode> onResult) => onResult?.Invoke(_language);
}
