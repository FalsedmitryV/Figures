using System;
#if RewardedAdv_yg
using YG;
#endif

/// <summary>
/// Реклама за награду. Модуль Rewarded Adv плагина PluginYG2 подхватывается сам, как
/// только его поставят (он объявляет дефайн RewardedAdv_yg). Пока модуля нет — награда
/// выдаётся сразу, чтобы фичу можно было гонять в редакторе: механика не ждёт рекламы.
/// </summary>
public static class RewardedAds
{
#if RewardedAdv_yg
    public static bool IsRealAdAvailable => true;
#else
    public static bool IsRealAdAvailable => false;
#endif

    /// <param name="rewardId">Id награды для аналитики платформы.</param>
    /// <param name="onReward">Вызывается только после успешного просмотра.</param>
    public static void Show(string rewardId, Action onReward)
    {
        if (onReward == null) return;

#if RewardedAdv_yg
        // перегрузка с колбэком: плагин зовёт его только при успешном просмотре
        // и сбрасывает при ошибке
        YG2.RewardedAdvShow(rewardId, onReward);
#else
        DevLog.Info($"[RewardedAds] Модуль Rewarded Adv не установлен — награда '{rewardId}' выдана без рекламы.");
        onReward();
#endif
    }
}
