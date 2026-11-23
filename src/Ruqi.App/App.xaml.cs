using Ruqi.App.Pages;
using Ruqi.App.Services;
using Ruqi.Core.Services;

namespace Ruqi.App;

public partial class App : Application
{
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;
    private readonly AppShell _shell;
    private Window? _mainWindow;
    private bool _enteredMainApp;

    public App(LedgerService ledger, IReminderScheduler scheduler, AppEventBus bus, AppShell shell)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;

        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var splash = new SplashLaunchPage(EnterMainAppAsync);
        _mainWindow = new Window(splash);
        _mainWindow.Resumed += (_, _) =>
        {
            if (_enteredMainApp)
            {
                _ = SyncAndRescheduleSafeAsync();
            }
        };
        return _mainWindow;
    }

    private Task EnterMainAppAsync()
    {
        _enteredMainApp = true;
        if (_mainWindow is not null)
        {
            _mainWindow.Page = _shell;
        }

        _ = SyncAndRescheduleSafeAsync();
        return Task.CompletedTask;
    }

    private async Task SyncAndRescheduleSafeAsync()
    {
        try
        {
            await _ledger.InitializeAsync();
            await _ledger.SyncDueOccurrencesAsync();
            await _scheduler.RescheduleAllAsync();
            _bus.NotifyDataChanged();
        }
        catch
        {
            // 启动与切前台容错
        }
    }
}
