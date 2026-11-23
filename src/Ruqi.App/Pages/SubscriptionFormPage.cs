using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public sealed class SubscriptionFormPage : ContentPage
{
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;
    private readonly string? _sourceSubscriptionId;
    private readonly bool _isCopyMode;
    private bool IsEditMode => !string.IsNullOrEmpty(_sourceSubscriptionId) && !_isCopyMode;

    private readonly Entry _catalogSearchEntry;
    private readonly HorizontalStackLayout _quickPresetChips;
    private readonly VerticalStackLayout _catalogResultsStack;
    private readonly VerticalStackLayout _catalogSection;

    private readonly AppIconBadge _previewIcon;
    private readonly Entry _appNameEntry;
    private readonly Label _categoryValueLabel;
    private readonly Entry _tierEntry;
    private readonly Entry _amountEntry;
    private readonly HorizontalStackLayout _cycleChipBar;
    private readonly Grid _intervalRow;
    private readonly Entry _intervalEntry;
    private readonly Label _intervalUnitLabel;
    private readonly Label _dateFieldLabel;
    private readonly Label _nextDateValueLabel;

    // 一次性顺手记账
    private readonly VerticalStackLayout _oneTimePaymentSection;
    private readonly Switch _recordOneTimeSwitch;
    private readonly VerticalStackLayout _oneTimePaymentDetails;
    private readonly Entry _oneTimePaidAmountEntry;
    private readonly Label _oneTimePaidDateValueLabel;

    // 开通账号与付款渠道（全局跨应用常用账号池）
    private readonly HorizontalStackLayout _quickAccountChips;
    private readonly ScrollView _quickAccountsScroll;
    private readonly Label _accountQuickPickLabel;
    private readonly Entry _accountIdentifierEntry;
    private readonly Entry _accountNicknameEntry;
    private readonly Label _channelValueLabel;
    private readonly Entry _noteEntry;

    // 提醒（复刻 design_v2 胶囊按钮风格 + 现代时间选择器）
    private readonly Border _remindBeforePill;
    private readonly Label _remindBeforePillLabel;
    private readonly Border _remindOnDayPill;
    private readonly Label _remindOnDayPillLabel;
    private readonly Grid _remindDaysRow;
    private readonly Label _remindDaysValueLabel;
    private readonly Grid _remindTimeRow;
    private readonly Label _remindTimeValueLabel;

    private string _selectedCategory = BuiltInCategories.Other;
    private BillingCycleType _selectedCycle = BillingCycleType.Monthly;
    private DateOnly _selectedNextDate = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _minNextDate = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _selectedOneTimePaidDate = DateOnly.FromDateTime(DateTime.Today);
    private TimeOnly _selectedRemindTime = new(9, 0);

    private string? _selectedChannelName;
    private string _selectedIconKey = string.Empty;
    private string _selectedBrandColor = "#5B6CF9";
    private string? _selectedAccountId;
    private bool _remindBeforeEnabled = true;
    private int _remindBeforeDays = 3;
    private bool _remindOnDayEnabled = true;

    private List<PaymentChannel> _channels = [];
    private List<AccountProfile> _globalAccounts = [];
    private bool _saving;
    private bool _initialized;

    private static readonly (string Label, BillingCycleType Type)[] CycleOptions =
    [
        ("每月", BillingCycleType.Monthly),
        ("每年", BillingCycleType.Yearly),
        ("每 N 天", BillingCycleType.EveryNDays),
        ("每 N 月", BillingCycleType.EveryNMonths),
        ("一次性", BillingCycleType.OneTime)
    ];

    public SubscriptionFormPage(LedgerService ledger, IReminderScheduler scheduler, AppEventBus bus)
        : this(ledger, scheduler, bus, null, false)
    {
    }

    public SubscriptionFormPage(
        LedgerService ledger,
        IReminderScheduler scheduler,
        AppEventBus bus,
        string? sourceSubscriptionId,
        bool isCopyMode)
    {
        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;
        _sourceSubscriptionId = sourceSubscriptionId;
        _isCopyMode = isCopyMode;

        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = UiHelper.PageBg;

        // 1. 搜索预设应用区
        _catalogSearchEntry = new Entry
        {
            Placeholder = "搜索预设应用或自定义（ChatGPT、iCloud、B站…）",
            FontSize = 14,
            TextColor = UiHelper.TextPrimary,
            PlaceholderColor = UiHelper.TextMuted,
            BackgroundColor = Colors.Transparent,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            VerticalOptions = LayoutOptions.Center
        };
        _catalogSearchEntry.TextChanged += async (_, e) => await UpdateCatalogSearchAsync(e.NewTextValue);

        var searchGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 8
        };
        searchGrid.Add(new VectorIconView { Kind = VectorIconKind.Search, IconColor = UiHelper.TextMuted, WidthRequest = 16, HeightRequest = 16, StrokeThickness = 1.9f }, 0, 0);
        searchGrid.Add(_catalogSearchEntry, 1, 0);

        _quickPresetChips = new HorizontalStackLayout { Spacing = 8 };
        _catalogResultsStack = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        _catalogSection = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = !IsEditMode && !_isCopyMode,
            Children =
            {
                new Border
                {
                    BackgroundColor = Color.FromArgb("#EAEFF8"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Padding = new Thickness(14, 9),
                    Content = searchGrid
                },
                new ScrollView
                {
                    Orientation = ScrollOrientation.Horizontal,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                    Content = _quickPresetChips
                },
                _catalogResultsStack
            }
        };

        // 2. 核心表单控件
        _previewIcon = new AppIconBadge { WidthRequest = 46, HeightRequest = 46 };
        _appNameEntry = new Entry
        {
            Placeholder = "输入应用名称（必填）",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            PlaceholderColor = UiHelper.TextMuted,
            BackgroundColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center
        };
        _appNameEntry.TextChanged += (_, e) =>
        {
            var name = e.NewTextValue ?? string.Empty;
            _previewIcon.AppName = name;
            if (string.IsNullOrEmpty(_selectedIconKey))
            {
                _selectedBrandColor = FormatHelpers.PickFallbackColor(name);
                _previewIcon.BrandColorHex = _selectedBrandColor;
            }
        };

        _categoryValueLabel = CreateSelectValueLabel(_selectedCategory);
        _tierEntry = CreateCleanRightEntry("如 Plus / 200GB（选填）");

        _amountEntry = new Entry
        {
            Placeholder = "0.00",
            Keyboard = Keyboard.Numeric,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            PlaceholderColor = UiHelper.TextMuted,
            HorizontalTextAlignment = TextAlignment.End,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 110,
            VerticalOptions = LayoutOptions.Center
        };

        _cycleChipBar = new HorizontalStackLayout { Spacing = 8 };

        _intervalEntry = new Entry
        {
            Text = "1",
            Keyboard = Keyboard.Numeric,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            WidthRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
            BackgroundColor = Colors.Transparent
        };
        _intervalUnitLabel = new Label
        {
            Text = "个月",
            FontSize = 14,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        };
        _intervalRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 10),
            IsVisible = false
        };
        _intervalRow.Add(new Label { Text = "自定义间隔", FontSize = 15, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 0);
        _intervalRow.Add(new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "每", FontSize = 14, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center },
                new Border
                {
                    BackgroundColor = Color.FromArgb("#F1F4FA"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(8, 4),
                    Content = _intervalEntry
                },
                _intervalUnitLabel
            }
        }, 1, 0);

        _dateFieldLabel = new Label { Text = "下次扣费日", FontSize = 15, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center };
        _nextDateValueLabel = CreateSelectValueLabel(FormatHelpers.FormatFullDate(_selectedNextDate));

        // 3. 一次性顺手记账
        _oneTimePaidAmountEntry = CreateCleanRightEntry("实付金额（¥）", Keyboard.Numeric);
        _oneTimePaidDateValueLabel = CreateSelectValueLabel(FormatHelpers.FormatFullDate(_selectedOneTimePaidDate));
        _oneTimePaymentDetails = new VerticalStackLayout
        {
            Spacing = 0,
            IsVisible = false,
            Children =
            {
                UiHelper.CreateDivider(),
                BuildInputRow("本次实付（¥）", _oneTimePaidAmountEntry),
                UiHelper.CreateDivider(),
                BuildDateButtonRow(new Label { Text = "实际付款日", FontSize = 15, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center }, _oneTimePaidDateValueLabel, PickOneTimePaidDateAsync)
            }
        };
        _recordOneTimeSwitch = new Switch { IsToggled = false };
        _recordOneTimeSwitch.Toggled += (_, e) =>
        {
            _oneTimePaymentDetails.IsVisible = e.Value;
            if (e.Value && string.IsNullOrWhiteSpace(_oneTimePaidAmountEntry.Text))
            {
                _oneTimePaidAmountEntry.Text = _amountEntry.Text;
            }
        };
        _oneTimePaymentSection = new VerticalStackLayout
        {
            Spacing = 0,
            IsVisible = false,
            Children =
            {
                UiHelper.CreateDivider(),
                BuildSwitchRow("同时记入一笔已付账单", _recordOneTimeSwitch),
                _oneTimePaymentDetails
            }
        };

        // 4. 开通账号与付款渠道（全局快选 + 手动录入）
        _quickAccountChips = new HorizontalStackLayout { Spacing = 8 };
        _quickAccountsScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _quickAccountChips,
            IsVisible = false,
            Margin = new Thickness(0, 6, 0, 6)
        };
        _accountQuickPickLabel = CreateSelectValueLabel("从常用账号快选", isPlaceholder: true);
        _accountIdentifierEntry = CreateCleanRightEntry("手机号或邮箱（选填）");
        _accountIdentifierEntry.TextChanged += (_, _) => _selectedAccountId = null;
        _accountNicknameEntry = CreateCleanRightEntry("如 个人手机 / 工作邮箱（选填）");
        _accountNicknameEntry.TextChanged += (_, _) => _selectedAccountId = null;

        _channelValueLabel = CreateSelectValueLabel("不填写", isPlaceholder: true);
        _noteEntry = CreateCleanRightEntry("原外币价或备注（选填）");

        // 5. 提醒胶囊按钮 + 现代时间选择器
        _remindBeforePillLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _remindBeforePill = new Border
        {
            HeightRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            StrokeThickness = 1,
            Content = _remindBeforePillLabel
        };
        UiHelper.AttachTap(_remindBeforePill, () =>
        {
            _remindBeforeEnabled = !_remindBeforeEnabled;
            UpdateReminderVisuals();
        });

        _remindOnDayPillLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _remindOnDayPill = new Border
        {
            HeightRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            StrokeThickness = 1,
            Content = _remindOnDayPillLabel
        };
        UiHelper.AttachTap(_remindOnDayPill, () =>
        {
            _remindOnDayEnabled = !_remindOnDayEnabled;
            UpdateReminderVisuals();
        });

        _remindDaysValueLabel = CreateSelectValueLabel("提前 3 天");
        _remindDaysRow = BuildSelectRow("提前天数", _remindDaysValueLabel, PickRemindBeforeDaysAsync);

        _remindTimeValueLabel = CreateSelectValueLabel(_selectedRemindTime.ToString("HH:mm"));
        _remindTimeRow = BuildDateButtonRow(
            new Label { Text = "提醒时间", FontSize = 15, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center },
            _remindTimeValueLabel,
            PickRemindTimeAsync);

        RenderCycleChips();
        UpdateReminderVisuals();
        Content = BuildLayout();
    }

    private View BuildLayout()
    {
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

        var cancelLabel = new Label
        {
            Text = "取消",
            FontSize = 16,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(4, 6)
        };
        UiHelper.AttachTapAsync(cancelLabel, async () => await ModalCoordinator.PopModalAsync(this, animated: true));
        topBar.Add(cancelLabel, 0, 0);

        topBar.Add(new Label
        {
            Text = _isCopyMode ? "复制为新订阅" : IsEditMode ? "编辑订阅" : "新增订阅",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var saveChip = new Border
        {
            BackgroundColor = UiHelper.Primary,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16, 7),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "保存",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            }
        };
        UiHelper.AttachTapAsync(saveChip, HandleSaveAsync);
        topBar.Add(saveChip, 2, 0);

        // 卡片 1：应用与金额周期
        var appHeaderRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 12,
            Padding = new Thickness(0, 6, 0, 10)
        };
        appHeaderRow.Add(_previewIcon, 0, 0);
        appHeaderRow.Add(new Border
        {
            BackgroundColor = Color.FromArgb("#F5F7FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 8),
            VerticalOptions = LayoutOptions.Center,
            Content = _appNameEntry
        }, 1, 0);

        var amountRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 10)
        };
        amountRow.Add(new Label { Text = "预计金额", FontSize = 15, TextColor = UiHelper.TextSecondary, VerticalOptions = LayoutOptions.Center }, 0, 0);
        amountRow.Add(new Border
        {
            BackgroundColor = Color.FromArgb("#F5F7FC"),
            Stroke = Color.FromArgb("#E4E9F5"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 6),
            Content = new HorizontalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "¥", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.Primary, VerticalOptions = LayoutOptions.Center },
                    _amountEntry
                }
            }
        }, 1, 0);

        var cycleBlock = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(0, 12),
            Children =
            {
                new Label { Text = "计费周期", FontSize = 15, TextColor = UiHelper.TextSecondary },
                new ScrollView
                {
                    Orientation = ScrollOrientation.Horizontal,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                    Content = _cycleChipBar
                }
            }
        };

        var coreCard = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                appHeaderRow,
                UiHelper.CreateDivider(),
                amountRow,
                UiHelper.CreateDivider(),
                BuildSelectRow("分类", _categoryValueLabel, PickCategoryAsync),
                UiHelper.CreateDivider(),
                BuildInputRow("档位 / 套餐", _tierEntry),
                UiHelper.CreateDivider(),
                cycleBlock,
                _intervalRow,
                UiHelper.CreateDivider(),
                BuildDateButtonRow(_dateFieldLabel, _nextDateValueLabel, PickNextDateAsync),
                _oneTimePaymentSection
            }
        }, new Thickness(16, 10), 20);

        // 卡片 2：开通账号与付款渠道
        var accountCard = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                BuildSelectRow("常用账号", _accountQuickPickLabel, PickGlobalAccountAsync),
                _quickAccountsScroll,
                UiHelper.CreateDivider(),
                BuildInputRow("手机号 / 邮箱", _accountIdentifierEntry),
                UiHelper.CreateDivider(),
                BuildInputRow("账号简称", _accountNicknameEntry),
                UiHelper.CreateDivider(),
                BuildSelectRow("付款渠道", _channelValueLabel, PickChannelAsync),
                UiHelper.CreateDivider(),
                BuildInputRow("备注", _noteEntry)
            }
        }, new Thickness(16, 6), 20);

        // 卡片 3：提醒计划
        var reminderPillsGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 10,
            Padding = new Thickness(0, 6, 0, 10)
        };
        reminderPillsGrid.Add(_remindBeforePill, 0, 0);
        reminderPillsGrid.Add(_remindOnDayPill, 1, 0);

        var reminderCard = UiHelper.CreateCard(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                reminderPillsGrid,
                _remindDaysRow,
                _remindTimeRow
            }
        }, new Thickness(16, 10), 20);

        var bottomSaveBtn = new Button
        {
            Text = IsEditMode ? "保存修改" : "保存订阅",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 16,
            HeightRequest = 50,
            Margin = new Thickness(0, 4, 0, 0)
        };
        UiHelper.AttachClickAsync(bottomSaveBtn, HandleSaveAsync);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 4, 16, 36),
                Spacing = 14,
                Children =
                {
                    _catalogSection,
                    coreCard,
                    CreateSectionLabel("开通账号与付款渠道"),
                    accountCard,
                    CreateSectionLabel("提醒计划"),
                    reminderCard,
                    bottomSaveBtn
                }
            }
        };

        var pageGrid = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ]
        };
        pageGrid.Add(topBar, 0, 0);
        pageGrid.Add(scroll, 0, 1);
        return pageGrid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        _channels = await _ledger.GetPaymentChannelsAsync();
        await LoadGlobalAccountsAsync();
        await LoadQuickPresetsAsync();

        if (!string.IsNullOrEmpty(_sourceSubscriptionId))
        {
            await LoadFromSourceAsync(_sourceSubscriptionId, _isCopyMode);
        }
        else
        {
            var settings = await _ledger.GetSettingsAsync();
            _remindBeforeEnabled = settings.DefaultRemindBeforeEnabled;
            _remindBeforeDays = Math.Max(1, settings.DefaultRemindBeforeDays);
            _remindOnDayEnabled = settings.DefaultRemindOnDayEnabled;
            _selectedRemindTime = settings.DefaultRemindTime;
            UpdateReminderVisuals();
        }

        UpdateCycleDependentVisibility();
    }

    private async Task LoadGlobalAccountsAsync()
    {
        _globalAccounts = await _ledger.GetAccountsAsync();
        _quickAccountChips.Children.Clear();
        _quickAccountsScroll.IsVisible = _globalAccounts.Count > 0;

        foreach (var acc in _globalAccounts)
        {
            var label = FormatHelpers.BuildAccountDetailLine(acc.Nickname, acc.Identifier);
            var chip = UiHelper.CreatePill(label, UiHelper.PrimarySoft, UiHelper.Primary, 12);
            var captured = acc;
            UiHelper.AttachTap(chip, () => SelectAccountProfile(captured));
            _quickAccountChips.Add(chip);
        }
    }

    private void SelectAccountProfile(AccountProfile acc)
    {
        _accountIdentifierEntry.Text = acc.Identifier ?? string.Empty;
        _accountNicknameEntry.Text = acc.Nickname ?? string.Empty;
        _selectedAccountId = acc.Id;
        _accountQuickPickLabel.Text = FormatHelpers.BuildAccountDetailLine(acc.Nickname, acc.Identifier);
        _accountQuickPickLabel.TextColor = UiHelper.Primary;
    }

    private async Task PickGlobalAccountAsync()
    {
        await LoadGlobalAccountsAsync();

        var options = new List<PickerOption> { new("", "不关联常用账号", "清空或手动输入新手机号/邮箱") };
        foreach (var acc in _globalAccounts)
        {
            var masked = FormatHelpers.MaskIdentifier(acc.Identifier);
            var title = !string.IsNullOrEmpty(masked) ? masked : (acc.Nickname ?? "未命名账号");
            var sub = !string.IsNullOrWhiteSpace(acc.Nickname) && !string.IsNullOrEmpty(masked) ? acc.Nickname : "常用账号";
            options.Add(new PickerOption(acc.Id, title, sub));
        }

        var chosen = await ModernPickerPage.PickAsync(
            Navigation,
            "选择开通账号",
            options,
            _selectedAccountId ?? "",
            extraActionLabel: "+ 新增常用手机号/邮箱");

        if (chosen is null) return;

        if (chosen == "__EXTRA__")
        {
            var edited = await ModernDialogPage.ShowAccountEditorAsync(Navigation, "新增常用账号");
            if (edited is null) return;

            try
            {
                var created = await _ledger.SaveAccountAsync(new AccountProfile
                {
                    AppName = string.Empty,
                    Identifier = edited.Identifier,
                    Nickname = edited.Nickname
                });
                await LoadGlobalAccountsAsync();
                SelectAccountProfile(created);
            }
            catch (Exception ex)
            {
                await ModernDialogPage.ShowAlertAsync(Navigation, "保存失败", ex.Message);
            }
            return;
        }

        if (string.IsNullOrEmpty(chosen))
        {
            _selectedAccountId = null;
            _accountIdentifierEntry.Text = string.Empty;
            _accountNicknameEntry.Text = string.Empty;
            _accountQuickPickLabel.Text = "从常用账号快选";
            _accountQuickPickLabel.TextColor = UiHelper.TextMuted;
            return;
        }

        var matched = _globalAccounts.FirstOrDefault(a => a.Id == chosen);
        if (matched is not null)
        {
            SelectAccountProfile(matched);
        }
    }

    private async Task PickNextDateAsync()
    {
        var title = _selectedCycle == BillingCycleType.OneTime ? "选择到期日期" : "选择下次扣费日";
        var picked = await ModernDatePickerPage.PickAsync(Navigation, title, _selectedNextDate, minDate: _minNextDate);
        if (picked.HasValue)
        {
            _selectedNextDate = picked.Value;
            _nextDateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedNextDate);
        }
    }

    private async Task PickOneTimePaidDateAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var picked = await ModernDatePickerPage.PickAsync(Navigation, "选择实际付款日", _selectedOneTimePaidDate, maxDate: today);
        if (picked.HasValue)
        {
            _selectedOneTimePaidDate = picked.Value;
            _oneTimePaidDateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedOneTimePaidDate);
        }
    }

    private async Task PickRemindTimeAsync()
    {
        var picked = await ModernTimePickerPage.PickAsync(Navigation, "选择提醒时间", _selectedRemindTime);
        if (picked.HasValue)
        {
            _selectedRemindTime = picked.Value;
            _remindTimeValueLabel.Text = _selectedRemindTime.ToString("HH:mm");
        }
    }

    private void RenderCycleChips()
    {
        _cycleChipBar.Children.Clear();
        foreach (var opt in CycleOptions)
        {
            var isSelected = opt.Type == _selectedCycle;
            var chip = new Border
            {
                BackgroundColor = isSelected ? UiHelper.Primary : Color.FromArgb("#F1F4FA"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 8),
                Content = new Label
                {
                    Text = opt.Label,
                    FontSize = 13,
                    FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isSelected ? Colors.White : UiHelper.TextSecondary
                }
            };
            var captured = opt.Type;
            UiHelper.AttachTap(chip, () =>
            {
                _selectedCycle = captured;
                RenderCycleChips();
                UpdateCycleDependentVisibility();
            });
            _cycleChipBar.Add(chip);
        }
    }

    private void UpdateReminderVisuals()
    {
        _remindBeforePill.BackgroundColor = _remindBeforeEnabled ? UiHelper.PrimarySoft : Color.FromArgb("#F2F4F8");
        _remindBeforePill.Stroke = _remindBeforeEnabled ? Color.FromArgb("#CBD3FF") : Colors.Transparent;
        _remindBeforePillLabel.Text = _remindBeforeEnabled ? $"提前 {_remindBeforeDays} 天 ✓" : $"提前 {_remindBeforeDays} 天";
        _remindBeforePillLabel.TextColor = _remindBeforeEnabled ? UiHelper.Primary : UiHelper.TextSecondary;

        _remindOnDayPill.BackgroundColor = _remindOnDayEnabled ? UiHelper.PrimarySoft : Color.FromArgb("#F2F4F8");
        _remindOnDayPill.Stroke = _remindOnDayEnabled ? Color.FromArgb("#CBD3FF") : Colors.Transparent;
        _remindOnDayPillLabel.Text = _remindOnDayEnabled ? "当天提醒 ✓" : "当天提醒";
        _remindOnDayPillLabel.TextColor = _remindOnDayEnabled ? UiHelper.Primary : UiHelper.TextSecondary;

        _remindDaysRow.IsVisible = _remindBeforeEnabled;
        _remindDaysValueLabel.Text = $"提前 {_remindBeforeDays} 天";

        _remindTimeRow.IsVisible = _remindBeforeEnabled || _remindOnDayEnabled;
        _remindTimeValueLabel.Text = _selectedRemindTime.ToString("HH:mm");
    }

    private async Task PickCategoryAsync()
    {
        var options = BuiltInCategories.All.Select(c => new PickerOption(c, c)).ToList();
        var chosen = await ModernPickerPage.PickAsync(Navigation, "选择分类", options, _selectedCategory);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            _selectedCategory = chosen;
            _categoryValueLabel.Text = chosen;
            _categoryValueLabel.TextColor = UiHelper.TextPrimary;
        }
    }

    private async Task PickChannelAsync()
    {
        _channels = await _ledger.GetPaymentChannelsAsync();
        var options = new List<PickerOption> { new("", "不填写", "暂不关联付款渠道") };
        options.AddRange(_channels.Select(c => new PickerOption(c.Name, c.Name, c.IsBuiltIn ? "常用渠道" : "自定义渠道")));

        var chosen = await ModernPickerPage.PickAsync(
            Navigation,
            "选择付款渠道",
            options,
            _selectedChannelName ?? "",
            extraActionLabel: "+ 新增自定义付款渠道");

        if (chosen is null) return;

        if (chosen == "__EXTRA__")
        {
            var name = await ModernDialogPage.ShowPromptAsync(Navigation, "新增付款渠道", "添加你的常用扣款方式", "如：招商银行信用卡、PayPal", confirmText: "添加");
            if (string.IsNullOrWhiteSpace(name)) return;
            var added = await _ledger.AddCustomPaymentChannelAsync(name);
            _selectedChannelName = added.Name;
        }
        else
        {
            _selectedChannelName = string.IsNullOrWhiteSpace(chosen) ? null : chosen;
        }

        UpdateChannelValueDisplay();
    }

    private async Task PickRemindBeforeDaysAsync()
    {
        List<PickerOption> options =
        [
            new("1", "提前 1 天"),
            new("2", "提前 2 天"),
            new("3", "提前 3 天（推荐）"),
            new("5", "提前 5 天"),
            new("7", "提前 7 天（一周前）")
        ];

        var chosen = await ModernPickerPage.PickAsync(
            Navigation,
            "提前几天提醒",
            options,
            _remindBeforeDays.ToString(),
            extraActionLabel: "自定义天数…");

        if (chosen is null) return;

        if (chosen == "__EXTRA__")
        {
            var input = await ModernDialogPage.ShowPromptAsync(
                Navigation,
                "自定义提前提醒天数",
                "请输入正整数天数",
                "例如：14",
                initialValue: _remindBeforeDays.ToString(),
                confirmText: "确定",
                keyboard: Keyboard.Numeric);
            if (int.TryParse(input?.Trim(), out var d) && d >= 1)
            {
                _remindBeforeDays = d;
            }
        }
        else if (int.TryParse(chosen, out var parsed) && parsed >= 1)
        {
            _remindBeforeDays = parsed;
        }

        UpdateReminderVisuals();
    }

    private void UpdateChannelValueDisplay()
    {
        if (string.IsNullOrWhiteSpace(_selectedChannelName))
        {
            _channelValueLabel.Text = "不填写";
            _channelValueLabel.TextColor = UiHelper.TextMuted;
        }
        else
        {
            _channelValueLabel.Text = _selectedChannelName;
            _channelValueLabel.TextColor = UiHelper.TextPrimary;
        }
    }

    private async Task LoadQuickPresetsAsync()
    {
        var all = await _ledger.SearchCatalogAsync();
        string[] featuredApps = ["ChatGPT", "iCloud+", "网易云音乐", "Spotify", "哔哩哔哩", "Claude", "WPS", "京东 PLUS"];
        _quickPresetChips.Children.Clear();

        foreach (var app in featuredApps)
        {
            var item = all.FirstOrDefault(x => x.AppName == app);
            if (item is null) continue;

            var chip = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = UiHelper.CardBorder,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(10, 6),
                Content = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new AppIconBadge { IconKey = item.IconKey, AppName = item.AppName, BrandColorHex = item.BrandColor, WidthRequest = 22, HeightRequest = 22 },
                        new Label { Text = item.AppName, FontSize = 13, TextColor = UiHelper.TextPrimary, VerticalOptions = LayoutOptions.Center }
                    }
                }
            };
            var captured = item;
            UiHelper.AttachTap(chip, () => ApplyCatalogItem(captured));
            _quickPresetChips.Add(chip);
        }
    }

    private async Task UpdateCatalogSearchAsync(string? keyword)
    {
        _catalogResultsStack.Children.Clear();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            _catalogResultsStack.IsVisible = false;
            return;
        }

        var results = (await _ledger.SearchCatalogAsync(keyword)).Take(8).ToList();
        _catalogResultsStack.IsVisible = results.Count > 0;

        foreach (var item in results)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ],
                ColumnSpacing = 10
            };
            row.Add(new AppIconBadge { IconKey = item.IconKey, AppName = item.AppName, BrandColorHex = item.BrandColor, WidthRequest = 34, HeightRequest = 34 }, 0, 0);
            row.Add(new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = string.IsNullOrWhiteSpace(item.TierName) ? item.AppName : $"{item.AppName} · {item.TierName}", FontSize = 14, FontAttributes = FontAttributes.Bold },
                    new Label { Text = item.Category, FontSize = 11, TextColor = UiHelper.TextMuted }
                }
            }, 1, 0);
            row.Add(new Label
            {
                Text = item.ReferencePriceCents.HasValue
                    ? FormatHelpers.FormatAmountWithCycle(item.ReferencePriceCents.Value, item.DefaultCycleType, item.DefaultInterval)
                    : "自定义金额",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.Primary,
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            var card = UiHelper.CreateCard(row, new Thickness(12, 9), 14);
            var captured = item;
            UiHelper.AttachTap(card, () =>
            {
                ApplyCatalogItem(captured);
                _catalogResultsStack.IsVisible = false;
                _catalogSearchEntry.Text = string.Empty;
            });
            _catalogResultsStack.Add(card);
        }
    }

    private void ApplyCatalogItem(CatalogItem item)
    {
        _selectedIconKey = item.IconKey;
        _selectedBrandColor = item.BrandColor;
        _previewIcon.IconKey = item.IconKey;
        _previewIcon.BrandColorHex = item.BrandColor;
        _previewIcon.AppName = item.AppName;

        _appNameEntry.Text = item.AppName;
        _selectedCategory = item.Category;
        _categoryValueLabel.Text = item.Category;
        _categoryValueLabel.TextColor = UiHelper.TextPrimary;
        _tierEntry.Text = item.TierName ?? string.Empty;
        _amountEntry.Text = item.ReferencePriceCents.HasValue ? (item.ReferencePriceCents.Value / 100m).ToString("0.00") : string.Empty;

        _selectedCycle = item.DefaultCycleType;
        _intervalEntry.Text = Math.Max(1, item.DefaultInterval).ToString();
        if (!IsEditMode && item.Category == BuiltInCategories.LifeServices &&
            _selectedCycle == BillingCycleType.OneTime)
        {
            _recordOneTimeSwitch.IsToggled = true;
        }
        RenderCycleChips();
        UpdateCycleDependentVisibility();
    }

    private async Task LoadFromSourceAsync(string sourceId, bool isCopy)
    {
        if (isCopy)
        {
            var draft = await _ledger.CreateCopyDraftAsync(sourceId);
            ApplyDraft(draft);
        }
        else
        {
            var sub = await _ledger.GetSubscriptionByIdAsync(sourceId);
            if (sub is null) return;

            AccountProfile? acc = null;
            if (!string.IsNullOrEmpty(sub.AccountId))
            {
                acc = _globalAccounts.FirstOrDefault(a => a.Id == sub.AccountId);
            }

            ApplyDraft(new SubscriptionDraft
            {
                Id = sub.Id,
                AppName = sub.AppName,
                Category = sub.Category,
                IconKey = sub.IconKey,
                BrandColor = sub.BrandColor,
                TierName = sub.TierName,
                AmountCents = sub.AmountCents,
                CycleType = sub.CycleType,
                CycleInterval = sub.CycleInterval,
                NextBillingDate = sub.NextBillingDate,
                AccountId = sub.AccountId,
                AccountNickname = acc?.Nickname ?? (acc is null ? sub.AccountDisplayCache : null),
                AccountIdentifier = acc?.Identifier,
                PaymentChannelName = sub.PaymentChannelName,
                RemindBeforeEnabled = sub.RemindBeforeEnabled,
                RemindBeforeDays = sub.RemindBeforeDays,
                RemindOnDayEnabled = sub.RemindOnDayEnabled,
                RemindTime = sub.RemindTime,
                Note = sub.Note
            });

            if (sub.NextBillingDate < _minNextDate)
            {
                _minNextDate = sub.NextBillingDate;
            }
        }
    }

    private void ApplyDraft(SubscriptionDraft draft)
    {
        _selectedIconKey = draft.IconKey;
        _selectedBrandColor = draft.BrandColor;
        _previewIcon.IconKey = draft.IconKey;
        _previewIcon.BrandColorHex = draft.BrandColor;
        _previewIcon.AppName = draft.AppName;

        _appNameEntry.Text = draft.AppName;
        _selectedCategory = draft.Category;
        _categoryValueLabel.Text = draft.Category;
        _categoryValueLabel.TextColor = UiHelper.TextPrimary;
        _tierEntry.Text = draft.TierName ?? string.Empty;
        _amountEntry.Text = (draft.AmountCents / 100m).ToString("0.00");

        _selectedCycle = draft.CycleType;
        _intervalEntry.Text = Math.Max(1, draft.CycleInterval).ToString();
        RenderCycleChips();

        _selectedNextDate = draft.NextBillingDate;
        _nextDateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedNextDate);

        _accountIdentifierEntry.Text = draft.AccountIdentifier ?? string.Empty;
        _accountNicknameEntry.Text = draft.AccountNickname ?? string.Empty;
        _selectedAccountId = draft.AccountId;
        var accDisplay = FormatHelpers.BuildAccountDetailLine(draft.AccountNickname, draft.AccountIdentifier);
        if (!string.IsNullOrWhiteSpace(accDisplay))
        {
            _accountQuickPickLabel.Text = accDisplay;
            _accountQuickPickLabel.TextColor = UiHelper.Primary;
        }

        _selectedChannelName = draft.PaymentChannelName;
        UpdateChannelValueDisplay();

        _noteEntry.Text = draft.Note ?? string.Empty;
        _remindBeforeEnabled = draft.RemindBeforeEnabled;
        _remindBeforeDays = Math.Max(1, draft.RemindBeforeDays);
        _remindOnDayEnabled = draft.RemindOnDayEnabled;
        _selectedRemindTime = draft.RemindTime;
        UpdateReminderVisuals();
    }

    private void UpdateCycleDependentVisibility()
    {
        _intervalRow.IsVisible = _selectedCycle is BillingCycleType.EveryNDays or BillingCycleType.EveryNMonths;
        _intervalUnitLabel.Text = _selectedCycle == BillingCycleType.EveryNDays ? "天" : "个月";
        _dateFieldLabel.Text = _selectedCycle == BillingCycleType.OneTime ? "到期日期" : "下次扣费日";
        _oneTimePaymentSection.IsVisible = !IsEditMode && _selectedCycle == BillingCycleType.OneTime;
    }

    private async Task HandleSaveAsync()
    {
        if (_saving) return;
        _saving = true;

        try
        {
            if (string.IsNullOrWhiteSpace(_appNameEntry.Text))
            {
                await ModernDialogPage.ShowAlertAsync(Navigation, "请完善信息", "请填写应用名称。");
                return;
            }

            if (!FormatHelpers.TryParseCnyToCents(_amountEntry.Text, out var amountCents))
            {
                await ModernDialogPage.ShowAlertAsync(Navigation, "金额格式有误", "请输入有效的人民币金额（大于等于 0，最多两位小数）。");
                return;
            }

            var interval = 1;
            if (_selectedCycle is BillingCycleType.EveryNDays or BillingCycleType.EveryNMonths)
            {
                if (!int.TryParse(_intervalEntry.Text?.Trim(), out interval) || interval < 1)
                {
                    await ModernDialogPage.ShowAlertAsync(Navigation, "间隔有误", "自定义周期间隔必须为正整数。");
                    return;
                }
            }

            long? oneTimePaidCents = null;
            if (!IsEditMode && _selectedCycle == BillingCycleType.OneTime && _recordOneTimeSwitch.IsToggled)
            {
                var raw = string.IsNullOrWhiteSpace(_oneTimePaidAmountEntry.Text) ? _amountEntry.Text : _oneTimePaidAmountEntry.Text;
                if (!FormatHelpers.TryParseCnyToCents(raw, out var parsed))
                {
                    await ModernDialogPage.ShowAlertAsync(Navigation, "实付金额有误", "请输入有效的本次实付金额。");
                    return;
                }
                oneTimePaidCents = parsed;
            }

            if (_remindBeforeEnabled || _remindOnDayEnabled)
            {
                await _scheduler.EnsureNotificationPermissionAsync();
            }

            var draft = new SubscriptionDraft
            {
                Id = IsEditMode ? _sourceSubscriptionId : null,
                AppName = _appNameEntry.Text.Trim(),
                Category = _selectedCategory,
                IconKey = _selectedIconKey,
                BrandColor = _selectedBrandColor,
                TierName = _tierEntry.Text,
                AmountCents = amountCents,
                CycleType = _selectedCycle,
                CycleInterval = interval,
                NextBillingDate = _selectedNextDate,
                AccountId = _selectedAccountId,
                AccountNickname = _accountNicknameEntry.Text,
                AccountIdentifier = _accountIdentifierEntry.Text,
                PaymentChannelName = _selectedChannelName,
                RemindBeforeEnabled = _remindBeforeEnabled,
                RemindBeforeDays = Math.Max(1, _remindBeforeDays),
                RemindOnDayEnabled = _remindOnDayEnabled,
                RemindTime = _selectedRemindTime,
                Note = _noteEntry.Text,
                RecordOneTimePayment = !IsEditMode && _selectedCycle == BillingCycleType.OneTime && _recordOneTimeSwitch.IsToggled,
                OneTimePaidAmountCents = oneTimePaidCents,
                OneTimePaidDate = _selectedOneTimePaidDate
            };

            await _ledger.SaveSubscriptionAsync(draft, enforceTodayOrFuture: true);
            await _scheduler.RescheduleAllAsync();
            _bus.NotifyDataChanged();
            await ModalCoordinator.PopModalAsync(this, animated: true);
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "保存失败", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private static Entry CreateCleanRightEntry(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        FontSize = 15,
        TextColor = UiHelper.TextPrimary,
        PlaceholderColor = UiHelper.TextMuted,
        HorizontalTextAlignment = TextAlignment.End,
        BackgroundColor = Colors.Transparent,
        VerticalOptions = LayoutOptions.Center
    };

    private static Label CreateSelectValueLabel(string text, bool isPlaceholder = false) => new()
    {
        Text = text,
        FontSize = 15,
        FontAttributes = FontAttributes.Bold,
        TextColor = isPlaceholder ? UiHelper.TextMuted : UiHelper.TextPrimary,
        HorizontalTextAlignment = TextAlignment.End,
        VerticalOptions = LayoutOptions.Center
    };

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = UiHelper.TextSecondary,
        Margin = new Thickness(6, 2, 0, -6)
    };

    private static View BuildInputRow(string label, View field)
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
            FontSize = 15,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        row.Add(field, 1, 0);
        return row;
    }

    private static Grid BuildDateButtonRow(Label titleLabel, Label valueLabel, Func<Task> onTap)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 10)
        };
        row.Add(titleLabel, 0, 0);

        var pill = new Border
        {
            BackgroundColor = Color.FromArgb("#F5F7FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(12, 7),
            Content = new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    valueLabel,
                    new Label { Text = "›", FontSize = 16, TextColor = UiHelper.TextMuted, VerticalOptions = LayoutOptions.Center }
                }
            }
        };
        row.Add(pill, 1, 0);
        UiHelper.AttachTapAsync(row, onTap);
        return row;
    }

    private static Grid BuildSelectRow(string label, Label valueLabel, Func<Task> onTap)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8,
            Padding = new Thickness(0, 13)
        };
        row.Add(new Label
        {
            Text = label,
            FontSize = 15,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        row.Add(valueLabel, 1, 0);
        row.Add(new Label
        {
            Text = "›",
            FontSize = 19,
            TextColor = UiHelper.TextMuted,
            VerticalOptions = LayoutOptions.Center
        }, 2, 0);

        UiHelper.AttachTapAsync(row, onTap);
        return row;
    }

    private static View BuildSwitchRow(string label, Switch sw)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 10)
        };
        row.Add(new Label
        {
            Text = label,
            FontSize = 15,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        row.Add(sw, 1, 0);
        return row;
    }
}
