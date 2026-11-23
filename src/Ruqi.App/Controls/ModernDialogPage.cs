using Microsoft.Maui.Controls.Shapes;
using Ruqi.Core.Services;

namespace Ruqi.App.Controls;

public sealed record AccountEditResult(string Identifier, string Nickname);
public sealed record BillingEditResult(long AmountCents, DateOnly PaidDate, string? Note);

/// <summary>
/// 全站统一的现代冷白弹窗与底部表单抽屉套件，彻底取代 Android 原生 AlertDialog / DisplayPromptAsync。
/// </summary>
public static class ModernDialogPage
{
    public static Task ShowAlertAsync(
        INavigation? navigation,
        string title,
        string message,
        string okText = "知道了",
        bool isDanger = false)
    {
        var nav = ResolveNav(navigation);
        if (nav is null) return Task.CompletedTask;

        var tcs = new TaskCompletionSource();
        var page = new CenteredAlertPage(title, message, okText, null, isDanger, _ => tcs.TrySetResult());
        return PushAndAwaitAsync(nav, page, tcs.Task);
    }

    public static Task<bool> ShowConfirmAsync(
        INavigation? navigation,
        string title,
        string message,
        string confirmText = "确定",
        string cancelText = "取消",
        bool isDanger = false)
    {
        var nav = ResolveNav(navigation);
        if (nav is null) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();
        var page = new CenteredAlertPage(title, message, confirmText, cancelText, isDanger, ok => tcs.TrySetResult(ok));
        return PushAndAwaitAsync(nav, page, tcs.Task);
    }

    public static Task<string?> ShowPromptAsync(
        INavigation? navigation,
        string title,
        string? subtitle,
        string placeholder,
        string? initialValue = null,
        string confirmText = "确定",
        Keyboard? keyboard = null)
    {
        var nav = ResolveNav(navigation);
        if (nav is null) return Task.FromResult<string?>(null);

        var tcs = new TaskCompletionSource<string?>();
        var page = new SinglePromptSheetPage(title, subtitle, placeholder, initialValue, confirmText, keyboard ?? Keyboard.Default, v => tcs.TrySetResult(v));
        return PushAndAwaitAsync(nav, page, tcs.Task);
    }

    public static Task<AccountEditResult?> ShowAccountEditorAsync(
        INavigation? navigation,
        string title,
        string? initialIdentifier = null,
        string? initialNickname = null)
    {
        var nav = ResolveNav(navigation);
        if (nav is null) return Task.FromResult<AccountEditResult?>(null);

        var tcs = new TaskCompletionSource<AccountEditResult?>();
        var page = new AccountEditorSheetPage(title, initialIdentifier, initialNickname, r => tcs.TrySetResult(r));
        return PushAndAwaitAsync(nav, page, tcs.Task);
    }

    public static Task<BillingEditResult?> ShowBillingEditorAsync(
        INavigation? navigation,
        string title,
        long initialAmountCents,
        DateOnly initialDate,
        string? initialNote,
        DateOnly? maxDate = null)
    {
        var nav = ResolveNav(navigation);
        if (nav is null) return Task.FromResult<BillingEditResult?>(null);

        var tcs = new TaskCompletionSource<BillingEditResult?>();
        var page = new BillingEditorSheetPage(title, initialAmountCents, initialDate, initialNote, maxDate, r => tcs.TrySetResult(r));
        return PushAndAwaitAsync(nav, page, tcs.Task);
    }

    private static INavigation? ResolveNav(INavigation? nav)
        => nav ?? Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    private static async Task PushAndAwaitAsync(INavigation nav, ContentPage page, Task task)
    {
        await nav.PushModalAsync(page, animated: false);
        await task;
    }

    private static async Task<T> PushAndAwaitAsync<T>(INavigation nav, ContentPage page, Task<T> task)
    {
        await nav.PushModalAsync(page, animated: false);
        return await task;
    }

    private sealed class CenteredAlertPage : ContentPage
    {
        private readonly Action<bool> _complete;
        private bool _closed;

        public CenteredAlertPage(string title, string message, string confirmText, string? cancelText, bool isDanger, Action<bool> complete)
        {
            _complete = complete;
            BackgroundColor = Colors.Black.WithAlpha(0.42f);
            Shell.SetNavBarIsVisible(this, false);

            var primaryBtn = new Button
            {
                Text = confirmText,
                BackgroundColor = isDanger ? UiHelper.DangerColor : UiHelper.Primary,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                CornerRadius = 14,
                HeightRequest = 46
            };
            primaryBtn.Clicked += async (_, _) => await CloseAsync(true);

            View buttonsView;
            if (string.IsNullOrEmpty(cancelText))
            {
                buttonsView = primaryBtn;
            }
            else
            {
                var cancelBtn = new Button
                {
                    Text = cancelText,
                    BackgroundColor = Color.FromArgb("#F1F4FA"),
                    TextColor = UiHelper.TextSecondary,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    CornerRadius = 14,
                    HeightRequest = 46
                };
                cancelBtn.Clicked += async (_, _) => await CloseAsync(false);

                var row = new Grid
                {
                    ColumnDefinitions =
                    [
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    ],
                    ColumnSpacing = 10
                };
                row.Add(cancelBtn, 0, 0);
                row.Add(primaryBtn, 1, 0);
                buttonsView = row;
            }

            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E6ECF8"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                Padding = new Thickness(22, 22, 22, 18),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                MaximumWidthRequest = 340,
                Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 8), Radius = 24, Opacity = 0.14f },
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = title,
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = UiHelper.TextPrimary,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new ScrollView
                        {
                            MaximumHeightRequest = 320,
                            Content = new Label
                            {
                                Text = message,
                                FontSize = 14,
                                LineHeight = 1.45,
                                TextColor = UiHelper.TextSecondary,
                                HorizontalTextAlignment = message.Contains('\n') ? TextAlignment.Start : TextAlignment.Center
                            }
                        },
                        new ContentView { Margin = new Thickness(0, 6, 0, 0), Content = buttonsView }
                    }
                }
            };

            var backdrop = new BoxView { Color = Colors.Transparent };
            UiHelper.AttachTapAsync(backdrop, () => CloseAsync(false));

            Content = new Grid
            {
                Padding = new Thickness(24),
                Children = { backdrop, card }
            };
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(false);
            return true;
        }

        private async Task CloseAsync(bool result)
        {
            if (_closed) return;
            _closed = true;
            _complete(result);
            try { await Navigation.PopModalAsync(animated: false); } catch { }
        }
    }

    private sealed class SinglePromptSheetPage : ContentPage
    {
        private readonly Action<string?> _complete;
        private readonly Entry _entry;
        private bool _closed;

        public SinglePromptSheetPage(string title, string? subtitle, string placeholder, string? initialValue, string confirmText, Keyboard keyboard, Action<string?> complete)
        {
            _complete = complete;
            BackgroundColor = Colors.Black.WithAlpha(0.42f);
            Shell.SetNavBarIsVisible(this, false);

            _entry = new Entry
            {
                Text = initialValue ?? string.Empty,
                Placeholder = placeholder,
                Keyboard = keyboard,
                FontSize = 16,
                TextColor = UiHelper.TextPrimary,
                PlaceholderColor = UiHelper.TextMuted,
                BackgroundColor = Colors.Transparent,
                ClearButtonVisibility = ClearButtonVisibility.WhileEditing
            };

            var header = BuildSheetHeader(title, subtitle, () => _ = CloseAsync(null));

            var inputBox = new Border
            {
                BackgroundColor = Color.FromArgb("#F5F7FC"),
                Stroke = Color.FromArgb("#E4E9F5"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(14, 10),
                Content = _entry
            };

            var footer = BuildSheetFooter("取消", confirmText, () => _ = CloseAsync(null), () => _ = CloseAsync(_entry.Text?.Trim() ?? string.Empty));

            Content = WrapBottomSheet(() => _ = CloseAsync(null), header, inputBox, footer);
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(string? result)
        {
            if (_closed) return;
            _closed = true;
            _complete(result);
            try { await Navigation.PopModalAsync(animated: false); } catch { }
        }
    }

    private sealed class AccountEditorSheetPage : ContentPage
    {
        private readonly Action<AccountEditResult?> _complete;
        private readonly Entry _identifierEntry;
        private readonly Entry _nicknameEntry;
        private readonly Label _errorLabel;
        private bool _closed;

        public AccountEditorSheetPage(string title, string? initialIdentifier, string? initialNickname, Action<AccountEditResult?> complete)
        {
            _complete = complete;
            BackgroundColor = Colors.Black.WithAlpha(0.42f);
            Shell.SetNavBarIsVisible(this, false);

            var header = BuildSheetHeader(title, "跨所有应用通用，新增订阅时可一键选择", () => _ = CloseAsync(null));

            _identifierEntry = new Entry
            {
                Text = initialIdentifier ?? string.Empty,
                Placeholder = "如 13812348000 或 work@outlook.com",
                FontSize = 15,
                TextColor = UiHelper.TextPrimary,
                PlaceholderColor = UiHelper.TextMuted,
                BackgroundColor = Colors.Transparent,
                ClearButtonVisibility = ClearButtonVisibility.WhileEditing
            };

            _nicknameEntry = new Entry
            {
                Text = initialNickname ?? string.Empty,
                Placeholder = "如 个人手机、工作邮箱（选填）",
                FontSize = 15,
                TextColor = UiHelper.TextPrimary,
                PlaceholderColor = UiHelper.TextMuted,
                BackgroundColor = Colors.Transparent,
                ClearButtonVisibility = ClearButtonVisibility.WhileEditing
            };

            _errorLabel = new Label
            {
                FontSize = 12,
                TextColor = UiHelper.DangerColor,
                IsVisible = false
            };

            var fields = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    BuildLabeledField("手机号 / 邮箱 / 账号", _identifierEntry),
                    BuildLabeledField("账号简称（选填）", _nicknameEntry),
                    _errorLabel
                }
            };

            var footer = BuildSheetFooter("取消", "保存账号", () => _ = CloseAsync(null), () =>
            {
                var id = _identifierEntry.Text?.Trim() ?? string.Empty;
                var nick = _nicknameEntry.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(nick))
                {
                    _errorLabel.Text = "请至少填写手机号/邮箱或账号简称。";
                    _errorLabel.IsVisible = true;
                    return;
                }
                _ = CloseAsync(new AccountEditResult(id, nick));
            });

            Content = WrapBottomSheet(() => _ = CloseAsync(null), header, fields, footer);
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(AccountEditResult? result)
        {
            if (_closed) return;
            _closed = true;
            _complete(result);
            try { await Navigation.PopModalAsync(animated: false); } catch { }
        }
    }

    private sealed class BillingEditorSheetPage : ContentPage
    {
        private readonly Action<BillingEditResult?> _complete;
        private readonly Entry _amountEntry;
        private readonly Label _dateValueLabel;
        private readonly Entry _noteEntry;
        private readonly Label _errorLabel;
        private DateOnly _selectedDate;
        private bool _closed;

        public BillingEditorSheetPage(
            string title,
            long initialAmountCents,
            DateOnly initialDate,
            string? initialNote,
            DateOnly? maxDate,
            Action<BillingEditResult?> complete)
        {
            _complete = complete;
            _selectedDate = initialDate;
            BackgroundColor = Colors.Black.WithAlpha(0.42f);
            Shell.SetNavBarIsVisible(this, false);

            var header = BuildSheetHeader(title, "账单变更后将立即同步更新支出统计", () => _ = CloseAsync(null));

            _amountEntry = new Entry
            {
                Text = (initialAmountCents / 100m).ToString("0.00"),
                Placeholder = "0.00",
                Keyboard = Keyboard.Numeric,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.TextPrimary,
                PlaceholderColor = UiHelper.TextMuted,
                BackgroundColor = Colors.Transparent,
                HorizontalTextAlignment = TextAlignment.End
            };

            var amountField = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                ],
                ColumnSpacing = 8
            };
            amountField.Add(new Label { Text = "¥", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.Primary, VerticalOptions = LayoutOptions.Center }, 0, 0);
            amountField.Add(_amountEntry, 1, 0);

            _dateValueLabel = new Label
            {
                Text = FormatHelpers.FormatFullDate(_selectedDate),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiHelper.TextPrimary,
                VerticalOptions = LayoutOptions.Center
            };

            var dateRow = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ]
            };
            dateRow.Add(_dateValueLabel, 0, 0);
            dateRow.Add(new VectorIconView { Kind = VectorIconKind.ChevronRight, IconColor = UiHelper.TextMuted, WidthRequest = 14, HeightRequest = 14 }, 1, 0);

            var dateBox = new Border
            {
                BackgroundColor = Color.FromArgb("#F5F7FC"),
                Stroke = Color.FromArgb("#E4E9F5"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(14, 12),
                Content = dateRow
            };
            UiHelper.AttachTapAsync(dateBox, async () =>
            {
                var picked = await ModernDatePickerPage.PickAsync(Navigation, "选择实际扣款日", _selectedDate, maxDate: maxDate);
                if (picked.HasValue)
                {
                    _selectedDate = picked.Value;
                    _dateValueLabel.Text = FormatHelpers.FormatFullDate(_selectedDate);
                }
            });

            _noteEntry = new Entry
            {
                Text = initialNote ?? string.Empty,
                Placeholder = "如：手工补录、活动优惠（选填）",
                FontSize = 14,
                TextColor = UiHelper.TextPrimary,
                PlaceholderColor = UiHelper.TextMuted,
                BackgroundColor = Colors.Transparent
            };

            _errorLabel = new Label
            {
                FontSize = 12,
                TextColor = UiHelper.DangerColor,
                IsVisible = false
            };

            var fields = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    BuildLabeledField("实付金额（人民币）", amountField),
                    new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            new Label { Text = "实际扣款日", FontSize = 13, TextColor = UiHelper.TextSecondary },
                            dateBox
                        }
                    },
                    BuildLabeledField("备注（选填）", _noteEntry),
                    _errorLabel
                }
            };

            var footer = BuildSheetFooter("取消", "保存账单", () => _ = CloseAsync(null), () =>
            {
                if (!FormatHelpers.TryParseCnyToCents(_amountEntry.Text, out var cents))
                {
                    _errorLabel.Text = "请输入有效的人民币金额（大于等于 0，最多两位小数）。";
                    _errorLabel.IsVisible = true;
                    return;
                }

                _ = CloseAsync(new BillingEditResult(cents, _selectedDate, _noteEntry.Text?.Trim()));
            });

            Content = WrapBottomSheet(() => _ = CloseAsync(null), header, fields, footer);
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(BillingEditResult? result)
        {
            if (_closed) return;
            _closed = true;
            _complete(result);
            try { await Navigation.PopModalAsync(animated: false); } catch { }
        }
    }

    private static View BuildSheetHeader(string title, string? subtitle, Action onClose)
    {
        var header = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 12
        };

        var titleStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary }
            }
        };
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            titleStack.Add(new Label { Text = subtitle, FontSize = 12, TextColor = UiHelper.TextMuted });
        }

        header.Add(titleStack, 0, 0);
        header.Add(UiHelper.CreateCircleIconButton(VectorIconKind.Close, onClose, Color.FromArgb("#F1F4FA"), UiHelper.TextSecondary, 32, 1.9f), 1, 0);
        return header;
    }

    private static View BuildLabeledField(string label, View innerControl) => new VerticalStackLayout
    {
        Spacing = 6,
        Children =
        {
            new Label { Text = label, FontSize = 13, TextColor = UiHelper.TextSecondary },
            new Border
            {
                BackgroundColor = Color.FromArgb("#F5F7FC"),
                Stroke = Color.FromArgb("#E4E9F5"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(14, 10),
                Content = innerControl
            }
        }
    };

    private static View BuildSheetFooter(string cancelText, string confirmText, Action onCancel, Action onConfirm)
    {
        var cancelBtn = new Button
        {
            Text = cancelText,
            BackgroundColor = Color.FromArgb("#F1F4FA"),
            TextColor = UiHelper.TextSecondary,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 46
        };
        cancelBtn.Clicked += (_, _) => onCancel();

        var confirmBtn = new Button
        {
            Text = confirmText,
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 46
        };
        confirmBtn.Clicked += (_, _) => onConfirm();

        var row = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(2, GridUnitType.Star))
            ],
            ColumnSpacing = 10,
            Margin = new Thickness(0, 6, 0, 0)
        };
        row.Add(cancelBtn, 0, 0);
        row.Add(confirmBtn, 1, 0);
        return row;
    }

    private static Grid WrapBottomSheet(Action onBackdropTap, params View[] children)
    {
        var root = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ]
        };

        var backdrop = new BoxView { Color = Colors.Transparent };
        UiHelper.AttachTap(backdrop, onBackdropTap);
        root.Add(backdrop, 0, 0);

        var contentStack = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 20, 22),
            Spacing = 14
        };
        contentStack.Add(new BoxView
        {
            WidthRequest = 38,
            HeightRequest = 4,
            CornerRadius = 2,
            Color = Color.FromArgb("#D5DCEB"),
            HorizontalOptions = LayoutOptions.Center
        });
        foreach (var child in children)
        {
            contentStack.Add(child);
        }

        var sheet = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = contentStack
        };

        root.Add(sheet, 0, 1);
        return root;
    }
}
