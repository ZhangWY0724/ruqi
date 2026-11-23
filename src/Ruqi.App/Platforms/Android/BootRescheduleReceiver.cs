using Android.App;
using Android.Content;
using Ruqi.Core.Data;
using Ruqi.Core.Services;

namespace Ruqi.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter([
    Intent.ActionBootCompleted,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    Intent.ActionMyPackageReplaced
])]
public sealed class BootRescheduleReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;

        var pendingResult = GoAsync();
        Task.Run(async () =>
        {
            try
            {
                var dbPath = Path.Combine(context.FilesDir!.AbsolutePath, "ruqi_ledger.db");
                if (!File.Exists(dbPath))
                {
                    return;
                }

                var db = new RuqiDatabase(dbPath);
                var ledger = new LedgerService(db);
                var scheduler = new AndroidReminderScheduler(ledger);
                await scheduler.RescheduleAllAsync();
            }
            catch
            {
                // 静默容错避免后台广播崩溃
            }
            finally
            {
                pendingResult?.Finish();
            }
        });
    }
}
