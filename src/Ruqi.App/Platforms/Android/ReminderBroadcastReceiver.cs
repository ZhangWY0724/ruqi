using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace Ruqi.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ReminderBroadcastReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;

        AndroidReminderScheduler.EnsureNotificationChannel(context);

        var notifId = intent.GetIntExtra(AndroidReminderScheduler.ExtraNotificationId, 1001);
        var title = intent.GetStringExtra(AndroidReminderScheduler.ExtraTitle) ?? "如期续费提醒";
        var body = intent.GetStringExtra(AndroidReminderScheduler.ExtraBody) ?? "有一笔订阅即将续费，点击查看详情。";
        var subId = intent.GetStringExtra(AndroidReminderScheduler.ExtraSubscriptionId);
        var occId = intent.GetStringExtra(AndroidReminderScheduler.ExtraOccurrenceId);

        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (!string.IsNullOrEmpty(subId))
            launchIntent.PutExtra(AndroidReminderScheduler.ExtraSubscriptionId, subId);
        if (!string.IsNullOrEmpty(occId))
            launchIntent.PutExtra(AndroidReminderScheduler.ExtraOccurrenceId, occId);

        var contentIntent = PendingIntent.GetActivity(
            context,
            notifId,
            launchIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, AndroidReminderScheduler.ChannelId);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetAutoCancel(true);
        if (contentIntent is not null)
        {
            builder.SetContentIntent(contentIntent);
        }

        var nm = NotificationManagerCompat.From(context);
        var notification = builder.Build();
        if (nm is not null && nm.AreNotificationsEnabled() && notification is not null)
        {
            nm.Notify(notifId, notification);
        }
    }
}
