using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class HomePage : ContentPage
{
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;
    private readonly IServiceProvider _services;

    private readonly ConfirmBillingSheet _confirmSheet;
    private readonly CountdownArcView _arcView;
    private readonly Border _heroItemContainer;
    private readonly Label _paidThisMonthLabel;
    private readonly Label _upcoming30Label;
    private readonly Label _monthlyAvgLabel;
    private readonly VerticalStackLayout _pendingSection;
    private readonly Label _pendingBadgeLabel;
    private readonly VerticalStackLayout _pendingList;
    private readonly VerticalStackLayout _upcomingSection;
    private readonly VerticalStackLayout _upcomingList;
    private readonly Border _emptyGuideCard;

    private DashboardSummary? _summary;
    private bool _loading;

    public HomePage(LedgerService ledger, IReminderScheduler scheduler, AppEventBus bus, IServiceProvider services)
    {
        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;
        _services = services;

        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = UiHelper.PageBg;

        _confirmSheet = new ConfirmBillingSheet(_ledger, _scheduler, _bus);
        _confirmSheet.Completed += async (_, _) => await RefreshAsync();

        _arcView = new CountdownArcView();
        _heroItemContainer = new Border
        {
            BackgroundColor = UiHelper.PageBg,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 10),
            IsVisible = false
        };

        _paidThisMonthLabel = CreateMetricValueLabel();
        _upcoming30Label = CreateMetricValueLabel();
        _monthlyAvgLabel = CreateMetricValueLabel();

        _pendingBadgeLabel = new Label
        {
            Text = "0",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _pendingList = new VerticalStackLayout { Spacing = 10 };
        _pendingSection = BuildPendingSection();

        _upcomingList = new VerticalStackLayout { Spacing = 10 };
        _upcomingSection = BuildUpcomingSection();
        _emptyGuideCard = BuildEmptyGuideCard();

        _confirmSheet.Content = BuildMainContent();
        Content = _confirmSheet;

        _bus.DataChanged += OnDataChanged;
        _bus.DeepLinkRequested += OnDeepLinkRequested;
    }

    private View BuildMainContent()
    {
        // 顶部标题栏
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(4, 6, 4, 2)
        };
        header.Add(new Label
        {
            Text = "如期",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var addBtn = UiHelper.CreateCircleIconButton("+", async () => await OpenAddSubscriptionAsync());
        header.Add(addBtn, 1, 0);

        // 倒计时主卡
        var heroCard = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 6,
            Children = { _arcView, _heroItemContainer }
        }, new Thickness(14, 10, 14, 14), 22);

        // 三列核心指标卡
        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 10
        };

        var paidCard = BuildMetricCard("本月已付", _paidThisMonthLabel, async () =>
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//stats");
        });
        var upcoming30Card = BuildMetricCard("未来30天", _upcoming30Label, async () =>
        {
            _bus.RequestSubscriptionsCalendarView();
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//subs");
        });
        var avgCard = BuildMetricCard("月均成本", _monthlyAvgLabel, null);

        metricsGrid.Add(paidCard, 0, 0);
        metricsGrid.Add(upcoming30Card, 1, 0);
        metricsGrid.Add(avgCard, 2, 0);

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 28),
                Spacing = 16,
                Children =
                {
                    header,
                    heroCard,
                    metricsGrid,
                    _pendingSection,
                    _upcomingSection,
                    _emptyGuideCard
                }
            }
        };
    }

    private Border BuildMetricCard(string title, Label valueLabel, Func<Task>? onTap)
    {
        var card = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 12,
                    TextColor = UiHelper.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                valueLabel
            }
        }, new Thickness(8, 12), 16);

        if (onTap is not null)
        {
            UiHelper.AttachTapAsync(card, onTap);
        }

        return card;
    }

    private VerticalStackLayout BuildPendingSection()
    {
        var badge = new Border
        {
            WidthRequest = 20,
            HeightRequest = 20,
            BackgroundColor = UiHelper.PendingColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            VerticalOptions = LayoutOptions.Center,
            Content = _pendingBadgeLabel
        };

        var header = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "待确认",
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    VerticalOptions = LayoutOptions.Center
                },
                badge
            }
        };

        return new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children = { header, _pendingList }
        };
    }

    private VerticalStackLayout BuildUpcomingSection()
    {
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        header.Add(new Label
        {
            Text = "即将续费",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary
        }, 0, 0);

        var allLink = new Label
        {
            Text = "全部订阅 ›",
            FontSize = 13,
            TextColor = UiHelper.Primary,
            VerticalOptions = LayoutOptions.Center
        };
        UiHelper.AttachTapAsync(allLink, async () =>
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//subs");
        });
        header.Add(allLink, 1, 0);

        return new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children = { header, _upcomingList }
        };
    }

    private Border BuildEmptyGuideCard()
    {
        var addBtn = new Button
        {
            Text = "添加第一笔订阅",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            HeightRequest = 46,
            Margin = new Thickness(0, 6, 0, 0)
        };
        addBtn.Clicked += async (_, _) => await OpenAddSubscriptionAsync();

        var card = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new Label
                {
                    Text = "记录你的第一笔订阅",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = "内置 50+ 常见应用目录，离线即可录入与提醒，数据仅保存在本机。",
                    FontSize = 13,
                    TextColor = UiHelper.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                addBtn
            }
        }, new Thickness(20, 22), 18);
        card.IsVisible = false;
        return card;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();

        var pendingLink = _bus.ConsumePendingDeepLink();
        if (pendingLink is not null)
        {
            await HandleDeepLinkAsync(pendingLink);
        }
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
    }

    private void OnDeepLinkRequested(object? sender, DeepLinkTarget target)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var consumed = _bus.ConsumePendingDeepLink() ?? target;
            await HandleDeepLinkAsync(consumed);
        });
    }

    private async Task HandleDeepLinkAsync(DeepLinkTarget target)
    {
        await RefreshAsync();
        if (!string.IsNullOrWhiteSpace(target.OccurrenceId))
        {
            await _confirmSheet.OpenAsync(target.OccurrenceId);
            return;
        }

        var pendings = await _ledger.GetPendingOccurrencesBySubscriptionAsync(target.SubscriptionId);
        if (pendings.Count > 0)
        {
            await _confirmSheet.OpenAsync(pendings[0].Id);
            return;
        }

        await OpenSubscriptionDetailAsync(target.SubscriptionId);
    }

    public async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            _summary = await _ledger.GetDashboardSummaryAsync();
            RenderSummary(_summary);
        }
        finally
        {
            _loading = false;
        }
    }

    private void RenderSummary(DashboardSummary summary)
    {
        // 1. 弧形倒计时
        if (summary.HeroRenewal is null)
        {
            _arcView.HasPlan = false;
            _arcView.DaysRemaining = 0;
            _heroItemContainer.IsVisible = false;
        }
        else
        {
            var hero = summary.HeroRenewal;
            _arcView.HasPlan = true;
            _arcView.DaysRemaining = hero.DaysUntil;
            _heroItemContainer.IsVisible = true;
            _heroItemContainer.Content = BuildHeroSubRow(hero, summary.HeroSameDayCount);
            _heroItemContainer.GestureRecognizers.Clear();
            UiHelper.AttachTapAsync(_heroItemContainer, async () =>
            {
                if (!string.IsNullOrEmpty(hero.PendingOccurrenceId))
                {
                    await _confirmSheet.OpenAsync(hero.PendingOccurrenceId);
                }
                else
                {
                    await OpenSubscriptionDetailAsync(hero.Subscription.Id);
                }
            });
        }

        // 2. 三列汇总卡
        _paidThisMonthLabel.Text = FormatHelpers.FormatCny(summary.PaidThisMonthCents, trimZeroDecimals: true);
        _upcoming30Label.Text = FormatHelpers.FormatCny(summary.Upcoming30DaysCents, trimZeroDecimals: true);
        _monthlyAvgLabel.Text = FormatHelpers.FormatCny(summary.MonthlyAverageCostCents, trimZeroDecimals: true);

        // 3. 待确认区
        _pendingList.Children.Clear();
        _pendingSection.IsVisible = summary.PendingOccurrences.Count > 0;
        _pendingBadgeLabel.Text = summary.PendingOccurrences.Count.ToString();
        foreach (var item in summary.PendingOccurrences)
        {
            _pendingList.Add(BuildPendingCard(item));
        }

        // 4. 即将续费区
        _upcomingList.Children.Clear();
        _upcomingSection.IsVisible = summary.UpcomingRenewals.Count > 0;
        foreach (var item in summary.UpcomingRenewals.Take(6))
        {
            _upcomingList.Add(BuildUpcomingCard(item));
        }

        // 5. 空状态引导
        _emptyGuideCard.IsVisible = !summary.HasAnySubscription;
    }

    private View BuildHeroSubRow(UpcomingRenewalView hero, int sameDayCount)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 12
        };

        grid.Add(new AppIconBadge
        {
            IconKey = hero.Subscription.IconKey,
            AppName = hero.Subscription.AppName,
            BrandColorHex = hero.Subscription.BrandColor,
            WidthRequest = 42,
            HeightRequest = 42,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        // 第一行：左应用名 + 账号胶囊（截断保护），右金额（独占右上角，绝不重叠）
        var topRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8
        };

        var titleText = string.IsNullOrWhiteSpace(hero.Subscription.TierName)
            ? hero.Subscription.AppName
            : $"{hero.Subscription.AppName} · {hero.Subscription.TierName}";

        topRow.Add(new Label
        {
            Text = titleText,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        topRow.Add(new Label
        {
            Text = FormatHelpers.FormatCny(hero.ExpectedAmountCents),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        // 第二行：左日期/渠道/账号，右「当天共N笔」胶囊（独占右下角，绝不挤压）
        var bottomRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8
        };

        var subtitleParts = new List<string> { FormatHelpers.FormatMonthDay(hero.ScheduledDate) };
        if (!string.IsNullOrWhiteSpace(hero.Subscription.PaymentChannelName))
            subtitleParts.Add(hero.Subscription.PaymentChannelName);
        if (!string.IsNullOrWhiteSpace(hero.Subscription.AccountDisplayCache))
            subtitleParts.Add(hero.Subscription.AccountDisplayCache);

        bottomRow.Add(new Label
        {
            Text = string.Join(" · ", subtitleParts),
            FontSize = 12,
            TextColor = UiHelper.TextSecondary,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        if (sameDayCount > 1)
        {
            bottomRow.Add(UiHelper.CreatePill($"当天共{sameDayCount}笔", UiHelper.PendingBg, UiHelper.PendingColor, 11), 1, 0);
        }

        var rightStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children = { topRow, bottomRow }
        };

        grid.Add(rightStack, 1, 0);
        return grid;
    }

    private View BuildPendingCard(PendingOccurrenceView item)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 12
        };

        grid.Add(new AppIconBadge
        {
            IconKey = item.Subscription.IconKey,
            AppName = item.Subscription.AppName,
            BrandColorHex = item.Subscription.BrandColor,
            WidthRequest = 42,
            HeightRequest = 42
        }, 0, 0);

        var dateDesc = item.IsToday
            ? "今天到期"
            : $"已逾期 {item.OverdueDays} 天（{FormatHelpers.FormatMonthDay(item.Occurrence.ScheduledDate)}）";

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Subscription.AccountDisplayCache))
            metaParts.Add(item.Subscription.AccountDisplayCache);
        metaParts.Add(dateDesc);
        metaParts.Add(FormatHelpers.FormatCny(item.Occurrence.ExpectedAmountCents));

        var info = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(item.Subscription.TierName)
                        ? item.Subscription.AppName
                        : $"{item.Subscription.AppName} · {item.Subscription.TierName}",
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = string.Join(" · ", metaParts),
                    FontSize = 12,
                    TextColor = item.IsToday ? UiHelper.PendingColor : UiHelper.DangerColor
                }
            }
        };
        grid.Add(info, 1, 0);

        var confirmBtn = new Button
        {
            Text = "确认",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 16,
            Padding = new Thickness(14, 0),
            HeightRequest = 34,
            MinimumHeightRequest = 34,
            VerticalOptions = LayoutOptions.Center
        };
        confirmBtn.Clicked += async (_, _) => await _confirmSheet.OpenAsync(item.Occurrence.Id);
        grid.Add(confirmBtn, 2, 0);

        var card = UiHelper.CreateCard(grid, new Thickness(14, 12), 16);
        UiHelper.AttachTapAsync(card, async () => await OpenSubscriptionDetailAsync(item.Subscription.Id));
        return card;
    }

    private View BuildUpcomingCard(UpcomingRenewalView item)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 12
        };

        grid.Add(new AppIconBadge
        {
            IconKey = item.Subscription.IconKey,
            AppName = item.Subscription.AppName,
            BrandColorHex = item.Subscription.BrandColor,
            WidthRequest = 42,
            HeightRequest = 42
        }, 0, 0);

        var subParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Subscription.AccountDisplayCache))
            subParts.Add(item.Subscription.AccountDisplayCache);
        if (!string.IsNullOrWhiteSpace(item.Subscription.PaymentChannelName))
            subParts.Add(item.Subscription.PaymentChannelName);
        subParts.Add(FormatHelpers.FormatMonthDay(item.ScheduledDate));

        var info = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = item.Subscription.AppName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary
                },
                new Label
                {
                    Text = string.Join(" · ", subParts),
                    FontSize = 12,
                    TextColor = UiHelper.TextSecondary
                }
            }
        };
        grid.Add(info, 1, 0);

        var right = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Children =
            {
                new Label
                {
                    Text = FormatHelpers.FormatCny(item.ExpectedAmountCents),
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.End
                },
                new Label
                {
                    Text = item.DaysUntil <= 0 ? "今天" : $"{item.DaysUntil}天后",
                    FontSize = 11,
                    TextColor = UiHelper.TextMuted,
                    HorizontalTextAlignment = TextAlignment.End
                }
            }
        };
        grid.Add(right, 2, 0);

        var card = UiHelper.CreateCard(grid, new Thickness(14, 12), 16);
        UiHelper.AttachTapAsync(card, async () => await OpenSubscriptionDetailAsync(item.Subscription.Id));
        return card;
    }

    private async Task OpenAddSubscriptionAsync()
    {
        var page = ActivatorUtilities.CreateInstance<SubscriptionFormPage>(_services);
        await Navigation.PushModalAsync(page);
    }

    private async Task OpenSubscriptionDetailAsync(string subscriptionId)
    {
        var page = ActivatorUtilities.CreateInstance<SubscriptionDetailPage>(_services, subscriptionId);
        await Navigation.PushAsync(page);
    }

    private static Label CreateMetricValueLabel() => new()
    {
        Text = "¥0",
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        TextColor = UiHelper.TextPrimary,
        HorizontalTextAlignment = TextAlignment.Center,
        LineBreakMode = LineBreakMode.TailTruncation
    };
}
