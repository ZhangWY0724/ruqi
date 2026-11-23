using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class StatisticsPage : ContentPage
{
    private readonly LedgerService _ledger;
    private readonly AppEventBus _bus;

    private readonly SegmentedSwitch _modeSwitch;
    private readonly Label _periodTitleLabel;
    private readonly Label _totalCaptionLabel;
    private readonly Label _totalValueLabel;
    private readonly Label _monthlyAvgHintLabel;
    private readonly TrendBarChartView _trendChart;
    private readonly VerticalStackLayout _breakdownContainer;

    private bool _isYearly;
    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;
    private readonly HashSet<string> _expandedCategories = [];
    private readonly HashSet<string> _expandedApps = [];
    private StatisticsSummary? _summary;

    public StatisticsPage(LedgerService ledger, AppEventBus bus)
    {
        _ledger = ledger;
        _bus = bus;

        Shell.SetNavBarIsVisible(this, false);
        BackgroundColor = UiHelper.PageBg;

        _modeSwitch = new SegmentedSwitch("月度", "年度");
        _modeSwitch.SelectedIndexChanged += async (_, idx) =>
        {
            _isYearly = idx == 1;
            await RefreshAsync();
        };

        _periodTitleLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        _totalCaptionLabel = new Label { FontSize = 13, TextColor = UiHelper.TextSecondary };
        _totalValueLabel = new Label { FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary };
        _monthlyAvgHintLabel = new Label { FontSize = 12, TextColor = UiHelper.TextSecondary };
        _trendChart = new TrendBarChartView();
        _breakdownContainer = new VerticalStackLayout { Spacing = 10 };

        Content = BuildLayout();
        _bus.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
    }

    private View BuildLayout()
    {
        var periodNav = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        periodNav.Add(UiHelper.CreateCircleIconButton("‹", async () => await ShiftPeriodAsync(-1), Colors.White, UiHelper.TextSecondary, 34), 0, 0);
        periodNav.Add(_periodTitleLabel, 1, 0);
        periodNav.Add(UiHelper.CreateCircleIconButton("›", async () => await ShiftPeriodAsync(1), Colors.White, UiHelper.TextSecondary, 34), 2, 0);

        var summaryCard = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                _totalCaptionLabel,
                _totalValueLabel,
                _monthlyAvgHintLabel,
                _trendChart
            }
        }, new Thickness(16, 16, 16, 12), 20);

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 12, 16, 28),
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "支出统计",
                        FontSize = 26,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = UiHelper.TextPrimary,
                        Padding = new Thickness(4, 6, 4, 0)
                    },
                    _modeSwitch,
                    periodNav,
                    summaryCard,
                    new Label
                    {
                        Text = "分类与账号明细",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = UiHelper.TextPrimary,
                        Margin = new Thickness(4, 2, 0, 0)
                    },
                    _breakdownContainer
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task ShiftPeriodAsync(int delta)
    {
        if (_isYearly)
        {
            _year += delta;
        }
        else
        {
            var next = new DateOnly(_year, _month, 1).AddMonths(delta);
            _year = next.Year;
            _month = next.Month;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _summary = await _ledger.GetStatisticsSummaryAsync(_isYearly, _year, _month);

        _periodTitleLabel.Text = _isYearly ? $"{_year}年" : $"{_year}年{_month}月";
        _totalCaptionLabel.Text = _isYearly ? "年度实付总额（人民币）" : "本月实付总额（人民币）";
        _totalValueLabel.Text = FormatHelpers.FormatCny(_summary.TotalPaidCents);
        _monthlyAvgHintLabel.Text = $"当前续费中项目估算月均成本 {FormatHelpers.FormatCny(_summary.MonthlyAverageCostCents)}";

        _trendChart.SetPoints(_summary.TrendPoints);

        if (_expandedCategories.Count == 0 && _summary.Categories.Count > 0)
        {
            _expandedCategories.Add(_summary.Categories[0].Category);
        }

        RenderBreakdown();
    }

    private void RenderBreakdown()
    {
        _breakdownContainer.Children.Clear();
        if (_summary is null || _summary.Categories.Count == 0)
        {
            _breakdownContainer.Add(UiHelper.CreateCard(new Label
            {
                Text = _isYearly ? "当年暂无已确认的实付账单" : "当月暂无已确认的实付账单",
                FontSize = 14,
                TextColor = UiHelper.TextMuted,
                HorizontalTextAlignment = TextAlignment.Center
            }, new Thickness(16, 24), 16));
            return;
        }

        foreach (var cat in _summary.Categories)
        {
            var isCatExpanded = _expandedCategories.Contains(cat.Category);
            var cardContent = new VerticalStackLayout { Spacing = 8 };

            var catRow = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                ],
                ColumnSpacing = 8
            };
            catRow.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = cat.Category, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary },
                    UiHelper.CreatePill($"{cat.Ratio * 100:0.#}%", UiHelper.PrimarySoft, UiHelper.Primary)
                }
            }, 0, 0);
            catRow.Add(new Label
            {
                Text = FormatHelpers.FormatCny(cat.TotalCents),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.TextPrimary,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            catRow.Add(new Label
            {
                Text = isCatExpanded ? "▾" : "▸",
                FontSize = 14,
                TextColor = UiHelper.TextMuted,
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            var catName = cat.Category;
            UiHelper.AttachTap(catRow, () =>
            {
                if (!_expandedCategories.Add(catName)) _expandedCategories.Remove(catName);
                RenderBreakdown();
            });
            cardContent.Add(catRow);

            if (isCatExpanded)
            {
                foreach (var app in cat.Apps)
                {
                    cardContent.Add(UiHelper.CreateDivider());
                    var appKey = $"{cat.Category}|{app.AppName}";
                    var isAppExpanded = _expandedApps.Contains(appKey) || app.Accounts.Count > 1;

                    var appRow = new Grid
                    {
                        ColumnDefinitions =
                        [
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        ],
                        ColumnSpacing = 10,
                        Padding = new Thickness(0, 4)
                    };
                    appRow.Add(new AppIconBadge { IconKey = app.IconKey, AppName = app.AppName, BrandColorHex = app.BrandColor, WidthRequest = 30, HeightRequest = 30 }, 0, 0);

                    var appTitleStack = new HorizontalStackLayout
                    {
                        Spacing = 6,
                        VerticalOptions = LayoutOptions.Center,
                        Children = { new Label { Text = app.AppName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary } }
                    };
                    if (app.HasDeletedRecords)
                    {
                        appTitleStack.Add(UiHelper.CreatePill("含已删除", Color.FromArgb("#F0F2F7"), UiHelper.TextMuted, 10));
                    }

                    appRow.Add(appTitleStack, 1, 0);
                    appRow.Add(new Label
                    {
                        Text = FormatHelpers.FormatCny(app.TotalCents),
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = UiHelper.TextPrimary,
                        VerticalOptions = LayoutOptions.Center
                    }, 2, 0);

                    UiHelper.AttachTap(appRow, () =>
                    {
                        if (!_expandedApps.Add(appKey)) _expandedApps.Remove(appKey);
                        RenderBreakdown();
                    });
                    cardContent.Add(appRow);

                    if (isAppExpanded)
                    {
                        foreach (var acc in app.Accounts)
                        {
                            var accRow = new Grid
                            {
                                ColumnDefinitions =
                                [
                                    new ColumnDefinition(GridLength.Star),
                                    new ColumnDefinition(GridLength.Auto)
                                ],
                                Padding = new Thickness(40, 2, 0, 2)
                            };
                            var accText = acc.IsDeletedSubscription
                                ? $"{acc.AccountDisplay}（已删除）· {acc.RecordCount}笔"
                                : $"{acc.AccountDisplay} · {acc.RecordCount}笔";

                            accRow.Add(new Label { Text = accText, FontSize = 12, TextColor = UiHelper.TextSecondary }, 0, 0);
                            accRow.Add(new Label { Text = FormatHelpers.FormatCny(acc.TotalCents), FontSize = 13, TextColor = UiHelper.TextSecondary }, 1, 0);

                            var capturedRecords = acc.Records;
                            UiHelper.AttachTapAsync(accRow, async () => await ShowAccountRecordsActionAsync(app.AppName, acc.AccountDisplay, capturedRecords));
                            cardContent.Add(accRow);
                        }
                    }
                }
            }

            _breakdownContainer.Add(UiHelper.CreateCard(cardContent, new Thickness(14, 12), 16));
        }
    }

    private async Task ShowAccountRecordsActionAsync(string appName, string accountDisplay, List<BillingRecord> records)
    {
        if (records.Count == 0) return;

        var options = records
            .Select(r => new PickerOption(r.Id, $"{FormatHelpers.FormatFullDate(r.PaidDate)} · {FormatHelpers.FormatCny(r.PaidAmountCents)}", r.Note))
            .ToList();

        var chosenId = await ModernPickerPage.PickAsync(Navigation, $"{appName}（{accountDisplay}）账单明细", options);
        if (string.IsNullOrEmpty(chosenId)) return;

        var rec = records.FirstOrDefault(r => r.Id == chosenId);
        if (rec is null) return;

        var op = await ModernPickerPage.PickAsync(
            Navigation,
            $"{FormatHelpers.FormatFullDate(rec.PaidDate)} · {FormatHelpers.FormatCny(rec.PaidAmountCents)}",
            [
                new PickerOption("edit", "修改实付金额"),
                new PickerOption("delete", "删除这笔账单", "统计数据将同步扣除")
            ]);

        if (op == "delete")
        {
            var ok = await ModernDialogPage.ShowConfirmAsync(Navigation, "确认删除账单", "删除后支出统计将同步扣除该笔金额，是否继续？", confirmText: "删除", isDanger: true);
            if (!ok) return;
            await _ledger.DeleteBillingRecordAsync(rec.Id);
            _bus.NotifyDataChanged();
            await RefreshAsync();
        }
        else if (op == "edit")
        {
            var edited = await ModernDialogPage.ShowBillingEditorAsync(
                Navigation,
                "修改历史账单",
                rec.PaidAmountCents,
                rec.PaidDate,
                rec.Note);
            if (edited is null) return;

            await _ledger.UpdateBillingRecordAsync(rec.Id, edited.AmountCents, edited.PaidDate, edited.Note);
            _bus.NotifyDataChanged();
            await RefreshAsync();
        }
    }
}
