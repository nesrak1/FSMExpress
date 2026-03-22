using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using FSMExpress.Common.Assets;
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
public partial class FsmDataSearchViewModel : ViewModelBase, IDialogAware<FsmDataSearchListEntry>
{
    [ObservableProperty]
    private string _searchText = "";
    [ObservableProperty]
    private FsmDataSearchType _searchType = FsmDataSearchType.ClassName;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFsmSelected))]
    public FsmDataSearchListEntry? _selectedEntry;
    [ObservableProperty]
    private RangeObservableCollection<FsmDataSearchListEntry> _entries = [];

    private List<FsmDataSearchListEntry> _internalEntries = [];

    private readonly AssetsManager _manager;
    private readonly AssetsFileInstance _fileInst;
    private readonly Action<string> _searchDb;

    public string Title => "FSM Data Search";
    public int Width => 500;
    public int Height => 450;
    public event Action<FsmDataSearchListEntry?>? RequestClose;

    public bool IsFsmSelected => SelectedEntry != null;

    public Task AsyncInit() => FillFsmEntries();

    public FsmDataSearchViewModel(AssetsManager manager, AssetsFileInstance fileInst)
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
            Entries.AddRange(_internalEntries.Where(e => e.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)));

        if (Entries.Count > 0)
            SelectedEntry = Entries[0];
    }

    public async Task FillFsmEntries()
    {
        SearchText = "Loading...";
        if (!_manager.LoadMonoBehaviours(_fileInst))
        {
            await MessageBoxUtil.ShowDialog("Mono error", "Couldn't find game assemblies. Check your Managed or il2cpp_data folder?");
            RequestClose?.Invoke(null);
            return;
        }

        // find script indices for monobehaviours we care about
        // note: hashset required because for some reason the same
        // monobehaviour can show up multiple times in one type tree
        // bruh...
        var playMakerFsmSis = new HashSet<ushort>();
        var fsmTemplateSis = new HashSet<ushort>();
        var scriptInfos = AssetHelper.GetAssetsFileScriptInfos(_manager, _fileInst);
        foreach (var scriptInfo in scriptInfos)
        {
            var scriptInfoRef = scriptInfo.Value;
            var asmName = scriptInfoRef.AsmName;
            var nameSpace = scriptInfoRef.Namespace;
            var className = scriptInfoRef.ClassName;
            if (asmName == "PlayMaker.dll" && nameSpace == "" && className == "PlayMakerFSM")
                playMakerFsmSis.Add((ushort)scriptInfo.Key);
            if (asmName == "PlayMaker.dll" && nameSpace == "" && className == "FsmTemplate")
                fsmTemplateSis.Add((ushort)scriptInfo.Key);
        }

        var file = _fileInst.file;
        var afNamer = new AfAssetNamer(_manager, _fileInst);
        foreach (var info in file.AssetInfos)
        {
            if (info.TypeId != (int)AssetClassID.MonoBehaviour)
                continue;

            var infoSi = info.GetScriptIndex(_fileInst.file);
            if (infoSi == ushort.MaxValue)
                continue;

            if (playMakerFsmSis.Contains(infoSi))
            {
                var fsmName = GetFSMNameFast(_manager, _fileInst, info, afNamer);
                var fsmPtr = new AssetPPtr(_fileInst.name, info.PathId);
                _internalEntries.Add(new FsmDataSearchListEntry(fsmName, fsmPtr));
            }
        }

        _internalEntries.Sort((a, b) => a.Name.CompareTo(b.Name));

        SearchText = "";
        FilterEntries(string.Empty);
    }

    private static string GetFSMNameFast(AssetsManager manager, AssetsFileInstance fileInst, AssetFileInfo info, AfAssetNamer namer)
    {
        var fsmTemp = manager.GetTemplateBaseField(fileInst, info);

        var nameIndex = fsmTemp.Children.FindIndex(monoTemp => monoTemp.Name == "name");
        if (nameIndex != -1)
        {
            fsmTemp.Children.RemoveRange(nameIndex + 1, fsmTemp.Children.Count - (nameIndex + 1));
        }

        AssetTypeValueField? monoBf;
        lock (fileInst.LockReader)
        {
            monoBf = fsmTemp.MakeValue(fileInst.file.Reader, info.GetAbsoluteByteOffset(fileInst.file));
        }

        var fsmName = monoBf["fsm"]["name"].AsString;
        var goPtr = monoBf["m_GameObject"];
        var goName = namer.GetName(goPtr["m_FileID"].AsInt, goPtr["m_PathID"].AsLong);
        return $"{goName} - {fsmName}";
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

public class FsmDataSearchListEntry(string name, AssetPPtr ptr)
{
    public string Name { get; } = name;
    public AssetPPtr Ptr { get; } = ptr;
}

public enum FsmDataSearchType
{
    ClassName,
    FieldValueText
}