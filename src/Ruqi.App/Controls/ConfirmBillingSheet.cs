using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Controls;

/// <summary>
/// 现代底部圆角「扣款确认」抽屉容器（零第三方兼容风险，带入场动画与遮罩点击关闭）。
/// </summary>
public sealed class ConfirmBillingSheet : Grid
{
    private readonly LedgerService _ledger;
    private readonly IReminderScheduler _scheduler;
    private readonly AppEventBus _bus;

    private readonly ContentPresenter _mainPresenter;
    private readonly Grid _overlayRoot;
    private readonly BoxView _backdrop;
    private readonly Border _sheetCard;

    private readonly AppIconBadge _iconBadge;
    private readonly Label _appNameLabel;
    private readonly Border _accountChip;
    private readonly Label _accountLabel;
    private readonly Label _periodLabel;
    private readonly Entry _amountEntry;
    private readonly Label _paidDateValueLabel;
    private readonly Label _errorLabel;
    private readonly Button _confirmButton;
    private readonly Button _skipButton;

    private string? _occurrenceId;
    private DateOnly _selectedPaidDate = DateOnly.FromDateTime(DateTime.Today);
    private bool _isOpening;
    private bool _isClosing;
    private bool _busy;

    private bool CanInteract => _overlayRoot.IsVisible && !_isOpening && !_isClosing && !_busy;

    public event EventHandler? Completed;

    public View? Content
    {
        get => _mainPresenter.Content;
        set => _mainPresenter.Content = value;
    }

    public ConfirmBillingSheet(LedgerService ledger, IReminderScheduler scheduler, AppEventBus bus)
    {
        _ledger = ledger;
        _scheduler = scheduler;
        _bus = bus;

        _mainPresenter = new ContentPresenter();
        Children.Add(_mainPresenter);

        _iconBadge = new AppIconBadge { WidthRequest = 44, HeightRequest = 44 };
        _appNameLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary
        };

        _accountLabel = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.Primary
        };
        _accountChip = new Border
        {
            BackgroundColor = UiHelper.PrimarySoft,
            StrokeThickness = 0,
            Padding = new Thickness(8, 2),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HorizontalOptions = LayoutOptions.Start,
            Content = _accountLabel
        };

        _periodLabel = new Label
        {
            FontSize = 12,
            TextColor = UiHelper.TextSecondary
        };

        _amountEntry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            Placeholder = "0.00",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalTextAlignment = TextAlignment.End,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 120
        };

        _paidDateValueLabel = new Label
        {
            Text = FormatHelpers.FormatFullDate(_selectedPaidDate),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        };

        _errorLabel = new Label
        {
            FontSize = 12,
            TextColor = UiHelper.DangerColor,
            IsVisible = false
        };

        _confirmButton = new Button
        {
            Text = "确认记账",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 15,
            HeightRequest = 48
        };
        _confirmButton.Clicked += OnConfirmClicked;

        _skipButton = new Button
        {
            Text = "本期未扣款",
            BackgroundColor = Color.FromArgb("#F2F4FA"),
            TextColor = UiHelper.TextSecondary,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            CornerRadius = 14,
            HeightRequest = 42
        };
        _skipButton.Clicked += OnSkipClicked;

        _backdrop = new BoxView
        {
            Color = Colors.Black.WithAlpha(0.42f),
            Opacity = 0
        };
        UiHelper.AttachTapAsync(_backdrop, DismissAsync);

        _sheetCard = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            VerticalOptions = LayoutOptions.End,
            TranslationY = 420,
            Content = BuildSheetContent()
        };

        _overlayRoot = new Grid
        {
            IsVisible = false,
            InputTransparent = false,
            Children = { _backdrop, _sheetCard }
        };

        Children.Add(_overlayRoot);
    }

    private View BuildSheetContent()
    {
        var closeBtn = UiHelper.CreateCircleIconButtonAsync(VectorIconKind.Close, DismissAsync, Color.FromArgb("#F1F4FA"), UiHelper.TextSecondary, 32, 1.9f);

        var titleBar = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        titleBar.Add(new Label
        {
            Text = "确认扣款",
            FontSize = 19,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        titleBar.Add(closeBtn, 1, 0);

        var infoGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            ],
            ColumnSpacing = 12
        };
        infoGrid.Add(_iconBadge, 0, 0);
        infoGrid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _appNameLabel,
                _accountChip,
                _periodLabel
            }
        }, 1, 0);

        var infoCard = new Border
        {
            BackgroundColor = Color.FromArgb("#F5F7FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Content = infoGrid
        };

        var amountRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 6)
        };
        amountRow.Add(new Label
        {
            Text = "实付金额（人民币）",
            FontSize = 15,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var amountBox = new Border
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
                    new Label
                    {
                        Text = "¥",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = UiHelper.Primary,
                        VerticalOptions = LayoutOptions.Center
                    },
                    _amountEntry
                }
            }
        };
        amountRow.Add(amountBox, 1, 0);

        var dateRow = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            Padding = new Thickness(0, 6)
        };
        dateRow.Add(new Label
        {
            Text = "实际扣款日",
            FontSize = 15,
            TextColor = UiHelper.TextSecondary,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        var datePill = new Border
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
                    _paidDateValueLabel,
                    new Label { Text = "›", FontSize = 16, TextColor = UiHelper.TextMuted, VerticalOptions = LayoutOptions.Center }
                }
            }
        };
        dateRow.Add(datePill, 1, 0);
        UiHelper.AttachTapAsync(dateRow, async () =>
        {
            if (!CanInteract) return;
            var nav = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (nav is null) return;

            _busy = true;
            try
            {
                var picked = await ModernDatePickerPage.PickAsync(nav, "选择实际扣款日", _selectedPaidDate);
                if (picked.HasValue)
                {
                    _selectedPaidDate = picked.Value;
                    _paidDateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedPaidDate);
                }
            }
            finally
            {
                _busy = false;
            }
        });

        return new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 20, 24),
            Spacing = 12,
            Children =
            {
                new BoxView
                {
                    WidthRequest = 38,
                    HeightRequest = 4,
                    CornerRadius = 2,
                    Color = Color.FromArgb("#D5DCEB"),
                    HorizontalOptions = LayoutOptions.Center
                },
                titleBar,
                infoCard,
                amountRow,
                UiHelper.CreateDivider(),
                dateRow,
                _errorLabel,
                _confirmButton,
                _skipButton
            }
        };
    }

    public async Task OpenAsync(string occurrenceId)
    {
        if (_isOpening || _isClosing || _busy || _overlayRoot.IsVisible) return;
        _isOpening = true;
        try
        {
            var occ = await _ledger.GetOccurrenceByIdAsync(occurrenceId);
            if (occ is null) return;

            if (occ.Status == OccurrenceStatus.Confirmed)
            {
                await ModernDialogPage.ShowAlertAsync(null, "已完成记账", "该期次此前已确认记账，无需重复操作。");
                return;
            }

            if (occ.Status == OccurrenceStatus.Skipped)
            {
                await ModernDialogPage.ShowAlertAsync(null, "已标记未扣款", "该期次此前已标记为本期未扣款。");
                return;
            }

            var sub = await _ledger.GetSubscriptionByIdAsync(occ.SubscriptionId);
            if (sub is null) return;

            _occurrenceId = occ.Id;
            _iconBadge.IconKey = sub.IconKey;
            _iconBadge.AppName = sub.AppName;
            _iconBadge.BrandColorHex = sub.BrandColor;
            _appNameLabel.Text = string.IsNullOrWhiteSpace(sub.TierName) ? sub.AppName : $"{sub.AppName} · {sub.TierName}";

            _accountChip.IsVisible = !string.IsNullOrWhiteSpace(sub.AccountDisplayCache);
            _accountLabel.Text = sub.AccountDisplayCache;
            _periodLabel.Text = $"{occ.ScheduledDate.Month}月期次 · 计划 {FormatHelpers.FormatMonthDay(occ.ScheduledDate)}";
            _amountEntry.Text = (occ.ExpectedAmountCents / 100m).ToString("0.00");
            _selectedPaidDate = occ.ScheduledDate;
            _paidDateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedPaidDate);
            _errorLabel.IsVisible = false;

            _overlayRoot.IsVisible = true;
            _backdrop.Opacity = 0;
            _sheetCard.TranslationY = 420;
            await Task.WhenAll(
                _backdrop.FadeToAsync(1, 180, Easing.CubicOut),
                _sheetCard.TranslateToAsync(0, 0, 220, Easing.CubicOut));
        }
        catch
        {
            _occurrenceId = null;
            _overlayRoot.IsVisible = false;
            throw;
        }
        finally
        {
            _isOpening = false;
        }
    }

    private Task DismissAsync() => CanInteract ? CloseAsync() : Task.CompletedTask;

    private async Task CloseAsync()
    {
        if (!_overlayRoot.IsVisible || _isClosing) return;
        _isClosing = true;
        try
        {
            await Task.WhenAll(
                _backdrop.FadeToAsync(0, 140, Easing.CubicIn),
                _sheetCard.TranslateToAsync(0, 420, 160, Easing.CubicIn));
        }
        finally
        {
            _occurrenceId = null;
            _overlayRoot.IsVisible = false;
            _isClosing = false;
        }
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (!CanInteract || string.IsNullOrEmpty(_occurrenceId)) return;

        if (!FormatHelpers.TryParseCnyToCents(_amountEntry.Text, out var cents))
        {
            _errorLabel.Text = "请输入有效的人民币金额（大于等于 0，最多两位小数）。";
            _errorLabel.IsVisible = true;
            return;
        }

        _busy = true;
        try
        {
            await _ledger.ConfirmOccurrenceAsync(_occurrenceId, cents, _selectedPaidDate);
            await _scheduler.RescheduleAllAsync();
            await CloseAsync();
            _bus.NotifyDataChanged();
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _errorLabel.Text = ex.Message;
            _errorLabel.IsVisible = true;
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        if (!CanInteract || string.IsNullOrEmpty(_occurrenceId)) return;

        _busy = true;
        try
        {
            await _ledger.SkipOccurrenceAsync(_occurrenceId);
            await _scheduler.RescheduleAllAsync();
            await CloseAsync();
            _bus.NotifyDataChanged();
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _errorLabel.Text = ex.Message;
            _errorLabel.IsVisible = true;
        }
        finally
        {
            _busy = false;
        }
    }
}
