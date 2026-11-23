using Microsoft.Maui.Controls.Shapes;
using Ruqi.App.Controls;

namespace Ruqi.App.Pages;

/// <summary>
/// 品牌启动动画页 + 首次启动必须手动确认的隐私与本地数据说明门禁。
/// </summary>
public sealed class SplashLaunchPage : ContentPage
{
    public const string PrivacyAcceptedKey = "ruqi_privacy_accepted_v1";

    private readonly Func<Task> _onEnterApp;
    private readonly Border _logoCard;
    private readonly VerticalStackLayout _brandTextStack;
    private readonly Border _progressBarFill;
    private readonly Grid _privacyOverlay;
    private readonly Border _privacyCard;

    private bool _animationStarted;

    public static bool HasAcceptedPrivacy => Preferences.Default.Get(PrivacyAcceptedKey, false);

    public SplashLaunchPage(Func<Task> onEnterApp)
    {
        _onEnterApp = onEnterApp;

        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#F2F6FE");

        // 品牌 Logo 卡片
        _logoCard = new Border
        {
            WidthRequest = 112,
            HeightRequest = 112,
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E2E9FA"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            HorizontalOptions = LayoutOptions.Center,
            Opacity = 0,
            Scale = 0.80,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#5B6CF9"),
                Offset = new Point(0, 10),
                Radius = 24,
                Opacity = 0.14f
            },
            Content = new Image
            {
                Source = "splash.png",
                Aspect = Aspect.AspectFit,
                WidthRequest = 86,
                HeightRequest = 86,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        _brandTextStack = new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            Opacity = 0,
            TranslationY = 16,
            Children =
            {
                new Label
                {
                    Text = "如期",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = "续费如期，心中有数",
                    FontSize = 14,
                    TextColor = UiHelper.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        _progressBarFill = new Border
        {
            HeightRequest = 4,
            WidthRequest = 12,
            BackgroundColor = UiHelper.Primary,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 2 },
            HorizontalOptions = LayoutOptions.Start
        };

        var progressTrack = new Border
        {
            WidthRequest = 120,
            HeightRequest = 4,
            BackgroundColor = Color.FromArgb("#DCE3F7"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 2 },
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 18, 0, 0),
            Content = _progressBarFill
        };

        var centerBrand = new VerticalStackLayout
        {
            Spacing = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                _logoCard,
                _brandTextStack,
                progressTrack
            }
        };

        var footerHint = new Label
        {
            Text = "纯本地离线 · 无需登录",
            FontSize = 12,
            TextColor = UiHelper.TextMuted,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 32)
        };

        // 首次启动隐私说明确认弹窗（不可点遮罩跳过）
        _privacyCard = BuildPrivacyCard(readOnly: false, onAccept: HandleAcceptPrivacyAsync, onDecline: HandleDeclinePrivacy);
        _privacyCard.Opacity = 0;
        _privacyCard.TranslationY = 36;

        _privacyOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Colors.Black.WithAlpha(0.45f),
            Padding = new Thickness(20, 24),
            Children = { _privacyCard }
        };

        Content = new Grid
        {
            Children =
            {
                centerBrand,
                footerHint,
                _privacyOverlay
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_animationStarted) return;
        _animationStarted = true;

        await RunIntroAnimationAsync();

        if (HasAcceptedPrivacy)
        {
            await EnterMainAppSmoothlyAsync();
        }
        else
        {
            await ShowPrivacyGateAsync();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // 未同意隐私说明前按返回键直接退出，不允许绕过进入 App
        if (!HasAcceptedPrivacy)
        {
            Application.Current?.Quit();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async Task RunIntroAnimationAsync()
    {
        await Task.Delay(60);

        await Task.WhenAll(
            _logoCard.FadeToAsync(1, 380, Easing.CubicOut),
            _logoCard.ScaleToAsync(1.0, 420, Easing.SpringOut),
            _brandTextStack.FadeToAsync(1, 380, Easing.CubicOut),
            _brandTextStack.TranslateToAsync(0, 0, 400, Easing.CubicOut));

        var anim = new Animation(v => _progressBarFill.WidthRequest = v, 12, 120, Easing.CubicInOut);
        var tcs = new TaskCompletionSource();
        anim.Commit(this, "SplashBar", 16, 460, finished: (_, _) => tcs.TrySetResult());
        await tcs.Task;
        await Task.Delay(80);
    }

    private async Task ShowPrivacyGateAsync()
    {
        _privacyOverlay.IsVisible = true;
        await Task.WhenAll(
            _privacyCard.FadeToAsync(1, 240, Easing.CubicOut),
            _privacyCard.TranslateToAsync(0, 0, 260, Easing.CubicOut));
    }

    private async Task HandleAcceptPrivacyAsync()
    {
        Preferences.Default.Set(PrivacyAcceptedKey, true);
        await Task.WhenAll(
            _privacyCard.FadeToAsync(0, 160, Easing.CubicIn),
            _privacyOverlay.FadeToAsync(0, 180, Easing.CubicIn));
        _privacyOverlay.IsVisible = false;
        await EnterMainAppSmoothlyAsync();
    }

    private void HandleDeclinePrivacy()
    {
        Application.Current?.Quit();
    }

    private async Task EnterMainAppSmoothlyAsync()
    {
        await this.FadeToAsync(0, 180, Easing.CubicIn);
        await _onEnterApp();
    }

    public static async Task ShowReadOnlyPrivacyAsync(INavigation navigation)
    {
        var modal = new ContentPage
        {
            BackgroundColor = Colors.Black.WithAlpha(0.45f)
        };
        Shell.SetNavBarIsVisible(modal, false);

        var card = BuildPrivacyCard(
            readOnly: true,
            onAccept: async () => await navigation.PopModalAsync(animated: false),
            onDecline: () => { });

        modal.Content = new Grid
        {
            Padding = new Thickness(20, 24),
            Children = { card }
        };

        await navigation.PushModalAsync(modal, animated: false);
    }

    private static Border BuildPrivacyCard(bool readOnly, Func<Task> onAccept, Action onDecline)
    {
        var points = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                BuildPrivacyBullet("1. 纯本地离线存储", "你的全部订阅、常用账号标识、金额与扣款记录仅保存在本机数据库中。无需注册登录、无云端服务器、不会联网上传或收集任何个人信息。"),
                BuildPrivacyBullet("2. 最小必要权限", "仅在你启用提醒时按需申请「通知权限」，用于本机定时提醒续费；仅在你主动导出或恢复备份时调用系统「文件」选择器。"),
                BuildPrivacyBullet("3. 账本工具声明", "本应用仅提供本地记账与到期提醒服务，不会自动扣款，也无法代替你取消外部平台的自动续费；取消续费请前往对应原付款渠道操作。")
            }
        };

        var buttons = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };

        var acceptBtn = new Button
        {
            Text = readOnly ? "我知道了" : "同意并进入如期",
            BackgroundColor = UiHelper.Primary,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            CornerRadius = 14,
            HeightRequest = 48
        };
        acceptBtn.Clicked += async (_, _) => await onAccept();
        buttons.Add(acceptBtn);

        if (!readOnly)
        {
            var declineBtn = new Button
            {
                Text = "暂不同意并退出",
                BackgroundColor = Color.FromArgb("#F1F4FA"),
                TextColor = UiHelper.TextSecondary,
                FontSize = 14,
                CornerRadius = 14,
                HeightRequest = 42
            };
            declineBtn.Clicked += (_, _) => onDecline();
            buttons.Add(declineBtn);
        }

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E4EAF8"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(20, 22),
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 380,
            Content = new ScrollView
            {
                MaximumHeightRequest = 520,
                Content = new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        new Label
                        {
                            Text = "隐私与本地数据说明",
                            FontSize = 19,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = UiHelper.TextPrimary,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = readOnly
                                ? "「如期」始终坚持纯本地、免登录、零数据上传的设计原则："
                                : "欢迎使用「如期」。进入应用前，请阅读并确认以下隐私与数据说明：",
                            FontSize = 13,
                            TextColor = UiHelper.TextSecondary
                        },
                        points,
                        buttons
                    }
                }
            }
        };
    }

    private static View BuildPrivacyBullet(string title, string detail) => new Border
    {
        BackgroundColor = Color.FromArgb("#F6F8FD"),
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        Padding = new Thickness(12, 10),
        Content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = UiHelper.TextPrimary
                },
                new Label
                {
                    Text = detail,
                    FontSize = 12.5,
                    LineHeight = 1.35,
                    TextColor = UiHelper.TextSecondary
                }
            }
        }
    };
}
