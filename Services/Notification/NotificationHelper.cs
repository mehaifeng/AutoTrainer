using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Notification;
using System;
using System.Diagnostics;

namespace AutoTrainer.Helpers
{
    /// <summary>
    /// 统一的通知辅助类
    /// 提供一致的通知样式和行为
    /// </summary>
    public static class NotificationHelper
    {
        // 预定义的颜色方案
        private static class Colors
        {
            public const string SuccessAccent = "#4CAF50";
            public const string SuccessBackground = "#e5f5e9";
            public const string SuccessForeground = "#1B5E20";
            
            public const string ErrorAccent = "#FF5252";
            public const string ErrorBackground = "#ffebee";
            public const string ErrorForeground = "#C62828";
            
            public const string WarningAccent = "#FF9800";
            public const string WarningBackground = "#fff3e0";
            public const string WarningForeground = "#E65100";
            
            public const string InfoAccent = "#2196F3";
            public const string InfoBackground = "#e3f2fd";
            public const string InfoForeground = "#0D47A1";
        }

        /// <summary>
        /// 显示成功通知 - 自动消失5秒
        /// </summary>
        public static void ShowSuccess(this INotificationMessageManager manager, string message, int dismissSeconds = 5)
        {
            manager.CreateMessage()
                .Accent(Colors.SuccessAccent)
                .Background(Colors.SuccessBackground)
                .Foreground(Colors.SuccessForeground)
                .HasBadge("成功")
                .HasMessage(message)
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示成功通知（带打开目录按钮）
        /// </summary>
        public static void ShowSuccessWithDirectory(this INotificationMessageManager manager, string message, string directoryPath, int dismissSeconds = 5)
        {
            manager.CreateMessage()
                .Accent(Colors.SuccessAccent)
                .Background(Colors.SuccessBackground)
                .Foreground(Colors.SuccessForeground)
                .HasBadge("成功")
                .HasMessage(message)
                .Dismiss().WithButton("打开目录", (Action<object>)(button =>
                {
                    try
                    {
                        if (System.IO.Directory.Exists(directoryPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = directoryPath,
                                UseShellExecute = true,
                                Verb = "open"
                            });
                        }
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示错误通知 - 自动消失6秒
        /// </summary>
        public static void ShowError(this INotificationMessageManager manager, string message, int dismissSeconds = 6)
        {
            manager.CreateMessage()
                .Accent(Colors.ErrorAccent)
                .Background(Colors.ErrorBackground)
                .Foreground(Colors.ErrorForeground)
                .HasBadge("错误")
                .HasMessage(message)
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示错误通知（带复制按钮）
        /// </summary>
        public static void ShowErrorWithCopy(this INotificationMessageManager manager, string message, string textToCopy, int dismissSeconds = 6)
        {
            manager.CreateMessage()
                .Accent(Colors.ErrorAccent)
                .Background(Colors.ErrorBackground)
                .Foreground(Colors.ErrorForeground)
                .HasBadge("错误")
                .HasMessage(message)
                .Dismiss().WithButton("复制信息", (Action<object>)(button =>
                {
                    try
                    {
                        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            var clipboard = desktop.MainWindow?.Clipboard;
                            clipboard?.SetTextAsync(textToCopy);
                        }
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示警告通知 - 自动消失4秒
        /// </summary>
        public static void ShowWarning(this INotificationMessageManager manager, string message, int dismissSeconds = 4)
        {
            manager.CreateMessage()
                .Accent(Colors.WarningAccent)
                .Background(Colors.WarningBackground)
                .Foreground(Colors.WarningForeground)
                .HasBadge("警告")
                .HasMessage(message)
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示信息通知 - 自动消失3秒
        /// </summary>
        public static void ShowInfo(this INotificationMessageManager manager, string message, int dismissSeconds = 3)
        {
            manager.CreateMessage()
                .Accent(Colors.InfoAccent)
                .Background(Colors.InfoBackground)
                .Foreground(Colors.InfoForeground)
                .HasBadge("提示")
                .HasMessage(message)
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示信息通知（带自定义按钮）
        /// </summary>
        public static void ShowInfoWithButton(this INotificationMessageManager manager, string message, string buttonText, Action buttonAction, int dismissSeconds = 3)
        {
            manager.CreateMessage()
                .Accent(Colors.InfoAccent)
                .Background(Colors.InfoBackground)
                .Foreground(Colors.InfoForeground)
                .HasBadge("提示")
                .HasMessage(message)
                .Dismiss().WithButton(buttonText, (Action<object>)(button =>
                {
                    try
                    {
                        buttonAction?.Invoke();
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示成功通知（带自定义按钮）
        /// </summary>
        public static void ShowSuccessWithButton(this INotificationMessageManager manager, string message, string buttonText, Action buttonAction, int dismissSeconds = 5)
        {
            manager.CreateMessage()
                .Accent(Colors.SuccessAccent)
                .Background(Colors.SuccessBackground)
                .Foreground(Colors.SuccessForeground)
                .HasBadge("成功")
                .HasMessage(message)
                .Dismiss().WithButton(buttonText, (Action<object>)(button =>
                {
                    try
                    {
                        buttonAction?.Invoke();
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示警告通知（带自定义按钮）
        /// </summary>
        public static void ShowWarningWithButton(this INotificationMessageManager manager, string message, string buttonText, Action buttonAction, int dismissSeconds = 4)
        {
            manager.CreateMessage()
                .Accent(Colors.WarningAccent)
                .Background(Colors.WarningBackground)
                .Foreground(Colors.WarningForeground)
                .HasBadge("警告")
                .HasMessage(message)
                .Dismiss().WithButton(buttonText, (Action<object>)(button =>
                {
                    try
                    {
                        buttonAction?.Invoke();
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }

        /// <summary>
        /// 显示成功通知（带打开文件按钮）
        /// </summary>
        public static void ShowSuccessWithFile(this INotificationMessageManager manager, string message, string filePath, int dismissSeconds = 5)
        {
            manager.CreateMessage()
                .Accent(Colors.SuccessAccent)
                .Background(Colors.SuccessBackground)
                .Foreground(Colors.SuccessForeground)
                .HasBadge("成功")
                .HasMessage(message)
                .Dismiss().WithButton("打开文件", (Action<object>)(button =>
                {
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch { }
                }))
                .Dismiss().WithDelay(TimeSpan.FromSeconds(dismissSeconds))
                .Queue();
        }
    }
}
