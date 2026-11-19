using System;

public static class UIThemeManager
{
    public static bool IsDarkTheme { get; private set; } = false;
    public static event Action<bool> OnThemeChanged;

    public static void SetTheme(bool isDark)
    {
        if (IsDarkTheme == isDark)
            return;

        IsDarkTheme = isDark;
        OnThemeChanged?.Invoke(IsDarkTheme);
    }
}