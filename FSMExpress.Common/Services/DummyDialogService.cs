namespace FSMExpress.Common.Services;
public class DummyDialogService : IDialogService
{
    public Task ShowDialog(IDialogAware viewModel)
    {
        return Task.CompletedTask;
    }

    public Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel)
    {
        return Task.FromResult(default(TResult));
    }
}
