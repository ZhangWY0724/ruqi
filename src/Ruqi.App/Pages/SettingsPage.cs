using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly LedgerService _ledger;
    private readonly BackupService _backup;
    private readonly IReminderScheduler _scheduler;
    private readonly IBackupFilePicker _filePicker;
    private readonly AppEventBus _bus;
    private readonly IServiceProvider _services;

    private readonly VerticalStackLayout _bodyStack;

    public SettingsPage(
        LedgerService ledger,
        BackupService backup,
        IReminderScheduler scheduler,
        IBackupFilePicker filePicker,
        AppEventBus bus,
        IServiceProvider services)
    {
        _ledger = ledger;
        _backup = backup;
        _scheduler = scheduler;
        _filePicker = filePicker;
        _bus = bus;
        _services = services;

        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = UiHelper.PageBg;

        _bodyStack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 12, 16, 32),
            Spacing = 14
        };

        Content = new ScrollView { Content = _bodyStack };
        _bus.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var notifStatus = await _scheduler.GetStatusAsync();
        var settings = await _ledger.GetSettingsAsync();
        var allCatalog = await _ledger.SearchCatalogAsync();
        var accounts = await _ledger.GetAccountsAsync();
        var channels = await _ledger.GetPaymentChannelsAsync();

        _bodyStack.Children.Clear();

        _bodyStack.Add(new Label
        {
            Text = "设置",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            Padding = new Thickness(4, 6, 4, 2)
        });

        // 1. 本地通知卡片
        _bodyStack.Add(CreateSectionHeader("本地通知"));
        var notifRows = new VerticalStackLayout { Spacing = 0 };

        notifRows.Add(CreateNavRow(
            "通知权限",
            notifStatus.AreNotificationsEnabled ? "已开启" : "未开启 · 去设置",
            notifStatus.AreNotificationsEnabled ? UiHelper.ConfirmedColor : UiHelper.PendingColor,
            () => _scheduler.OpenAppNotificationSettings()));

        notifRows.Add(UiHelper.CreateDivider());
        notifRows.Add(CreateNavRow(
            "精确提醒权限",
            notifStatus.CanScheduleExactAlarms ? "已授权" : "标准调度（点击优化）",
            notifStatus.CanScheduleExactAlarms ? UiHelper.ConfirmedColor : UiHelper.TextSecondary,
            () => _scheduler.OpenExactAlarmSettings()));

        notifRows.Add(UiHelper.CreateDivider());
        notifRows.Add(CreateNavRow(
            "后台省电白名单",
            notifStatus.IsIgnoringBatteryOptimizations ? "已放行" : "受省电限制（建议放行）",
            notifStatus.IsIgnoringBatteryOptimizations ? UiHelper.ConfirmedColor : UiHelper.TextSecondary,
            () => _scheduler.OpenBatteryOptimizationSettings()));

        notifRows.Add(UiHelper.CreateDivider());
        var defaultRemSummary = BuildDefaultReminderSummary(settings);
        notifRows.Add(CreateNavRowAsync("默认提醒策略", defaultRemSummary, UiHelper.TextSecondary, () => EditDefaultReminderAsync(settings)));

        _bodyStack.Add(UiHelper.CreateCard(notifRows, new Thickness(16, 4), 18));

        // 2. 目录与账号管理卡片
        _bodyStack.Add(CreateSectionHeader("目录、账号与渠道"));
        var dataRows = new VerticalStackLayout { Spacing = 0 };
        dataRows.Add(CreateNavRowAsync("预设与自定义目录", $"{allCatalog.Count} 项", UiHelper.TextSecondary, () => OpenManagePageAsync(ManageDataMode.Catalog)));
        dataRows.Add(UiHelper.CreateDivider());
        dataRows.Add(CreateNavRowAsync("常用使用账号", $"{accounts.Count} 个", UiHelper.TextSecondary, () => OpenManagePageAsync(ManageDataMode.Accounts)));
        dataRows.Add(UiHelper.CreateDivider());
        dataRows.Add(CreateNavRowAsync("付款渠道管理", $"{channels.Count} 项", UiHelper.TextSecondary, () => OpenManagePageAsync(ManageDataMode.Channels)));
        _bodyStack.Add(UiHelper.CreateCard(dataRows, new Thickness(16, 4), 18));

        // 3. 数据备份卡片
        _bodyStack.Add(CreateSectionHeader("数据备份（纯本地）"));
        var backupRows = new VerticalStackLayout { Spacing = 0 };
        backupRows.Add(new Label
        {
            Text = "数据仅保存在本机，无需登录；换机可通过系统「文件」导出与恢复整份账本。",
            FontSize = 12,
            TextColor = UiHelper.TextSecondary,
            Padding = new Thickness(0, 10, 0, 8)
        });
        backupRows.Add(UiHelper.CreateDivider());
        backupRows.Add(CreateNavRowAsync("导出备份到「文件」", "JSON 格式", UiHelper.Primary, HandleExportBackupAsync));
        backupRows.Add(UiHelper.CreateDivider());
        backupRows.Add(CreateNavRowAsync("从「文件」恢复备份", "校验后覆盖本机", UiHelper.Primary, HandleRestoreBackupAsync));

        var lastBackupText = settings.LastBackupAt.HasValue
            ? $"上次备份：{settings.LastBackupAt.Value.ToLocalTime():yyyy年MM月dd日 HH:mm}"
            : "尚未导出过备份";

        backupRows.Add(UiHelper.CreateDivider());
        backupRows.Add(new Label
        {
            Text = lastBackupText,
            FontSize = 12,
            TextColor = UiHelper.TextMuted,
            Padding = new Thickness(0, 10, 0, 10)
        });
        _bodyStack.Add(UiHelper.CreateCard(backupRows, new Thickness(16, 4), 18));

        // 4. 关于与隐私
        _bodyStack.Add(CreateSectionHeader("关于与隐私"));
        var aboutRows = new VerticalStackLayout { Spacing = 0 };
        aboutRows.Add(CreateNavRowAsync("隐私与本地数据说明", "纯本地离线", UiHelper.TextSecondary, () => SplashLaunchPage.ShowReadOnlyPrivacyAsync(Navigation)));
        _bodyStack.Add(UiHelper.CreateCard(aboutRows, new Thickness(16, 4), 18));

        // 版本信息
        _bodyStack.Add(new Label
        {
            Text = "如期 v1.0.0 · 纯本地人民币订阅账本",
            FontSize = 12,
            TextColor = UiHelper.TextMuted,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private async Task EditDefaultReminderAsync(AppSettings current)
    {
        var updated = await DefaultReminderSheetPage.EditAsync(Navigation, current);
        if (updated is null) return;

        await _ledger.SaveSettingsAsync(updated);
        await RefreshAsync();
    }

    private async Task HandleExportBackupAsync()
    {
        try
        {
            var json = await _backup.ExportBackupJsonAsync();
            var fileName = $"ruqi_backup_{DateTime.Now:yyyyMMdd_HHmm}.json";
            var saved = await _filePicker.ExportJsonAsync(fileName, json);
            if (!string.IsNullOrEmpty(saved))
            {
                await ModernDialogPage.ShowAlertAsync(Navigation, "备份导出成功", $"已导出备份文件：{saved}");
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "导出失败", ex.Message);
        }
    }

    private async Task HandleRestoreBackupAsync()
    {
        try
        {
            var json = await _filePicker.PickAndReadJsonAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var preview = _backup.ValidateAndPreview(json);
            var summary =
                $"备份版本：v{preview.SchemaVersion}\n" +
                $"导出时间：{preview.ExportedAt.ToLocalTime():yyyy-MM-dd HH:mm}\n" +
                $"包含订阅：{preview.SubscriptionCount} 条（续费中 {preview.ActiveSubscriptionCount} 条）\n" +
                $"待确认项：{preview.PendingOccurrenceCount} 期\n" +
                $"历史账单：{preview.RecordCount} 笔\n" +
                $"自定义目录/账号/渠道：{preview.CustomItemCount} 项\n\n" +
                "恢复将整份覆盖当前设备上的全部本地数据，是否继续？";

            var confirm = await ModernDialogPage.ShowConfirmAsync(Navigation, "确认恢复备份", summary, confirmText: "确认覆盖恢复", isDanger: true);
            if (!confirm) return;

            await _backup.RestoreBackupJsonAsync(json);
            await _scheduler.RescheduleAllAsync();
            _bus.NotifyDataChanged();
            await RefreshAsync();
            await ModernDialogPage.ShowAlertAsync(Navigation, "恢复完成", "已成功恢复全部账本数据，并根据当前设备权限重新排程本地通知。");
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "恢复失败（已保留原数据）", ex.Message);
        }
    }

    private async Task OpenManagePageAsync(ManageDataMode mode)
    {
        var page = ActivatorUtilities.CreateInstance<ManageDataPage>(_services, mode);
        await Navigation.PushAsync(page);
    }

    private static string BuildDefaultReminderSummary(AppSettings s)
    {
        if (!s.DefaultRemindBeforeEnabled && !s.DefaultRemindOnDayEnabled)
            return "默认关闭";

        var parts = new List<string>();
        if (s.DefaultRemindBeforeEnabled) parts.Add($"提前{s.DefaultRemindBeforeDays}天");
        if (s.DefaultRemindOnDayEnabled) parts.Add("当天");
        return $"{string.Join("、", parts)} · {s.DefaultRemindTime:HH:mm}";
    }

    private static Label CreateSectionHeader(string title) => new()
    {
        Text = title,
        FontSize = 14,
        FontAttributes = FontAttributes.Bold,
        TextColor = UiHelper.TextSecondary,
        Margin = new Thickness(4, 2, 0, -6)
    };

    private static View CreateNavRow(string title, string rightText, Color rightColor, Action onTap)
    {
        var row = BuildNavRowCore(title, rightText, rightColor);
        UiHelper.AttachTap(row, onTap);
        return row;
    }

    private static View CreateNavRowAsync(string title, string rightText, Color rightColor, Func<Task> onTap)
    {
        var row = BuildNavRowCore(title, rightText, rightColor);
        UiHelper.AttachTapAsync(row, onTap);
        return row;
    }

    private static Grid BuildNavRowCore(string title, string rightText, Color rightColor)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8,
            Padding = new Thickness(0, 14)
        };
        row.Add(new Label { Text = title, FontSize = 15, TextColor = UiHelper.TextPrimary, VerticalOptions = LayoutOptions.Center }, 0, 0);
        row.Add(new Label { Text = rightText, FontSize = 13, TextColor = rightColor, HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation }, 1, 0);
        row.Add(new Label { Text = "›", FontSize = 18, TextColor = UiHelper.TextMuted, VerticalOptions = LayoutOptions.Center }, 2, 0);
        return row;
    }
}
