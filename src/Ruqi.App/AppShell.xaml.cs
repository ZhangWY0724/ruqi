using Ruqi.App.Pages;

namespace Ruqi.App;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        var tabBar = new TabBar();

        var homeTab = new Tab { Title = "首页", Route = "home", Icon = "tab_home.png" };
        homeTab.Items.Add(new ShellContent
        {
            Route = "home",
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>())
        });

        var subsTab = new Tab { Title = "订阅", Route = "subs", Icon = "tab_subs.png" };
        subsTab.Items.Add(new ShellContent
        {
            Route = "subs",
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<SubscriptionsPage>())
        });

        var statsTab = new Tab { Title = "统计", Route = "stats", Icon = "tab_stats.png" };
        statsTab.Items.Add(new ShellContent
        {
            Route = "stats",
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<StatisticsPage>())
        });

        var settingsTab = new Tab { Title = "设置", Route = "settings", Icon = "tab_settings.png" };
        settingsTab.Items.Add(new ShellContent
        {
            Route = "settings",
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<SettingsPage>())
        });

        tabBar.Items.Add(homeTab);
        tabBar.Items.Add(subsTab);
        tabBar.Items.Add(statsTab);
        tabBar.Items.Add(settingsTab);

        Items.Add(tabBar);
    }
}
