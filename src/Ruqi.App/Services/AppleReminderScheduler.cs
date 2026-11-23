#if IOS || MACCATALYST
using Foundation;
using Ruqi.Core.Services;
using UIKit;
using UserNotifications;

namespace Ruqi.App.Services;

/// <summary>
/// iOS / macOS (MacCatalyst) 本地定时通知调度实现（基于 UNUserNotificationCenter，自动遵守 64 条上限）。
/// </summary>
public sealed class AppleReminderScheduler : IReminderScheduler
{
    private readonly LedgerService _ledger;

    public AppleReminderScheduler(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public async Task<NotificationStatusInfo> GetStatusAsync()
    {
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        var authorized = settings.AuthorizationStatus is UNAuthorizationStatus.Authorized
            or UNAuthorizationStatus.Provisional
            or UNAuthorizationStatus.Ephemeral;
        return new NotificationStatusInfo(authorized, true, true);
    }

    public async Task<bool> EnsureNotificationPermissionAsync()
    {
        var (granted, _) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
        return granted;
    }

    public async Task RescheduleAllAsync(CancellationToken ct = default)
    {
        var center = UNUserNotificationCenter.Current;
        center.RemoveAllPendingNotificationRequests();

        var status = await GetStatusAsync();
        if (!status.AreNotificationsEnabled)
        {
            return;
        }

        // Apple 限制单个 App 最多同时挂起 64 条本地通知，保留最近的 60 条
        var upcoming = (await _ledger.BuildUpcomingRemindersAsync(DateTime.Now, horizonDays: 60, ct))
            .Take(60)
            .ToList();

        foreach (var item in upcoming)
        {
            var content = new UNMutableNotificationContent
            {
                Title = item.Title,
                Body = item.Body,
                Sound = UNNotificationSound.Default
            };

            var userInfo = new NSMutableDictionary();
            userInfo.SetValueForKey(new NSString(item.SubscriptionId), new NSString("ruqi_sub_id"));
            if (!string.IsNullOrEmpty(item.OccurrenceId))
            {
                userInfo.SetValueForKey(new NSString(item.OccurrenceId), new NSString("ruqi_occ_id"));
            }
            content.UserInfo = userInfo;

            var comps = new NSDateComponents
            {
                Year = item.FireAtLocal.Year,
                Month = item.FireAtLocal.Month,
                Day = item.FireAtLocal.Day,
                Hour = item.FireAtLocal.Hour,
                Minute = item.FireAtLocal.Minute,
                Second = 0
            };

            var trigger = UNCalendarNotificationTrigger.CreateTrigger(comps, repeats: false);
            var req = UNNotificationRequest.FromIdentifier($"ruqi_{item.NotificationId}", content, trigger);
            await center.AddNotificationRequestAsync(req);
        }
    }

    public void OpenAppNotificationSettings()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var url = new NSUrl(UIApplication.OpenSettingsUrlString);
            if (UIApplication.SharedApplication.CanOpenUrl(url))
            {
                UIApplication.SharedApplication.OpenUrl(url, new NSDictionary(), null);
            }
        });
    }

    public void OpenExactAlarmSettings() => OpenAppNotificationSettings();

    public void OpenBatteryOptimizationSettings() => OpenAppNotificationSettings();
}
#endif
