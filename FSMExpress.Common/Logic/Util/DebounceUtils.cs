using Avalonia.Threading;

namespace FSMExpress.Common.Util;
public static class DebounceUtils
{
    public static Action<T> Debounce<T>(Action<T> func, int milliseconds = 300)
    {
        CancellationTokenSource? cancelTokenSource = null;

        return arg =>
        {
            cancelTokenSource?.Cancel();
            cancelTokenSource = new CancellationTokenSource();

            Task.Delay(milliseconds, cancelTokenSource.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            func(arg);
                        });
                    }
                }, TaskScheduler.Default);
        };
    }
}
