using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using FSMExpress.Common;
using FSMExpress.Common.Services;
using FSMExpress.ViewModels;
using FSMExpress.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FSMExpress;
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            var provider = ConfigureServices(desktop.MainWindow);
            Ioc.Default.ConfigureServices(provider);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices(Window? mainWindow)
    {
        var services = new ServiceCollection();

        var viewLocator = new ViewLocator();
        if (mainWindow != null)
            services.AddSingleton<IDialogService>(new DialogService(mainWindow, viewLocator));
        else
            services.AddSingleton<IDialogService, DummyDialogService>();

        return services.BuildServiceProvider();
    }
}