#if WINDOWS
using Ruqi.Core.Services;

namespace Ruqi.App.Services;

/// <summary>
/// Windows 桌面绿色便携版提醒调度实现（支持打开系统通知设置与前台/运行期定时检测）。
/// </summary>
public sealed class WindowsReminderScheduler : IReminderScheduler
{
    private readonly LedgerService _ledger;

    public WindowsReminderScheduler(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public Task<NotificationStatusInfo> GetStatusAsync()
        => Task.FromResult(new NotificationStatusInfo(true, true, true));

    public Task<bool> EnsureNotificationPermissionAsync()
        => Task.FromResult(true);

    public async Task RescheduleAllAsync(CancellationToken ct = default)
    {
        // 触发未来提醒计算与校验，确保状态同步
        _ = await _ledger.BuildUpcomingRemindersAsync(DateTime.Now, horizonDays: 60, ct);
    }

    public void OpenAppNotificationSettings()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Launcher.Default.OpenAsync("ms-settings:notifications");
            }
            catch
            {
                // 忽略不支持系统设置跳转的环境
            }
        });
    }

    public void OpenExactAlarmSettings() => OpenAppNotificationSettings();

    public void OpenBatteryOptimizationSettings() => OpenAppNotificationSettings();
}
#endif
