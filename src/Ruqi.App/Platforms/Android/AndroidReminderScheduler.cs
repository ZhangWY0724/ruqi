using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using Ruqi.App.Services;
using Ruqi.Core.Services;

namespace Ruqi.App.Platforms.Android;

public sealed class AndroidReminderScheduler : IReminderScheduler
{
    public const string ChannelId = "renewal_reminders";
    public const string ExtraNotificationId = "ruqi_notif_id";
    public const string ExtraTitle = "ruqi_title";
    public const string ExtraBody = "ruqi_body";
    public const string ExtraSubscriptionId = "ruqi_sub_id";
    public const string ExtraOccurrenceId = "ruqi_occ_id";

    private const string PrefsName = "ruqi_reminder_prefs";
    private const string PrefKeyScheduledIds = "scheduled_ids";

    private readonly LedgerService _ledger;

    public AndroidReminderScheduler(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public static void EnsureNotificationChannel(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;

        var existing = manager.GetNotificationChannel(ChannelId);
        if (existing is not null) return;

        var channel = new NotificationChannel(ChannelId, "如期续费提醒", NotificationImportance.High)
        {
            Description = "订阅续费前提醒、扣款当天提醒与服务到期提醒"
        };
        manager.CreateNotificationChannel(channel);
    }

    public Task<NotificationStatusInfo> GetStatusAsync()
    {
        var context = Platform.AppContext;
        var notifEnabled = NotificationManagerCompat.From(context)?.AreNotificationsEnabled() ?? false;

        var exactAllowed = true;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
            exactAllowed = alarmManager?.CanScheduleExactAlarms() ?? false;
        }

        var ignoringBattery = true;
        var pm = (PowerManager?)context.GetSystemService(Context.PowerService);
        if (pm is not null && !string.IsNullOrEmpty(context.PackageName))
        {
            ignoringBattery = pm.IsIgnoringBatteryOptimizations(context.PackageName);
        }

        return Task.FromResult(new NotificationStatusInfo(notifEnabled, exactAllowed, ignoringBattery));
    }

    public Task<bool> EnsureNotificationPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return Task.FromResult(NotificationManagerCompat.From(Platform.AppContext)?.AreNotificationsEnabled() ?? false);
        }

        return MainActivity.RequestPostNotificationsPermissionAsync();
    }

    public async Task RescheduleAllAsync(CancellationToken ct = default)
    {
        var context = Platform.AppContext;
        EnsureNotificationChannel(context);

        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;

        // 1. 取消旧的已排程闹钟
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        var oldIdsRaw = prefs?.GetString(PrefKeyScheduledIds, string.Empty) ?? string.Empty;
        if (!string.IsNullOrEmpty(oldIdsRaw))
        {
            foreach (var part in oldIdsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out var oldId))
                {
                    var cancelIntent = new Intent(context, typeof(ReminderBroadcastReceiver));
                    var pending = PendingIntent.GetBroadcast(
                        context,
                        oldId,
                        cancelIntent,
                        PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
                    if (pending is not null)
                    {
                        alarmManager.Cancel(pending);
                        pending.Cancel();
                    }
                }
            }
        }

        // 2. 获取未来 60 天内未过期的全部提醒并排程
        var upcoming = await _ledger.BuildUpcomingRemindersAsync(DateTime.Now, horizonDays: 60, ct);
        var newIds = new List<int>(upcoming.Count);

        var canExact = !OperatingSystem.IsAndroidVersionAtLeast(31) || alarmManager.CanScheduleExactAlarms();

        foreach (var item in upcoming)
        {
            var intent = new Intent(context, typeof(ReminderBroadcastReceiver));
            intent.PutExtra(ExtraNotificationId, item.NotificationId);
            intent.PutExtra(ExtraTitle, item.Title);
            intent.PutExtra(ExtraBody, item.Body);
            intent.PutExtra(ExtraSubscriptionId, item.SubscriptionId);
            if (!string.IsNullOrEmpty(item.OccurrenceId))
            {
                intent.PutExtra(ExtraOccurrenceId, item.OccurrenceId);
            }

            var pending = PendingIntent.GetBroadcast(
                context,
                item.NotificationId,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pending is null) continue;

            var triggerAtMillis = new DateTimeOffset(DateTime.SpecifyKind(item.FireAtLocal, DateTimeKind.Local)).ToUnixTimeMilliseconds();
            try
            {
                if (canExact)
                {
                    alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
                }
                else
                {
                    alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
                }

                newIds.Add(item.NotificationId);
            }
            catch
            {
                // 极端机型精确闹钟异常时兜底降级为标准低电耗唤醒
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
                newIds.Add(item.NotificationId);
            }
        }

        prefs?.Edit()?.PutString(PrefKeyScheduledIds, string.Join(",", newIds))?.Apply();
    }

    public void OpenAppNotificationSettings()
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        Intent intent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            intent = new Intent(Settings.ActionAppNotificationSettings);
            intent.PutExtra(Settings.ExtraAppPackage, context.PackageName);
        }
        else
        {
            intent = new Intent(Settings.ActionApplicationDetailsSettings, global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
        }

        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    public void OpenExactAlarmSettings()
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var intent = new Intent(Settings.ActionRequestScheduleExactAlarm, global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
            intent.AddFlags(ActivityFlags.NewTask);
            try
            {
                context.StartActivity(intent);
                return;
            }
            catch
            {
                // 回退到应用详情页
            }
        }

        OpenAppNotificationSettings();
    }

    public void OpenBatteryOptimizationSettings()
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var intent = new Intent(Settings.ActionIgnoreBatteryOptimizationSettings);
            intent.AddFlags(ActivityFlags.NewTask);
            try
            {
                context.StartActivity(intent);
                return;
            }
            catch
            {
                // 回退
            }
        }

        OpenAppNotificationSettings();
    }
}
