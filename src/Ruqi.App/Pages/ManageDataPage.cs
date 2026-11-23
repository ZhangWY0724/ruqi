using Ruqi.App.Controls;
using Ruqi.App.Services;
using Ruqi.Core.Models;
using Ruqi.Core.Services;

namespace Ruqi.App.Pages;

public enum ManageDataMode
{
    Catalog,
    Accounts,
    Channels
}

public sealed class ManageDataPage : ContentPage
{
    private readonly ManageDataMode _mode;
    private readonly LedgerService _ledger;
    private readonly AppEventBus _bus;
    private readonly VerticalStackLayout _listStack;

    public ManageDataPage(ManageDataMode mode, LedgerService ledger, AppEventBus bus)
    {
        _mode = mode;
        _ledger = ledger;
        _bus = bus;

        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = UiHelper.PageBg;

        var pageTitle = mode switch
        {
            ManageDataMode.Catalog => "预设与自定义目录",
            ManageDataMode.Accounts => "常用使用账号",
            ManageDataMode.Channels => "付款渠道管理",
            _ => "管理"
        };

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
            Text = pageTitle,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiHelper.TextPrimary,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        if (_mode == ManageDataMode.Channels)
        {
            topBar.Add(UiHelper.CreateCircleIconButton(VectorIconKind.Plus, async () => await AddChannelAsync(), Colors.White, UiHelper.Primary, 36, 2.0f), 2, 0);
        }
        else if (_mode == ManageDataMode.Accounts)
        {
            topBar.Add(UiHelper.CreateCircleIconButton(VectorIconKind.Plus, async () => await AddAccountAsync(), Colors.White, UiHelper.Primary, 36, 2.0f), 2, 0);
        }
        else
        {
            // 占位保持标题严格居中
            topBar.Add(new BoxView { WidthRequest = 36, HeightRequest = 36, Color = Colors.Transparent }, 2, 0);
        }

        _listStack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 6, 16, 32),
            Spacing = 10
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
        pageGrid.Add(new ScrollView { Content = _listStack }, 0, 1);

        Content = pageGrid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _listStack.Children.Clear();

        switch (_mode)
        {
            case ManageDataMode.Catalog:
                await RenderCatalogAsync();
                break;
            case ManageDataMode.Accounts:
                await RenderAccountsAsync();
                break;
            case ManageDataMode.Channels:
                await RenderChannelsAsync();
                break;
        }
    }

    private async Task RenderCatalogAsync()
    {
        var custom = await _ledger.GetCustomCatalogItemsAsync();
        var all = await _ledger.SearchCatalogAsync();
        var builtIn = all.Where(x => x.IsBuiltIn).ToList();

        _listStack.Add(new Label { Text = $"本机自定义目录 ({custom.Count})", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextSecondary });
        if (custom.Count == 0)
        {
            _listStack.Add(UiHelper.CreateCard(new Label { Text = "手动新增过的应用与档位会自动沉淀到这里供复用", FontSize = 13, TextColor = UiHelper.TextMuted }, new Thickness(14, 14), 14));
        }
        else
        {
            foreach (var item in custom)
            {
                var row = BuildCatalogRow(item, canDelete: true);
                _listStack.Add(UiHelper.CreateCard(row, new Thickness(14, 10), 14));
            }
        }

        _listStack.Add(new Label { Text = $"内置离线目录 ({builtIn.Count})", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextSecondary, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var item in builtIn)
        {
            var row = BuildCatalogRow(item, canDelete: false);
            _listStack.Add(UiHelper.CreateCard(row, new Thickness(14, 10), 14));
        }
    }

    private View BuildCatalogRow(CatalogItem item, bool canDelete)
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

        grid.Add(new AppIconBadge { IconKey = item.IconKey, AppName = item.AppName, BrandColorHex = item.BrandColor, WidthRequest = 34, HeightRequest = 34 }, 0, 0);
        grid.Add(new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = string.IsNullOrWhiteSpace(item.TierName) ? item.AppName : $"{item.AppName} · {item.TierName}", FontSize = 14, FontAttributes = FontAttributes.Bold },
                new Label { Text = item.Category, FontSize = 12, TextColor = UiHelper.TextMuted }
            }
        }, 1, 0);

        var right = new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = item.ReferencePriceCents.HasValue ? FormatHelpers.FormatAmountWithCycle(item.ReferencePriceCents.Value, item.DefaultCycleType, item.DefaultInterval) : "无参考价",
                    FontSize = 13,
                    TextColor = UiHelper.TextSecondary
                }
            }
        };

        if (canDelete)
        {
            var del = new Label { Text = "删除", FontSize = 13, TextColor = UiHelper.DangerColor };
            UiHelper.AttachTapAsync(del, async () =>
            {
                await _ledger.DeleteCustomCatalogItemAsync(item.Id);
                await RefreshAsync();
            });
            right.Add(del);
        }

        grid.Add(right, 2, 0);
        return grid;
    }

    private async Task RenderAccountsAsync()
    {
        var accounts = await _ledger.GetAccountsAsync();
        if (accounts.Count == 0)
        {
            _listStack.Add(UiHelper.CreateCard(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = "还没有常用开通账号", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "提前录入你的常用手机号或邮箱，新增订阅时即可一键快选「开通在哪个账号上」", FontSize = 13, TextColor = UiHelper.TextMuted, HorizontalTextAlignment = TextAlignment.Center }
                }
            }, new Thickness(18, 22), 16));
            return;
        }

        _listStack.Add(new Label
        {
            Text = "常用手机号或邮箱（跨所有应用通用，新增订阅时可一键选择）",
            FontSize = 12,
            TextColor = UiHelper.TextMuted,
            Margin = new Thickness(4, 0, 4, 2)
        });

        foreach (var acc in accounts)
        {
            var maskedId = FormatHelpers.MaskIdentifier(acc.Identifier);
            var primaryText = !string.IsNullOrEmpty(maskedId)
                ? maskedId
                : (acc.Nickname ?? "未命名账号");

            var kindTag = string.IsNullOrWhiteSpace(acc.Identifier)
                ? "账号简称"
                : acc.Identifier.Contains('@') ? "邮箱" : acc.Identifier.All(char.IsDigit) ? "手机号" : "账号";

            var subtitleText = !string.IsNullOrWhiteSpace(acc.Nickname) && !string.IsNullOrEmpty(maskedId)
                ? $"{acc.Nickname} · {kindTag}"
                : kindTag;

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ],
                ColumnSpacing = 12
            };

            grid.Add(new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = primaryText, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = UiHelper.TextPrimary },
                    new Label { Text = subtitleText, FontSize = 12, TextColor = UiHelper.TextSecondary }
                }
            }, 0, 0);

            var captured = acc;
            var actions = new HorizontalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center };

            var editBtn = new Label { Text = "编辑", FontSize = 13, TextColor = UiHelper.Primary };
            UiHelper.AttachTapAsync(editBtn, async () => await EditAccountAsync(captured));
            actions.Add(editBtn);

            var delBtn = new Label { Text = "删除", FontSize = 13, TextColor = UiHelper.DangerColor };
            UiHelper.AttachTapAsync(delBtn, async () =>
            {
                var ok = await ModernDialogPage.ShowConfirmAsync(Navigation, "删除常用账号", $"确定删除「{primaryText}」吗？已有订阅仍会保留原显示信息。", confirmText: "删除", isDanger: true);
                if (!ok) return;
                await _ledger.DeleteAccountAsync(captured.Id);
                _bus.NotifyDataChanged();
                await RefreshAsync();
            });
            actions.Add(delBtn);

            grid.Add(actions, 1, 0);
            _listStack.Add(UiHelper.CreateCard(grid, new Thickness(16, 13), 16));
        }
    }

    private async Task RenderChannelsAsync()
    {
        var channels = await _ledger.GetPaymentChannelsAsync(includeArchived: true);
        foreach (var ch in channels)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ]
            };

            grid.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = ch.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = ch.IsArchived ? UiHelper.TextMuted : UiHelper.TextPrimary },
                    UiHelper.CreatePill(ch.IsBuiltIn ? "内置" : "自定义", ch.IsBuiltIn ? Color.FromArgb("#F0F2F7") : UiHelper.PrimarySoft, ch.IsBuiltIn ? UiHelper.TextMuted : UiHelper.Primary)
                }
            }, 0, 0);

            var actions = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };
            var captured = ch;
            if (!ch.IsBuiltIn)
            {
                var rename = new Label { Text = "重命名", FontSize = 13, TextColor = UiHelper.Primary };
                UiHelper.AttachTapAsync(rename, async () =>
                {
                    var name = await ModernDialogPage.ShowPromptAsync(Navigation, "重命名付款渠道", "修改后将用于后续新增订阅选择", "请输入新名称", initialValue: captured.Name, confirmText: "保存");
                    if (string.IsNullOrWhiteSpace(name)) return;
                    await _ledger.RenamePaymentChannelAsync(captured.Id, name);
                    _bus.NotifyDataChanged();
                    await RefreshAsync();
                });
                actions.Add(rename);

                var del = new Label { Text = "删除", FontSize = 13, TextColor = UiHelper.DangerColor };
                UiHelper.AttachTapAsync(del, async () =>
                {
                    await _ledger.DeleteCustomPaymentChannelAsync(captured.Id);
                    _bus.NotifyDataChanged();
                    await RefreshAsync();
                });
                actions.Add(del);
            }

            grid.Add(actions, 1, 0);
            _listStack.Add(UiHelper.CreateCard(grid, new Thickness(14, 12), 14));
        }
    }

    private async Task AddChannelAsync()
    {
        var name = await ModernDialogPage.ShowPromptAsync(Navigation, "新增付款渠道", "添加你的常用扣款方式", "如：招商银行信用卡、PayPal", confirmText: "添加");
        if (string.IsNullOrWhiteSpace(name)) return;
        await _ledger.AddCustomPaymentChannelAsync(name);
        _bus.NotifyDataChanged();
        await RefreshAsync();
    }

    private async Task AddAccountAsync()
    {
        var edited = await ModernDialogPage.ShowAccountEditorAsync(Navigation, "新增常用账号");
        if (edited is null) return;

        try
        {
            await _ledger.SaveAccountAsync(new AccountProfile
            {
                AppName = string.Empty,
                Identifier = edited.Identifier,
                Nickname = edited.Nickname
            });
            _bus.NotifyDataChanged();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "保存失败", ex.Message);
        }
    }

    private async Task EditAccountAsync(AccountProfile acc)
    {
        var edited = await ModernDialogPage.ShowAccountEditorAsync(Navigation, "编辑常用账号", acc.Identifier, acc.Nickname);
        if (edited is null) return;

        try
        {
            acc.Identifier = edited.Identifier;
            acc.Nickname = edited.Nickname;
            acc.AppName = string.Empty;
            await _ledger.SaveAccountAsync(acc);
            _bus.NotifyDataChanged();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await ModernDialogPage.ShowAlertAsync(Navigation, "保存失败", ex.Message);
        }
    }
}
