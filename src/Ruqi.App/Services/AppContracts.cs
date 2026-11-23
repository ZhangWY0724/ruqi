namespace Ruqi.App.Services;

public sealed record NotificationStatusInfo(
    bool AreNotificationsEnabled,
    bool CanScheduleExactAlarms,
    bool IsIgnoringBatteryOptimizations);

public interface IReminderScheduler
{
    Task<NotificationStatusInfo> GetStatusAsync();
    Task<bool> EnsureNotificationPermissionAsync();
    Task RescheduleAllAsync(CancellationToken ct = default);
    void OpenAppNotificationSettings();
    void OpenExactAlarmSettings();
    void OpenBatteryOptimizationSettings();
}

public interface IBackupFilePicker
{
    Task<string?> ExportJsonAsync(string suggestedFileName, string jsonContent, CancellationToken ct = default);
    Task<string?> PickAndReadJsonAsync(CancellationToken ct = default);
}

public sealed class AppEventBus
{
    public event EventHandler? DataChanged;
    public event EventHandler<int>? SwitchSubscriptionsSubTabRequested; // 0 = 列表, 1 = 日历
    public event EventHandler<DeepLinkTarget>? DeepLinkRequested;

    public DeepLinkTarget? PendingDeepLink { get; private set; }

    public void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);

    public void RequestSubscriptionsCalendarView() => SwitchSubscriptionsSubTabRequested?.Invoke(this, 1);

    public void PublishDeepLink(DeepLinkTarget target)
    {
        PendingDeepLink = target;
        DeepLinkRequested?.Invoke(this, target);
    }

    public DeepLinkTarget? ConsumePendingDeepLink()
    {
        var current = PendingDeepLink;
        PendingDeepLink = null;
        return current;
    }
}

public sealed record DeepLinkTarget(string SubscriptionId, string? OccurrenceId);
