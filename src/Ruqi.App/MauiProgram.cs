using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Ruqi.App.Pages;
#if ANDROID
using Ruqi.App.Platforms.Android;
#endif
using Ruqi.App.Services;
using Ruqi.Core.Data;
using Ruqi.Core.Services;

namespace Ruqi.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        ConfigureModernPlatformControls();

        // Core 层注册
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ruqi_ledger.db");
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton(new RuqiDatabase(dbPath));
        builder.Services.AddSingleton<LedgerService>();
        builder.Services.AddSingleton<BackupService>();

        // App 事件总线
        builder.Services.AddSingleton<AppEventBus>();

        // 跨平台能力按目标平台注册
#if ANDROID
        builder.Services.AddSingleton<IReminderScheduler, AndroidReminderScheduler>();
        builder.Services.AddSingleton<IBackupFilePicker, AndroidBackupFilePicker>();
#elif IOS || MACCATALYST
        builder.Services.AddSingleton<IReminderScheduler, AppleReminderScheduler>();
        builder.Services.AddSingleton<IBackupFilePicker, CrossPlatformBackupFilePicker>();
#elif WINDOWS
        builder.Services.AddSingleton<IReminderScheduler, WindowsReminderScheduler>();
        builder.Services.AddSingleton<IBackupFilePicker, CrossPlatformBackupFilePicker>();
#endif

        // 主 Tab 页面与 Shell
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<SubscriptionsPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureModernPlatformControls()
    {
#if ANDROID
        // 彻底移除 Android 原生 Entry / Picker / DatePicker / TimePicker 的默认黑色下划线与冗余内边距
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });

        DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });

        TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });
#elif IOS || MACCATALYST
        EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, _) =>
        {
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
        });
#elif WINDOWS
        EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, _) =>
        {
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = null;
        });
#endif
    }
}
