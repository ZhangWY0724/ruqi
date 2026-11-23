using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class SubscriptionDetailPage : ContentPage
{
    private readonly string _subscriptionId;
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;
    private readonly IServiceProvider _services;

    private readonly ConfirmBillingSheet _confirmSheet;
    private readonly VerticalStackLayout _bodyStack;

    private Subscription? _sub;

    public SubscriptionDetailPage(
        string subscriptionId,
        LedgerService ledger,
        IReminderScheduler scheduler,
        AppEventBus bus,
        IServiceProvider services)
    {
        _subscriptionId = subscriptionId;
        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;
        _services = services;

        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = UiHelper.PageBg;

        _confirmSheet = new ConfirmBillingSheet(_ledger, _scheduler, _bus);
        _confirmSheet.Completed += async (_, _) => await LoadAsync();

        _bodyStack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 4, 16, 32),
            Spacing = 14
        };

        // 现代自定义三栏顶栏：左返回 · 中标题 · 右「编辑」胶囊 + 浅红删除图标按钮
        var topBar = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(16, 12, 16, 8),
            BackgroundColor = UiHelper.PageBg
        };

        var backBtn = UiHelper.CreateCircleIconButton(VectorIconKind.ChevronLeft, async () => await Navigation.PopAsync(), Colors.White, UiHelper.TextPrimary, 36, 2.0f);
        topBar.Add(backBtn, 0, 0);

        topBar.Add(new Label
        {
            Text = "订阅详情",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var editChip = new Border
        {
            BackgroundColor = UiHelper.PrimarySoft,
            Stroke = Color.FromArgb("#D8DFFF"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(13, 6),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "编辑",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.Primary,
                VerticalOptions = LayoutOptions.Center
            }
        };
        UiHelper.AttachTapAsync(editChip, OpenEditAsync);

        var deleteBtn = UiHelper.CreateCircleIconButton(VectorIconKind.Trash, async () => await HandleDeleteAsync(), UiHelper.DangerBg, UiHelper.DangerColor, 34, 1.8f);

        topBar.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { editChip, deleteBtn }
        }, 2, 0);

        var pageGrid = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ]
        };
        pageGrid.Add(topBar, 0, 0);
        pageGrid.Add(new ScrollView { Content = _bodyStack }, 0, 1);

        _confirmSheet.Content = pageGrid;
        Content = _confirmSheet;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _sub = await _ledger.GetSubscriptionByIdAsync(_subscriptionId);
        if (_sub is null)
        {
            await Navigation.PopAsync();
            return;
        }

        var pendings = await _ledger.GetPendingOccurrencesBySubscriptionAsync(_sub.Id);
        var records = await _ledger.GetBillingRecordsBySubscriptionAsync(_sub.Id);
        var notifStatus = await _scheduler.GetStatusAsync();

        _bodyStack.Children.Clear();

        // 1. 头部英雄信息
        var headerStack = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(0, 8, 0, 4),
            Children =
            {
                new AppIconBadge
                {
                    IconKey = _sub.IconKey,
                    AppName = _sub.AppName,
                    BrandColorHex = _sub.BrandColor,
                    WidthRequest = 62,
                    HeightRequest = 62,
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(_sub.TierName) ? _sub.AppName : $"{_sub.AppName} · {_sub.TierName}",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_sub.AccountDisplayCache))
        {
            var chip = UiHelper.CreatePill(_sub.AccountDisplayCache, Color.FromArgb("#E8ECF5"), UiHelper.TextSecondary, 12);
            chip.HorizontalOptions = LayoutOptions.Center;
            headerStack.Add(chip);
        }

        var pricePill = new Border
        {
            BackgroundColor = _sub.Status == SubscriptionStatus.Active ? UiHelper.PrimarySoft : Color.FromArgb("#F0F2F7"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(18, 6),
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = FormatHelpers.FormatAmountWithCycle(_sub.AmountCents, _sub.CycleType, _sub.CycleInterval),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = _sub.Status == SubscriptionStatus.Active ? UiHelper.Primary : UiHelper.TextMuted
            }
        };
        headerStack.Add(pricePill);

        if (_sub.Status == SubscriptionStatus.Cancelled)
        {
            var stoppedPill = UiHelper.CreatePill("已标记为不再续费", UiHelper.DangerBg, UiHelper.DangerColor, 12);
            stoppedPill.HorizontalOptions = LayoutOptions.Center;
            headerStack.Add(stoppedPill);
        }

        _bodyStack.Add(headerStack);

        // 2. 待确认快捷处理条
        if (pendings.Count > 0)
        {
            var pendingBox = new VerticalStackLayout { Spacing = 8 };
            foreach (var p in pendings)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    [
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    ]
                };
                row.Add(new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = $"{p.ScheduledDate.Month}月期次待确认",
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = UiHelper.PendingColor
                        },
                        new Label
                        {
                            Text = $"计划 {FormatHelpers.FormatFullDate(p.ScheduledDate)} · 预计 {FormatHelpers.FormatCny(p.ExpectedAmountCents)}",
                            FontSize = 12,
                            TextColor = UiHelper.TextSecondary
                        }
                    }
                }, 0, 0);

                var btn = new Button
                {
                    Text = "去确认",
                    BackgroundColor = UiHelper.PendingColor,
                    TextColor = Colors.White,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 14,
                    HeightRequest = 34,
                    MinimumHeightRequest = 34,
                    Padding = new Thickness(12, 0),
                    VerticalOptions = LayoutOptions.Center
                };
                var occId = p.Id;
                UiHelper.AttachClickAsync(btn, () => _confirmSheet.OpenAsync(occId));
                row.Add(btn, 1, 0);
                pendingBox.Add(row);
            }

            _bodyStack.Add(new Border
            {
                BackgroundColor = UiHelper.PendingBg,
                Stroke = Color.FromArgb("#F5D9A6"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14, 12),
                Content = pendingBox
            });
        }

        // 3. 基本信息卡
        var infoRows = new VerticalStackLayout { Spacing = 0 };
        if (_sub.Status == SubscriptionStatus.Cancelled)
        {
            infoRows.Add(CreateInfoRow("服务有效期至", _sub.ExpiryDate.HasValue ? FormatHelpers.FormatFullDate(_sub.ExpiryDate.Value) : "未设置"));
        }
        else
        {
            var dateTitle = _sub.CycleType == BillingCycleType.OneTime ? "到期日期" : "下次扣费";
            infoRows.Add(CreateInfoRow(dateTitle, FormatHelpers.FormatFullDate(_sub.NextBillingDate)));
        }

        infoRows.Add(UiHelper.CreateDivider());
        infoRows.Add(CreateInfoRow("计费周期", FormatHelpers.FormatCycleName(_sub.CycleType, _sub.CycleInterval)));
        infoRows.Add(UiHelper.CreateDivider());
        infoRows.Add(CreateInfoRow("提醒计划", FormatHelpers.FormatReminderSummary(_sub)));

        if (!notifStatus.AreNotificationsEnabled && (_sub.RemindBeforeEnabled || _sub.RemindOnDayEnabled))
        {
            var warnRow = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ],
                Padding = new Thickness(0, 8)
            };
            warnRow.Add(new Label
            {
                Text = "系统通知未开启，到期提醒将无法弹出",
                FontSize = 12,
                TextColor = UiHelper.PendingColor,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            var goSet = new Label
            {
                Text = "去开启 ›",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.Primary,
                VerticalOptions = LayoutOptions.Center
            };
            UiHelper.AttachTap(goSet, () => _scheduler.OpenAppNotificationSettings());
            warnRow.Add(goSet, 1, 0);
            infoRows.Add(warnRow);
        }

        infoRows.Add(UiHelper.CreateDivider());
        infoRows.Add(CreateInfoRow("付款渠道", string.IsNullOrWhiteSpace(_sub.PaymentChannelName) ? "未填写" : _sub.PaymentChannelName));
        infoRows.Add(UiHelper.CreateDivider());
        infoRows.Add(CreateInfoRow("分类", _sub.Category));

        if (!string.IsNullOrWhiteSpace(_sub.Note))
        {
            infoRows.Add(UiHelper.CreateDivider());
            infoRows.Add(CreateInfoRow("备注", _sub.Note));
        }

        _bodyStack.Add(UiHelper.CreateCard(infoRows, new Thickness(16, 4), 18));

        // 4. 历史扣款记录（含 + 补录账单）
        var historyHeader = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(4, 4, 4, 0)
        };
        historyHeader.Add(new Label
        {
            Text = $"历史扣款记录 ({records.Count})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var manualBtn = new Border
        {
            BackgroundColor = UiHelper.PrimarySoft,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(10, 5),
            Content = new Label
            {
                Text = "+ 补录账单",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.Primary
            }
        };
        UiHelper.AttachTapAsync(manualBtn, HandleAddManualRecordAsync);
        historyHeader.Add(manualBtn, 1, 0);
        _bodyStack.Add(historyHeader);

        if (records.Count == 0)
        {
            _bodyStack.Add(UiHelper.CreateCard(new Label
            {
                Text = "暂无历史扣款记录，如本月已扣款可点右上角「+ 补录账单」",
                FontSize = 13,
                TextColor = UiHelper.TextMuted,
                HorizontalTextAlignment = TextAlignment.Center
            }, new Thickness(14, 18), 16));
        }
        else
        {
            var recStack = new VerticalStackLayout { Spacing = 0 };
            for (var i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (i > 0) recStack.Add(UiHelper.CreateDivider());
                var row = new Grid
                {
                    ColumnDefinitions =
                    [
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    ],
                    Padding = new Thickness(0, 12)
                };
                row.Add(new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = FormatHelpers.FormatFullDate(rec.PaidDate),
                            FontSize = 14,
                            TextColor = UiHelper.TextPrimary
                        },
                        new Label
                        {
                            Text = string.IsNullOrWhiteSpace(rec.Note)
                                ? (rec.OccurrenceId is null ? "手工补录" : "周期确认")
                                : rec.Note,
                            FontSize = 12,
                            TextColor = UiHelper.TextMuted
                        }
                    }
                }, 0, 0);

                row.Add(new Label
                {
                    Text = $"{FormatHelpers.FormatCny(rec.PaidAmountCents)} ›",
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);

                var captured = rec;
                UiHelper.AttachTapAsync(row, async () => await HandleEditOrDeleteRecordAsync(captured));
                recStack.Add(row);
            }

            _bodyStack.Add(UiHelper.CreateCard(recStack, new Thickness(16, 4), 18));
        }

        // 5. 底部操作按钮
        var copyBtn = new Button
        {
            Text = "复制为新订阅",
            BackgroundColor = UiHelper.PrimarySoft,
            TextColor = UiHelper.Primary,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 14,
            HeightRequest = 46,
            BorderColor = Color.FromArgb("#DCE0FF"),
            BorderWidth = 1
        };
        UiHelper.AttachClickAsync(copyBtn, OpenCopyAsync);
        _bodyStack.Add(copyBtn);

        if (_sub.Status == SubscriptionStatus.Active)
        {
            var cancelBtn = new Button
            {
                Text = "标记为不再续费",
                BackgroundColor = Colors.Transparent,
                TextColor = UiHelper.DangerColor,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 42
            };
            UiHelper.AttachClickAsync(cancelBtn, HandleCancelAsync);
            _bodyStack.Add(cancelBtn);

            var channelTip = string.IsNullOrWhiteSpace(_sub.PaymentChannelName)
                ? "仅更新本地账本，请自行前往原付款渠道取消续费"
                : $"仅更新本地账本，请自行前往「{_sub.PaymentChannelName}」取消自动续费";

            _bodyStack.Add(new Label
            {
                Text = channelTip,
                FontSize = 12,
                TextColor = UiHelper.TextMuted,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -8, 0, 0)
            });
        }
        else
        {
            var resumeBtn = new Button
            {
                Text = "恢复续费",
                BackgroundColor = UiHelper.Primary,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 14,
                HeightRequest = 46
            };
            UiHelper.AttachClickAsync(resumeBtn, HandleResumeAsync);
            _bodyStack.Add(resumeBtn);
        }
    }

    private static View CreateInfoRow(string label, string value)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 12,
            Padding = new Thickness(0, 12)
        };
        row.Add(new Label
        {
            Text = label,
            FontSize = 14,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        row.Add(new Label
        {
            Text = value,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);
        return row;
    }

    private async Task OpenEditAsync()
    {
        if (_sub is null) return;
        var page = ActivatorUtilities.CreateInstance<SubscriptionFormPage>(_services, _sub.Id, false);
        await ModalCoordinator.PushModalAsync(Navigation, page, animated: true);
    }

    private async Task OpenCopyAsync()
    {
        if (_sub is null) return;
        var page = ActivatorUtilities.CreateInstance<SubscriptionFormPage>(_services, _sub.Id, true);
        await ModalCoordinator.PushModalAsync(Navigation, page, animated: true);
    }

    private async Task HandleCancelAsync()
    {
        if (_sub is null) return;

        var channelHint = string.IsNullOrWhiteSpace(_sub.PaymentChannelName)
            ? "原付款渠道"
            : $"「{_sub.PaymentChannelName}」";

        var action = await ModernPickerPage.PickAsync(
            Navigation,
            $"标记为不再续费？（请前往{channelHint}取消扣款）",
            [
                new PickerOption("keep", $"保留有效期至 {FormatHelpers.FormatMonthDay(_sub.NextBillingDate)}", "在日历中保留到期提醒，不再计入未来续费"),
                new PickerOption("now", "立即停用", "不再保留到期提醒，历史账单不受影响")
            ]);

        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        DateOnly? expiry = action == "keep" ? _sub.NextBillingDate : null;
        await _ledger.MarkAsCancelledAsync(_sub.Id, expiry);
        await _scheduler.RescheduleAllAsync();
        _bus.NotifyDataChanged();
        await LoadAsync();
    }

    private async Task HandleResumeAsync()
    {
        if (_sub is null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var suggestedDate = _sub.NextBillingDate >= today ? _sub.NextBillingDate : today;
        var picked = await ModernDatePickerPage.PickAsync(Navigation, "选择恢复后的下次扣费日", suggestedDate, minDate: today);
        if (!picked.HasValue) return;

        try
        {
            await _ledger.ResumeSubscriptionAsync(_sub.Id, picked.Value);
            await _scheduler.RescheduleAllAsync();
            _bus.NotifyDataChanged();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "无法恢复", ex.Message);
        }
    }

    private async Task HandleDeleteAsync()
    {
        if (_sub is null) return;

        var choice = await ModernPickerPage.PickAsync(
            Navigation,
            $"删除「{_sub.AppName}」（不可撤销）",
            [
                new PickerOption("keep", "仅删除订阅，保留历史账单（推荐）", "支出统计与历次实付记录保持完整"),
                new PickerOption("all", "删除订阅并连带删除全部历史账单", "彻底清除相关数据，支出统计同步扣除")
            ]);

        if (string.IsNullOrEmpty(choice))
        {
            return;
        }

        var mode = choice == "keep"
            ? DeleteSubscriptionMode.KeepHistory
            : DeleteSubscriptionMode.DeleteHistory;

        await _ledger.DeleteSubscriptionAsync(_sub.Id, mode);
        await _scheduler.RescheduleAllAsync();
        _bus.NotifyDataChanged();
        await Navigation.PopAsync();
    }

    private async Task HandleAddManualRecordAsync()
    {
        if (_sub is null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var edited = await ModernDialogPage.ShowBillingEditorAsync(
            Navigation,
            "补录历史账单",
            _sub.AmountCents,
            today,
            "手工补录",
            maxDate: today);

        if (edited is null) return;

        try
        {
            await _ledger.AddManualBillingRecordAsync(_sub.Id, edited.AmountCents, edited.PaidDate, edited.Note);
            _bus.NotifyDataChanged();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "补录失败", ex.Message);
        }
    }

    private async Task HandleEditOrDeleteRecordAsync(BillingRecord record)
    {
        var action = await ModernPickerPage.PickAsync(
            Navigation,
            $"{FormatHelpers.FormatFullDate(record.PaidDate)} · {FormatHelpers.FormatCny(record.PaidAmountCents)}",
            [
                new PickerOption("edit", "修改实付金额与日期"),
                new PickerOption("delete", "删除这笔账单", "支出统计将同步扣除")
            ]);

        if (action == "delete")
        {
            var confirm = await ModernDialogPage.ShowConfirmAsync(
                Navigation,
                "确认删除账单",
                "删除后支出统计将同步扣除该笔金额，是否继续？",
                confirmText: "删除",
                isDanger: true);
            if (!confirm) return;
            await _ledger.DeleteBillingRecordAsync(record.Id);
            _bus.NotifyDataChanged();
            await LoadAsync();
        }
        else if (action == "edit")
        {
            var edited = await ModernDialogPage.ShowBillingEditorAsync(
                Navigation,
                "修改历史账单",
                record.PaidAmountCents,
                record.PaidDate,
                record.Note);

            if (edited is null) return;

            await _ledger.UpdateBillingRecordAsync(record.Id, edited.AmountCents, edited.PaidDate, edited.Note);
            _bus.NotifyDataChanged();
            await LoadAsync();
        }
    }
}
