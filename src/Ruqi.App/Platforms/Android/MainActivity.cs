using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using Ruqi.App.Platforms.Android;
using Ruqi.App.Services;

namespace Ruqi.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int RequestCodeCreateDoc = 4101;
    private const int RequestCodeOpenDoc = 4102;
    private const int RequestCodePostNotifications = 4103;

    private static MainActivity? _current;
    private static TaskCompletionSource<global::Android.Net.Uri?>? _createDocTcs;
    private static TaskCompletionSource<global::Android.Net.Uri?>? _openDocTcs;
    private static TaskCompletionSource<bool>? _notifPermTcs;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                WriteCrashLog("AppDomain", ex);
            }
        };

        global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            WriteCrashLog("AndroidEnv", args.Exception);
        };

        base.OnCreate(savedInstanceState);
        _current = this;

        ConfigureLightStatusBar();
        AndroidReminderScheduler.EnsureNotificationChannel(this);
        TryPublishDeepLink(Intent);
    }

    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "last_crash.txt");
            File.WriteAllText(path, $"[{DateTime.Now:O}] [{source}]\n{ex}");
            global::Android.Util.Log.Error("RuqiCrash", ex.ToString());
        }
        catch
        {
            // 忽略写日志异常
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _current = this;
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        TryPublishDeepLink(intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestCodeCreateDoc)
        {
            var tcs = Interlocked.Exchange(ref _createDocTcs, null);
            tcs?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
        }
        else if (requestCode == RequestCodeOpenDoc)
        {
            var tcs = Interlocked.Exchange(ref _openDocTcs, null);
            tcs?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RequestCodePostNotifications)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            var tcs = Interlocked.Exchange(ref _notifPermTcs, null);
            tcs?.TrySetResult(granted);
        }
    }

    public static Task<global::Android.Net.Uri?> CreateDocumentAsync(string suggestedFileName, string mimeType)
    {
        var activity = _current;
        if (activity is null) return Task.FromResult<global::Android.Net.Uri?>(null);

        var tcs = new TaskCompletionSource<global::Android.Net.Uri?>();
        Interlocked.Exchange(ref _createDocTcs, tcs)?.TrySetCanceled();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType(mimeType);
            intent.PutExtra(Intent.ExtraTitle, suggestedFileName);
            activity.StartActivityForResult(intent, RequestCodeCreateDoc);
        });

        return tcs.Task;
    }

    public static Task<global::Android.Net.Uri?> OpenDocumentAsync(string[] mimeTypes)
    {
        var activity = _current;
        if (activity is null) return Task.FromResult<global::Android.Net.Uri?>(null);

        var tcs = new TaskCompletionSource<global::Android.Net.Uri?>();
        Interlocked.Exchange(ref _openDocTcs, tcs)?.TrySetCanceled();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            if (mimeTypes.Length > 0)
            {
                intent.PutExtra(Intent.ExtraMimeTypes, mimeTypes);
            }
            activity.StartActivityForResult(intent, RequestCodeOpenDoc);
        });

        return tcs.Task;
    }

    public static Task<bool> RequestPostNotificationsPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return Task.FromResult(true);
        }

        var activity = _current;
        if (activity is null) return Task.FromResult(false);

        var permission = Manifest.Permission.PostNotifications;
        if (ContextCompat.CheckSelfPermission(activity, permission) == Permission.Granted)
        {
            return Task.FromResult(true);
        }

        var tcs = new TaskCompletionSource<bool>();
        Interlocked.Exchange(ref _notifPermTcs, tcs)?.TrySetResult(false);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ActivityCompat.RequestPermissions(activity, [permission], RequestCodePostNotifications);
        });

        return tcs.Task;
    }

    private void ConfigureLightStatusBar()
    {
        if (Window is null) return;
        Window.AddFlags(global::Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            Window.SetStatusBarColor(global::Android.Graphics.Color.ParseColor("#F4F6FB"));
            Window.SetNavigationBarColor(global::Android.Graphics.Color.White);
        }
        else
        {
            Window.DecorView.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#F4F6FB"));
        }

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = true;
            controller.AppearanceLightNavigationBars = true;
        }
    }

    private static void TryPublishDeepLink(Intent? intent)
    {
        if (intent is null) return;
        var subId = intent.GetStringExtra(AndroidReminderScheduler.ExtraSubscriptionId);
        if (string.IsNullOrWhiteSpace(subId)) return;

        var occId = intent.GetStringExtra(AndroidReminderScheduler.ExtraOccurrenceId);
        var bus = IPlatformApplication.Current?.Services.GetService<AppEventBus>();
        bus?.PublishDeepLink(new DeepLinkTarget(subId, occId));
    }
}
