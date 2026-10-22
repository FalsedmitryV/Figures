#if PLUGIN_YG_2
using UnityEngine;
using YG;
#endif

/// <summary>
/// Разметка геймплея для Яндекс Игр (Gameplay API): платформа должна знать, когда
/// игрок действительно играет, — это требование модерации, и по нему же Яндекс
/// строит частоту рекламы. Без плагина вызовы ничего не делают.
///
/// <see cref="Start"/> — начался активный геймплей (уровень, бой),
/// <see cref="Stop"/> — меню, магазин, экран результата. Повторы безопасны.
/// </summary>
public static class PlatformGameplay
{
#if PLUGIN_YG_2
    private static bool _wanted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        _wanted = false;
        YG2.onPauseGame -= OnPauseGame;
        YG2.onPauseGame += OnPauseGame;
    }
#endif

    public static void Start()
    {
#if PLUGIN_YG_2
        _wanted = true;
        TrySend();
#endif
    }

    public static void Stop()
    {
#if PLUGIN_YG_2
        _wanted = false;
        YG2.GameplayStop();
#endif
    }

#if PLUGIN_YG_2
    private static void TrySend()
    {
        // во время рекламы и без фокуса старт отправлять нельзя
        if (!_wanted || YG2.nowAdsShow || !YG2.isFocusWindowGame) return;
        YG2.GameplayStart();
    }

    // Плагин после рекламы и возврата фокуса возобновляет разметку, только если она
    // шла до паузы. Старт, пропущенный во время рекламы (уровень запустился под
    // interstitial), иначе потерялся бы — дошлём его здесь.
    private static void OnPauseGame(bool paused)
    {
        if (!paused) TrySend();
    }
#endif
}
