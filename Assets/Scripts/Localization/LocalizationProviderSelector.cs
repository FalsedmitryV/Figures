/// <summary>
/// Выбирает активный провайдер локализации.
/// Если определён PLUGIN_YG_2 (плагин Yandex Games) — используется
/// YandexLocalizationProvider, иначе — DefaultLocalizationProvider.
/// </summary>
public static class LocalizationProviderSelector
{
    public static ILocalizationProvider Select(LangCode fallback)
    {
#if PLUGIN_YG_2
        var yandex = new YandexLocalizationProvider();
        if (yandex.IsAvailable) return yandex;
#endif
        return new DefaultLocalizationProvider(fallback);
    }
}
