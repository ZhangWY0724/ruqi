namespace Ruqi.App.Controls;

/// <summary>
/// 全站统一的模态页面协调器：
/// 1. 使用单一串行锁协调所有 PushModalAsync / PopModalAsync 转场，避免连续或并发弹窗撞车；
/// 2. 仅当目标页位于当前模态栈顶时才弹出，防止误弹上层子弹窗或底层父页面；
/// 3. 出栈后保留短暂原生 UI 帧缓冲，再释放锁并唤醒后续弹窗，兼容 Android / Windows。
/// </summary>
public static class ModalCoordinator
{
    private static readonly SemaphoreSlim TransitionLock = new(1, 1);

    public static INavigation? ResolveNavigation(INavigation? preferred = null)
        => preferred
           ?? Shell.Current?.Navigation
           ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public static async Task<bool> PushModalAsync(INavigation? preferredNavigation, Page page, bool animated = false)
    {
        var nav = ResolveNavigation(preferredNavigation);
        if (nav is null) return false;

        await TransitionLock.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await nav.PushModalAsync(page, animated);
            });
            return true;
        }
        finally
        {
            TransitionLock.Release();
        }
    }

    /// <summary>
    /// 安全弹出指定模态页。
    /// 返回 true 表示目标页已离开模态栈；返回 false 表示目标页仍在栈中但被上层页面遮挡，本次未执行弹出。
    /// </summary>
    public static async Task<bool> PopModalAsync(Page modalPage, bool animated = false)
    {
        await TransitionLock.WaitAsync();
        try
        {
            var poppedOrAbsent = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var nav = modalPage.Navigation ?? ResolveNavigation();
                var stack = nav?.ModalStack;
                if (stack is null || stack.Count == 0)
                {
                    return true;
                }

                if (ReferenceEquals(stack[^1], modalPage))
                {
                    await nav!.PopModalAsync(animated);
                    return true;
                }

                // 若目标页已不在模态栈中，说明已被外部关闭；若仍在栈中说明被上层子弹窗遮挡
                for (var i = 0; i < stack.Count; i++)
                {
                    if (ReferenceEquals(stack[i], modalPage))
                    {
                        return false;
                    }
                }

                return true;
            });

            if (poppedOrAbsent)
            {
                // 留出短暂原生 UI 帧缓冲，确保 Android DialogFragment / WinUI 模态层彻底卸载后再允许下一次转场
                await Task.Delay(32);
            }

            return poppedOrAbsent;
        }
        catch
        {
            return true;
        }
        finally
        {
            TransitionLock.Release();
        }
    }

    public static async Task<TResult> ShowAsync<TResult>(
        INavigation? preferredNavigation,
        ResultModalPage<TResult> page,
        TResult fallbackResult)
    {
        var pushed = await PushModalAsync(preferredNavigation, page, animated: false);
        if (!pushed)
        {
            return fallbackResult;
        }

        return await page.ResultTask;
    }
}

/// <summary>
/// 全站结果型模态弹窗基类：统一收拢返回键拦截、关闭两态保护（关闭中 / 已关闭）与安全出栈后再回调结果。
/// 未来新增弹窗只需继承此类并实现自身布局，关闭时调用 CloseAsync(result) 即可。
/// </summary>
public abstract class ResultModalPage<TResult> : ContentPage
{
    private readonly TaskCompletionSource<TResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TResult _cancelResult;
    private bool _isClosing;
    private bool _isClosed;

    internal Task<TResult> ResultTask => _tcs.Task;

    protected ResultModalPage(TResult cancelResult, float backdropAlpha = 0.42f)
    {
        _cancelResult = cancelResult;
        BackgroundColor = Colors.Black.WithAlpha(backdropAlpha);
        Shell.SetNavBarIsVisible(this, false);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(_cancelResult);
        return true;
    }

    protected async Task CloseAsync(TResult result)
    {
        if (_isClosed || _isClosing) return;
        _isClosing = true;

        var leftStack = await ModalCoordinator.PopModalAsync(this, animated: false);
        if (!leftStack)
        {
            // 被上层子弹窗暂时遮挡，保留可关闭状态，待子弹窗关闭后仍可正常关闭
            _isClosing = false;
            return;
        }

        _isClosed = true;
        _tcs.TrySetResult(result);
    }
}
