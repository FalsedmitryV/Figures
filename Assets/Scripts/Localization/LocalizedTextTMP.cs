using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Автоматическая локализация TMP_Text: берёт перевод по ключу из Lang
/// и обновляется при смене языка. Ключ можно задать в инспекторе либо
/// оставить пустым — тогда используется текущий text компонента.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedTextTMP : MonoBehaviour
{
    [Tooltip("Ключ перевода в CSV. Если пусто — берётся текущий text компонента.")]
    [SerializeField] private string translationKey;

    [Tooltip("Необязательный суффикс, добавляемый к ключу (напр. .title).")]
    [SerializeField] private string keySuffix = "";

    [Tooltip("Обновлять текст автоматически при смене языка.")]
    [SerializeField] private bool updateOnLanguageChange = true;

    [Tooltip("Аргументы для шаблона {0},{1},... (если перевод содержит плейсхолдеры).")]
    [SerializeField] private string[] formatArgs;

    [Tooltip("Шрифты по языкам; если не задано — шрифт компонента не меняется.")]
    [SerializeField] private FontByLang[] languageFonts;

    [Serializable]
    public struct FontByLang
    {
        public LangCode lang;
        public TMP_FontAsset font;
    }

    private TMP_Text _textComponent;
    private string _fullKey;

    private string FullKey => string.IsNullOrEmpty(keySuffix) ? translationKey : translationKey + keySuffix;

    private void Awake()
    {
        _textComponent = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(translationKey))
            translationKey = _textComponent.text;
        _fullKey = FullKey;
    }

    private void OnEnable()
    {
        if (updateOnLanguageChange)
            Lang.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void Start()
    {
        UpdateText();
    }

    private void OnDisable()
    {
        if (updateOnLanguageChange)
            Lang.OnLanguageChanged -= UpdateText;
    }

    public void UpdateText()
    {
        if (_textComponent == null) _textComponent = GetComponent<TMP_Text>();
        if (_textComponent == null || string.IsNullOrEmpty(_fullKey)) return;

        var translated = formatArgs != null && formatArgs.Length > 0
            ? Lang.T(_fullKey, formatArgs)
            : Lang.T(_fullKey);
        // Lang.T возвращает сам ключ, если перевода нет — тогда оставляем прежний текст.
        if (translated == _fullKey) return;

        _textComponent.text = translated;
        UpdateFont();
    }

    // Шрифт выбирается по текущему языку из Lang.Current.
    private void UpdateFont()
    {
        if (languageFonts == null || languageFonts.Length == 0) return;
        var current = Lang.Current;
        foreach (var entry in languageFonts)
        {
            if (entry.lang == current && entry.font != null)
            {
                _textComponent.font = entry.font;
                return;
            }
        }
    }

    /// <summary>Сменить ключ перевода (опционально с суффиксом) и обновить текст.</summary>
    public void SetKey(string newKey, string suffix = "")
    {
        translationKey = newKey;
        keySuffix = suffix;
        _fullKey = FullKey;
        UpdateText();
    }

    /// <summary>Задать аргументы шаблона {0},{1},... и обновить текст.</summary>
    public void SetFormatArgs(params string[] args)
    {
        formatArgs = args;
        UpdateText();
    }

    public string GetKey() => translationKey;
}
