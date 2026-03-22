using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FSMExpress.Common.ViewModels;

namespace FSMExpress.Common;
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        var viewModelType = data.GetType();
        var viewName = viewModelType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = viewModelType.Assembly.GetType(viewName);

        if (type != null)
        {
            var control = (Control)Activator.CreateInstance(type)!;
            control.DataContext = data;
            return control;
        }

        return new TextBlock { Text = "Not Found: " + viewName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
