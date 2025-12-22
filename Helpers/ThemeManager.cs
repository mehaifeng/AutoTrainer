using Avalonia;
using Avalonia.Styling;
using System;

namespace AutoTrainer.Helpers
{
    /// <summary>
    /// 主题管理器
    /// 提供浅色/深色主题切换功能
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>
        /// 当前主题变体
        /// </summary>
        public static ThemeVariant CurrentTheme
        {
            get
            {
                if (Application.Current is App app)
                {
                    return app.RequestedThemeVariant ?? ThemeVariant.Default;
                }
                return ThemeVariant.Default;
            }
        }

        /// <summary>
        /// 是否为深色主题
        /// </summary>
        public static bool IsDarkTheme => CurrentTheme == ThemeVariant.Dark;

        /// <summary>
        /// 是否为浅色主题
        /// </summary>
        public static bool IsLightTheme => CurrentTheme == ThemeVariant.Light;

        /// <summary>
        /// 切换到浅色主题
        /// </summary>
        public static void SwitchToLight()
        {
            SetTheme(ThemeVariant.Light);
        }

        /// <summary>
        /// 切换到深色主题
        /// </summary>
        public static void SwitchToDark()
        {
            SetTheme(ThemeVariant.Dark);
        }

        /// <summary>
        /// 跟随系统主题
        /// </summary>
        public static void FollowSystem()
        {
            SetTheme(ThemeVariant.Default);
        }

        /// <summary>
        /// 切换主题（在当前主题和另一个主题之间切换）
        /// </summary>
        public static void Toggle()
        {
            if (CurrentTheme == ThemeVariant.Dark)
            {
                SwitchToLight();
            }
            else
            {
                SwitchToDark();
            }
        }

        /// <summary>
        /// 设置主题
        /// </summary>
        /// <param name="theme">主题变体</param>
        private static void SetTheme(ThemeVariant theme)
        {
            if (Application.Current is App app)
            {
                app.RequestedThemeVariant = theme;
            }
        }

        /// <summary>
        /// 获取当前主题的主色调
        /// </summary>
        public static string GetPrimaryColor()
        {
            return IsDarkTheme ? "#42A5F5" : "#2196F3";
        }

        /// <summary>
        /// 获取当前主题的强调色
        /// </summary>
        public static string GetAccentColor()
        {
            return IsDarkTheme ? "#FFB74D" : "#FF9800";
        }

        /// <summary>
        /// 获取当前主题的背景色
        /// </summary>
        public static string GetBackgroundColor()
        {
            return IsDarkTheme ? "#121212" : "#FAFAFA";
        }

        /// <summary>
        /// 获取当前主题的文字颜色
        /// </summary>
        public static string GetTextColor()
        {
            return IsDarkTheme ? "#FFFFFF" : "#212121";
        }
    }
}
