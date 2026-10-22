using System.Diagnostics;

/// <summary>
/// Информационный лог только для редактора и development-сборок.
/// Conditional убирает сами вызовы из релиза — вместе с построением
/// интерполированной строки, так что в игре они не стоят ничего.
/// Предупреждения и ошибки сюда не переносить: они нужны и в релизе.
/// </summary>
public static class DevLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message) => UnityEngine.Debug.Log(message);
}
