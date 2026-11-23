using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class SubscriptionsPage : ContentPage
{
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;
    private readonly IServiceProvider _services;

    private readonly ConfirmBillingSheet _confirmSheet;
    private readonly SegmentedSwitch _viewSwitch;
    private readonly Border _searchContainer;
    private readonly Entry _searchEntry;

    private readonly VerticalStackLayout _listViewRoot;
    private readonly HorizontalStackLayout _cycleChipBar;
    private readonly HorizontalStackLayout _statusChipBar;
    private readonly VerticalStackLayout _subsListContainer;

    private readonly VerticalStackLayout _calendarViewRoot;
    private readonly RenewalCalendarView _calendarView;
    private readonly Label _selectedDayTitleLabel;
    private readonly VerticalStackLayout _selectedDayEventsContainer;

    private int _activeSubTab; // 0 = 列表, 1 = 日历
    private string _cycleFilter = "全部";
    private string _statusFilter = "全部状态";
    private string _searchKeyword = string.Empty;

    private List<Subscription> _allSubs = [];
    private Dictionary<string, int> _pendingCountBySub = [];
    private List<CalendarEventItem> _monthEvents = [];
    private int _calYear = DateTime.Today.Year;
    private int _calMonth = DateTime.Today.Month;
    private DateOnly _selectedCalDate = DateOnly.FromDateTime(DateTime.Today);

    public SubscriptionsPage(LedgerService ledger, IReminderScheduler scheduler, AppEventBus bus, IServiceProvider services)
    {
        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;
        _services = services;

        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = UiHelper.PageBg;

        _confirmSheet = new ConfirmBillingSheet(_ledger, _scheduler, _bus);
        _confirmSheet.Completed += async (_, _) => await RefreshAllAsync();

        _viewSwitch = new SegmentedSwitch("列表", "日历");
        _viewSwitch.SelectedIndexChanged += async (_, idx) =>
        {
            _activeSubTab = idx;
            UpdateSubTabVisibility();
            if (_activeSubTab == 1)
            {
                await LoadCalendarMonthAsync();
            }
        };

        _searchEntry = new Entry
        {
            Placeholder = "搜索应用、档位、账号或付款渠道...",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            VerticalOptions = LayoutOptions.Center
        };
        _searchEntry.TextChanged += (_, e) =>
        {
            _searchKeyword = e.NewTextValue ?? string.Empty;
            RenderSubscriptionList();
        };

        var searchRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 8
        };
        searchRow.Add(new VectorIconView { Kind = VectorIconKind.Search, IconColor = UiHelper.TextMuted, WidthRequest = 16, HeightRequest = 16, StrokeThickness = 1.9f }, 0, 0);
        searchRow.Add(_searchEntry, 1, 0);

        _searchContainer = new Border
        {
            BackgroundColor = Color.FromArgb("#EAEFF8"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 8),
            IsVisible = false,
            Content = searchRow
        };

        _cycleChipBar = new HorizontalStackLayout { Spacing = 8 };
        _statusChipBar = new HorizontalStackLayout { Spacing = 8 };
        _subsListContainer = new VerticalStackLayout { Spacing = 10 };

        _listViewRoot = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                _searchContainer,
                new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _cycleChipBar },
                new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _statusChipBar },
                _subsListContainer
            }
        };

        _calendarView = new RenewalCalendarView();
        _calendarView.MonthChanged += async (_, ym) =>
        {
            _calYear = ym.Year;
            _calMonth = ym.Month;
            await LoadCalendarMonthAsync();
        };
        _calendarView.SelectedDateChanged += (_, dt) =>
        {
            _selectedCalDate = dt;
            RenderSelectedDayEvents();
        };

        _selectedDayTitleLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary
        };
        _selectedDayEventsContainer = new VerticalStackLayout { Spacing = 10 };

        _calendarViewRoot = new VerticalStackLayout
        {
            Spacing = 14,
            IsVisible = false,
            Children =
            {
                _calendarView,
                _selectedDayTitleLabel,
                _selectedDayEventsContainer
            }
        };

        _confirmSheet.Content = BuildMainLayout();
        Content = _confirmSheet;

        _bus.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAllAsync());
        _bus.SwitchSubscriptionsSubTabRequested += (_, index) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                _activeSubTab = index;
                _viewSwitch.SelectedIndex = index;
                UpdateSubTabVisibility();
                if (_activeSubTab == 1)
                {
                    await LoadCalendarMonthAsync();
                }
            });
        };
    }

    private View BuildMainLayout()
    {
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 10,
            Padding = new Thickness(4, 6, 4, 2)
        };

        header.Add(new Label
        {
            Text = "订阅",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var searchToggleBtn = UiHelper.CreateCircleIconButton("⌕", () =>
        {
            if (_activeSubTab != 0)
            {
                _activeSubTab = 0;
                _viewSwitch.SelectedIndex = 0;
                UpdateSubTabVisibility();
            }

            _searchContainer.IsVisible = !_searchContainer.IsVisible;
            if (!_searchContainer.IsVisible)
            {
                _searchEntry.Text = string.Empty;
            }
        });
        header.Add(searchToggleBtn, 1, 0);

        var addBtn = UiHelper.CreateCircleIconButtonAsync(VectorIconKind.Plus, async () =>
        {
            var page = ActivatorUtilities.CreateInstance<SubscriptionFormPage>(_services);
            await ModalCoordinator.PushModalAsync(Navigation, page, animated: true);
        });
        header.Add(addBtn, 2, 0);

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 28),
                Spacing = 14,
                Children =
                {
                    header,
                    _viewSwitch,
                    _listViewRoot,
                    _calendarViewRoot
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAllAsync();
    }

    private void UpdateSubTabVisibility()
    {
        _listViewRoot.IsVisible = _activeSubTab == 0;
        _calendarViewRoot.IsVisible = _activeSubTab == 1;
    }

    private async Task RefreshAllAsync()
    {
        await _ledger.SyncDueOccurrencesAsync();
        _allSubs = await _ledger.GetSubscriptionsAsync();
        var dashboard = await _ledger.GetDashboardSummaryAsync();
        _pendingCountBySub = dashboard.PendingOccurrences
            .GroupBy(p => p.Subscription.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        RenderFilterChips();
        RenderSubscriptionList();

        if (_activeSubTab == 1)
        {
            await LoadCalendarMonthAsync();
        }
    }

    private void RenderFilterChips()
    {
        _cycleChipBar.Children.Clear();
        string[] cycles = ["全部", "月付", "年付", "其他"];
        foreach (var c in cycles)
        {
            var selected = _cycleFilter == c;
            var chip = CreateFilterChip(c, selected, () =>
            {
                _cycleFilter = c;
                RenderFilterChips();
                RenderSubscriptionList();
            });
            _cycleChipBar.Add(chip);
        }

        var totalPendingSubs = _pendingCountBySub.Count;
        _statusChipBar.Children.Clear();
        string[] statuses = ["全部状态", "续费中", "待确认", "不再续费"];
        foreach (var s in statuses)
        {
            var selected = _statusFilter == s;
            var label = s == "待确认" && totalPendingSubs > 0 ? $"待确认 {totalPendingSubs}" : s;
            var chip = CreateFilterChip(label, selected, () =>
            {
                _statusFilter = s;
                RenderFilterChips();
                RenderSubscriptionList();
            }, isPendingAccent: s == "待确认" && totalPendingSubs > 0);
            _statusChipBar.Add(chip);
        }
    }

    private static View CreateFilterChip(string text, bool isSelected, Action onTap, bool isPendingAccent = false)
    {
        var border = new Border
        {
            BackgroundColor = isSelected ? UiHelper.Primary : Colors.White,
            Stroke = isSelected ? UiHelper.Primary : UiHelper.CardBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 7),
            Content = new Label
            {
                Text = text,
                FontSize = 13,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected
                    ? Colors.White
                    : isPendingAccent ? UiHelper.PendingColor : UiHelper.TextSecondary
            }
        };
        UiHelper.AttachTap(border, onTap);
        return border;
    }

    private void RenderSubscriptionList()
    {
        _subsListContainer.Children.Clear();

        IEnumerable<Subscription> query = _allSubs;

        query = _cycleFilter switch
        {
            "月付" => query.Where(s => s.CycleType == BillingCycleType.Monthly),
            "年付" => query.Where(s => s.CycleType == BillingCycleType.Yearly),
            "其他" => query.Where(s => s.CycleType is BillingCycleType.EveryNDays or BillingCycleType.EveryNMonths or BillingCycleType.OneTime),
            _ => query
        };

        query = _statusFilter switch
        {
            "续费中" => query.Where(s => s.Status == SubscriptionStatus.Active),
            "待确认" => query.Where(s => _pendingCountBySub.ContainsKey(s.Id)),
            "不再续费" => query.Where(s => s.Status == SubscriptionStatus.Cancelled),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var q = _searchKeyword.Trim();
            query = query.Where(s =>
                s.AppName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.TierName) && s.TierName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.AccountDisplayCache) && s.AccountDisplayCache.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.PaymentChannelName) && s.PaymentChannelName.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        if (filtered.Count == 0)
        {
            _subsListContainer.Add(UiHelper.CreateCard(new Label
            {
                Text = _allSubs.Count == 0 ? "还没有订阅记录，点右上角 + 马上添加" : "当前筛选条件下没有匹配的订阅",
                FontSize = 14,
                TextColor = UiHelper.TextSecondary,
                HorizontalTextAlignment = TextAlignment.Center
            }, new Thickness(16, 24), 16));
            return;
        }

        foreach (var sub in filtered)
        {
            _subsListContainer.Add(BuildSubscriptionCard(sub));
        }
    }

    private View BuildSubscriptionCard(Subscription sub)
    {
        var hasPending = _pendingCountBySub.TryGetValue(sub.Id, out var pendingCount) && pendingCount > 0;

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
            IconKey = sub.IconKey,
            AppName = sub.AppName,
            BrandColorHex = sub.BrandColor,
            WidthRequest = 42,
            HeightRequest = 42
        }, 0, 0);

        var titleRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = sub.AppName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(sub.AccountDisplayCache))
        {
            titleRow.Add(UiHelper.CreatePill(sub.AccountDisplayCache, UiHelper.PrimarySoft, UiHelper.Primary));
        }

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sub.PaymentChannelName))
            subtitleParts.Add(sub.PaymentChannelName);

        Color subtitleColor = UiHelper.TextSecondary;
        if (hasPending)
        {
            subtitleParts.Add(pendingCount > 1 ? $"{pendingCount}期待确认" : "待确认扣款");
            subtitleColor = UiHelper.PendingColor;
        }
        else if (sub.Status == SubscriptionStatus.Cancelled)
        {
            subtitleParts.Add(sub.ExpiryDate.HasValue
                ? $"不再续费 · {FormatHelpers.FormatMonthDay(sub.ExpiryDate.Value)}到期"
                : "不再续费");
            subtitleColor = UiHelper.TextMuted;
        }
        else if (sub.CycleType == BillingCycleType.OneTime)
        {
            subtitleParts.Add($"{FormatHelpers.FormatMonthDay(sub.NextBillingDate)}到期");
        }
        else
        {
            subtitleParts.Add($"{FormatHelpers.FormatMonthDay(sub.NextBillingDate)}续费");
        }

        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                titleRow,
                new Label
                {
                    Text = string.Join(" · ", subtitleParts),
                    FontSize = 12,
                    TextColor = subtitleColor
                }
            }
        }, 1, 0);

        grid.Add(new Label
        {
            Text = FormatHelpers.FormatAmountWithCycle(sub.AmountCents, sub.CycleType, sub.CycleInterval),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = sub.Status == SubscriptionStatus.Cancelled ? UiHelper.TextMuted : UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 2, 0);

        var card = UiHelper.CreateCard(grid, new Thickness(14, 12), 16);
        UiHelper.AttachTapAsync(card, async () =>
        {
            var page = ActivatorUtilities.CreateInstance<SubscriptionDetailPage>(_services, sub.Id);
            await Navigation.PushAsync(page);
        });
        return card;
    }

    private async Task LoadCalendarMonthAsync()
    {
        _monthEvents = await _ledger.GetCalendarEventsForMonthAsync(_calYear, _calMonth);
        _calendarView.SetMonthData(_calYear, _calMonth, _selectedCalDate, _monthEvents);
        RenderSelectedDayEvents();
    }

    private void RenderSelectedDayEvents()
    {
        _selectedDayTitleLabel.Text = $"{_selectedCalDate.Month}月{_selectedCalDate.Day}日";
        _selectedDayEventsContainer.Children.Clear();

        var dayEvents = _monthEvents.Where(e => e.EventDate == _selectedCalDate).ToList();
        if (dayEvents.Count == 0)
        {
            _selectedDayEventsContainer.Add(UiHelper.CreateCard(new Label
            {
                Text = "当天暂无续费计划或扣款记录",
                FontSize = 13,
                TextColor = UiHelper.TextMuted,
                HorizontalTextAlignment = TextAlignment.Center
            }, new Thickness(16, 18), 16));
            return;
        }

        foreach (var ev in dayEvents)
        {
            _selectedDayEventsContainer.Add(BuildCalendarEventCard(ev));
        }
    }

    private View BuildCalendarEventCard(CalendarEventItem ev)
    {
        var (statusText, statusBg, statusFg) = (ev.Kind, ev.Status) switch
        {
            (OccurrenceKind.ExpiryOnly, _) => ("仅到期", Color.FromArgb("#F0F2F7"), Color.FromArgb("#8A94A6")),
            (_, OccurrenceStatus.Pending) => ("待确认", UiHelper.PendingBg, UiHelper.PendingColor),
            (_, OccurrenceStatus.Confirmed) => ("已扣款", UiHelper.ConfirmedBg, UiHelper.ConfirmedColor),
            _ => ("计划续费", UiHelper.PrimarySoft, UiHelper.Primary)
        };

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
            IconKey = ev.IconKey,
            AppName = ev.AppName,
            BrandColorHex = ev.BrandColor,
            WidthRequest = 40,
            HeightRequest = 40
        }, 0, 0);

        var titleRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = ev.AppName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(ev.AccountDisplay))
        {
            titleRow.Add(UiHelper.CreatePill(ev.AccountDisplay, UiHelper.PrimarySoft, UiHelper.Primary));
        }

        var metaParts = new List<string> { ev.CycleLabel };
        if (!string.IsNullOrWhiteSpace(ev.PaymentChannelName))
            metaParts.Add(ev.PaymentChannelName);

        grid.Add(new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                titleRow,
                new Label
                {
                    Text = string.Join(" · ", metaParts),
                    FontSize = 12,
                    TextColor = UiHelper.TextSecondary
                }
            }
        }, 1, 0);

        var right = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = ev.Kind == OccurrenceKind.ExpiryOnly && ev.AmountCents == 0 ? "到期" : FormatHelpers.FormatCny(ev.AmountCents),
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.End
                },
                UiHelper.CreatePill(statusText, statusBg, statusFg)
            }
        };
        grid.Add(right, 2, 0);

        var card = UiHelper.CreateCard(grid, new Thickness(14, 12), 16);
        UiHelper.AttachTapAsync(card, async () =>
        {
            if (ev.Status == OccurrenceStatus.Pending && !string.IsNullOrEmpty(ev.OccurrenceId))
            {
                await _confirmSheet.OpenAsync(ev.OccurrenceId);
            }
            else if (!string.IsNullOrEmpty(ev.SubscriptionId))
            {
                var page = ActivatorUtilities.CreateInstance<SubscriptionDetailPage>(_services, ev.SubscriptionId);
                await Navigation.PushAsync(page);
            }
        });
        return card;
    }
}
