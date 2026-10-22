#if PLUGIN_YG_2
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YG;

/// <summary>
/// Провайдер языка для Yandex Games SDK.
/// Вместо поллинга YG2.lang в цикле подписывается на события SDK:
///   • onSwitchLang — язык получен/изменён платформой (нормальный путь);
///   • onGetSDKData — SDK проинициализирован (запасной путь: читаем lang).
/// Если ни одно событие не пришло за MAX_WAIT_MS — отдаёт фоллбэк.
/// </summary>
public class YandexLocalizationProvider : ILocalizationProvider
{
    // Максимальное время ожидания языка от платформы, после которого
    // выдаём фоллбэк, чтобы игра не зависла без локализации.
    private const int MAX_WAIT_MS = 8000;

    public bool IsAvailable => true;

    public void RequestLanguage(Action<LangCode> onResult)
    {
        if (onResult == null) return;
        ResolveAsync(onResult).Forget();
    }

    private static async UniTaskVoid ResolveAsync(Action<LangCode> onResult)
    {
        bool settled = false;

        void OnSwitchLang(string lang)
        {
            if (settled) return;
            settled = true;
            YG2.onSwitchLang -= OnSwitchLang;
            var code = ToLangCode(lang);
            DevLog.Info($"[YandexLang] onSwitchLang: '{lang}' -> {code}");
            onResult(code);
        }

        YG2.onSwitchLang += OnSwitchLang;

        // Если к моменту подписки SDK уже готов и язык уже есть — отдаём сразу.
        if (!settled && YG2.isSDKEnabled && !string.IsNullOrEmpty(YG2.lang))
        {
            settled = true;
            YG2.onSwitchLang -= OnSwitchLang;
            var code = ToLangCode(YG2.lang);
            DevLog.Info($"[YandexLang] Уже готов: isSDKEnabled=true, lang='{YG2.lang}' -> {code}");
            onResult(code);
            return;
        }

        // SDK может быть ещё не инициализирован. Запрашиваем язык — когда он
        // придёт, сработает onSwitchLang. Параллельно ждём не дольше MAX_WAIT_MS.
        YG2.GetLanguage();

        await UniTask.Delay(MAX_WAIT_MS);

        if (settled) return;

        // Таймаут: пробуем прочитать lang напрямую, иначе EN.
        var fallback = !string.IsNullOrEmpty(YG2.lang) ? ToLangCode(YG2.lang) : LangCode.EN;
        Debug.LogWarning($"[YandexLang] Таймаут {MAX_WAIT_MS}мс, isSDKEnabled={YG2.isSDKEnabled}, lang='{YG2.lang}'. Отдаю {fallback}.");
        YG2.onSwitchLang -= OnSwitchLang;
        onResult(fallback);
    }

    // ru/RU/ru-RU -> RU, неизвестный язык -> EN.
    // Совпадение ищется по именам LangCode, поэтому новый язык в enum
    // подхватывается сам — правки здесь не нужны.
    private static LangCode ToLangCode(string lang)
    {
        if (string.IsNullOrEmpty(lang)) return LangCode.EN;

        string baseCode = lang.Split('-', '_')[0];
        return Enum.TryParse(baseCode, ignoreCase: true, out LangCode code) ? code : LangCode.EN;
    }
}
#endif
