using AddressablesTools.Catalog;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using FSMExpress.Common.Services;
using FSMExpress.Common.ViewModels;
using FSMExpress.Silksong.Logic.MapLoader;

namespace FSMExpress.Silksong.ViewModels.Dialogs;
public partial class MapSelectorViewModel : ViewModelBase, IDialogAware<string>
{
    private readonly AssetsManager _manager;
    private readonly ContentCatalogData _catalog;
    private readonly string _gamePath;
    private readonly MapLoader _loader;

    [ObservableProperty]
    private MapLevelCollection _mapCol;
    [ObservableProperty]
    private string? _selectedLevel = "";

    public string Title => "Scene Selector (map)";
    public int Width => 700;
    public int Height => 500;
    public event Action<string?>? RequestClose;

    public Task AsyncInit() => Task.CompletedTask;

    public MapSelectorViewModel(AssetsManager manager, ContentCatalogData catalog, string gamePath)
    {
        _manager = manager;
        _catalog = catalog;
        _gamePath = gamePath;

        _loader = new MapLoader(manager, catalog, gamePath);
        MapCol = _loader.LoadMap();
    }

    public void PickOk()
    {
        RequestClose?.Invoke(SelectedLevel);
    }

    public void PickCancel()
    {
        RequestClose?.Invoke(null);
    }
}
