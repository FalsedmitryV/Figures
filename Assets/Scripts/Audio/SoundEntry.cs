using System;
using UnityEngine;

/// <summary>
/// Одна запись каталога звуков: клип + базовая громкость + диапазон pitch
/// (для вариативности повторяющихся звуков). Без привязки к конкретному id —
/// маппинг id → SoundEntry делает каталог в твоём проекте.
///
/// Зависимости: только UnityEngine.
/// </summary>
[Serializable]
public class SoundEntry
{
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Базовая громкость клипа. Финал = это × мастер-громкость SFX.")]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    [Tooltip("Минимальный pitch (1 = норма).")]
    public float pitchMin = 1f;

    [Range(0.1f, 3f)]
    [Tooltip("Максимальный pitch — между min и max берётся случайный.")]
    public float pitchMax = 1f;

    [Min(0f)]
    [Tooltip("Сколько секунд звучать (реального времени). 0 — клип целиком. " +
             "После лимита звук коротко затухает — так один длинный клип годится и на короткие щелчки.")]
    public float maxDuration;

    /// <summary>Случайный pitch в заданном диапазоне.</summary>
    public float PickPitch() =>
        Mathf.Approximately(pitchMin, pitchMax)
            ? pitchMin
            : UnityEngine.Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
}
