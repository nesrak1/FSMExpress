using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using FSMExpress.Common.Logic.Util;
using FSMExpress.Common.Services;
using FSMExpress.Common.Util;
using FSMExpress.Common.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FSMExpress.ViewModels.Dialogs;
public partial class SceneSelectorViewModel : ViewModelBase, IDialogAware<SceneSelectorListEntry>
{
    [ObservableProperty]
    private string _searchText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSceneSelected))]
    public SceneSelectorListEntry? _selectedEntry;
    [ObservableProperty]
    private RangeObservableCollection<SceneSelectorListEntry> _entries = [];

    private List<SceneSelectorListEntry> _internalEntries = [];

    private readonly AssetsManager _manager;
    private readonly AssetsFileInstance _fileInst;
    private readonly Action<string> _searchDb;

    public string Title => "Scene Selector";
    public int Width => 450;
    public int Height => 650;
    public event Action<SceneSelectorListEntry?>? RequestClose;

    public bool IsSceneSelected => SelectedEntry != null;

    public Task AsyncInit() => FillSceneEntries();

    public SceneSelectorViewModel(AssetsManager manager, AssetsFileInstance fileInst)
    {
        _manager = manager;
        _fileInst = fileInst;
        _searchDb = DebounceUtils.Debounce<string>(FilterEntries, 300);
    }

    partial void OnSearchTextChanged(string value) => _searchDb(value);

    private void FilterEntries(string searchText)
    {
        Entries.Clear();
        if (searchText == string.Empty)
            Entries.AddRange(_internalEntries);
        else
            Entries.AddRange(_internalEntries.Where(e =>
                e.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            ));

        if (Entries.Count > 0)
            SelectedEntry = Entries[0];
    }

    public async Task FillSceneEntries()
    {
        var buildSettingsInfs = _fileInst.file.GetAssetsOfType(AssetClassID.BuildSettings);
        if (buildSettingsInfs.Count == 0)
        {
            await MessageBoxUtil.ShowDialog("Scene error", "Couldn't find build settings asset.");
            RequestClose?.Invoke(null);
            return;
        }

        var buildSettingsBf = _manager.GetBaseField(_fileInst, buildSettingsInfs[0]);
        var scenes = buildSettingsBf["scenes.Array"];
        var scenesList = scenes.Select(f => f.AsString);
        _internalEntries =
        [
            .. scenesList.Select((s, i) => new SceneSelectorListEntry(s, $"level{i}")),
            .. scenesList.Select((s, i) => new SceneSelectorListEntry(s, $"sharedassets{i}.assets")),
        ];
        _internalEntries.Add(new SceneSelectorListEntry("resources.assets", "resources.assets"));

        FilterEntries(string.Empty);
    }

    public void PickSelectedEntry()
    {
        if (SelectedEntry is not null)
        {
            RequestClose?.Invoke(SelectedEntry);
        }
    }

    public void PickCancel()
    {
        RequestClose?.Invoke(null);
    }
}

public class SceneSelectorListEntry(string name, string fileName)
{
    public string Name { get; } = name;
    public string FileName { get; } = fileName;

    public override string ToString() => $"{Name} ({FileName})";
}