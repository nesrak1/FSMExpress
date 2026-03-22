using AddressablesTools;
using AddressablesTools.Catalog;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using FSMExpress.Common.Assets;
using FSMExpress.Common.Document;
using FSMExpress.Common.Logic.Util;
using FSMExpress.Common.Services;
using FSMExpress.Common.ViewModels;
using FSMExpress.Logic.Configuration;
using FSMExpress.Logic.Fsm;
using FSMExpress.PlayMaker;
using FSMExpress.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FSMExpress.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DEFAULT_TITLE_TEXT = "FSMExpress";

    private readonly AssetsManager _manager = new();

    private ContentCatalogData? _catalog = null;
    private readonly Dictionary<string, List<string>> _catalogDeps = [];

    public ObservableCollection<FsmDocument> Documents { get; set; } = [];

    [ObservableProperty]
    private string _titleText = DEFAULT_TITLE_TEXT;

    [ObservableProperty]
    private string? _lastOpenedPath = null;

    [ObservableProperty]
    private FsmDocument? _activeDocument = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FieldsIndexed))]
    public FsmDocumentNode? _selectedNode = null;

    public IEnumerable<FsmDocumentNodeField> FieldsIndexed
    {
        get
        {
            if (SelectedNode is null)
                yield break;

            int index = 0;
            foreach (var field in SelectedNode.Fields)
            {
                yield return field is FsmDocumentNodeClassField classField
                    ? new FsmDocumentNodeIndexedClassField(classField, index++)
                    : field;
            }
        }
    }

    public MainWindowViewModel()
    {
        _manager.UseMonoTemplateFieldCache = true;
        _manager.UseTemplateFieldCache = true;
        _manager.UseQuickLookup = true;
    }

    public async void Loaded()
    {
        // load catalog on startup if game path already set (most likely scenario)
        var loadedCatalog = await TryLoadCatalog();

        // map loading test for silksong
        //if (loadedCatalog && _catalog is not null)
        //{
        //    if (ConfigurationManager.Settings.DefaultGamePath is not { } gamePath)
        //        return;

        //    var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        //    var levelName = await dialogService.ShowDialog(new MapSelectorViewModel(_manager, _catalog, gamePath));
        //    if (levelName is null)
        //        return;

        //    var selectedFsms = await PickFsms(Path.Combine(gamePath, "StreamingAssets", "aa", "StandaloneWindows64", "scenes_scenes_scenes", $"{levelName.ToLowerInvariant()}.bundle"));
        //    if (selectedFsms is null)
        //        return;

        //    foreach (var fsm in selectedFsms)
        //    {
        //        LoadPlaymakerFsm(fsm);
        //    }
        //}
    }

    public async Task<string?> PickScene(string ggmPath)
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        var fileInst = _manager.LoadAssetsFile(ggmPath);
        if (!_manager.LoadClassDatabase(fileInst))
        {
            await MessageBoxUtil.ShowDialog("Class database failed to load", "Couldn't load class database class. Check if classdata.tpk exists?");
            return null;
        }

        var sceneChoice = await dialogService.ShowDialog(new SceneSelectorViewModel(_manager, fileInst));
        if (sceneChoice == null)
            return null;

        return Path.Combine(Path.GetDirectoryName(ggmPath)!, sceneChoice.FileName);
    }

    public async Task<IEnumerable<AssetExternal>?> PickFsms(string filePath)
    {
        AssetsFileInstance fileInst;

        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        var fileType = FileTypeDetector.DetectFileType(filePath);
        if (fileType == DetectedFileType.BundleFile)
        {
            if (_catalog is not null)
            {
                await LoadCatalogDeps(filePath);
            }

            var bunInst = _manager.LoadBundleFile(filePath);
            var maybeFileInst = _manager.LoadBundleMainFile(bunInst);
            if (maybeFileInst == null)
            {
                await MessageBoxUtil.ShowDialog("Unsupported type", "Sorry, unsure which file to open in this bundle.");
                return null;
            }

            fileInst = maybeFileInst;
        }
        else if (fileType == DetectedFileType.AssetsFile)
        {
            fileInst = _manager.LoadAssetsFile(filePath);
        }
        else //if (fileType == DetectedFileType.Unknown)
        {
            await MessageBoxUtil.ShowDialog("Unsupported type", "Could not detect this as a valid Unity file.");
            return null;
        }

        if (!_manager.LoadClassDatabase(fileInst))
        {
            await MessageBoxUtil.ShowDialog("Class database failed to load", "Couldn't load class database class. Check if classdata.tpk exists?");
            return null;
        }

        var fsmChoices = await dialogService.ShowDialog(new FsmSelectorViewModel(_manager, fileInst));
        if (fsmChoices is null)
            return null;

        return fsmChoices.Select(fsm =>
        {
            var fsmFileInst = _manager.FileLookup[fsm.Ptr.FilePath.ToLowerInvariant()];
            return _manager.GetExtAsset(fsmFileInst, 0, fsm.Ptr.PathId);
        });
    }

    private void LoadPlaymakerFsm(AssetExternal fsmExt)
    {
        var fsmBaseField = fsmExt.baseField;
        var fsmFileInst = fsmExt.file;

        var fsmObject = new FsmPlaymaker(new AfAssetField(fsmBaseField["fsm"], new AfAssetNamer(_manager, fsmFileInst)));

        // get gameobject name
        var namer = new AfAssetNamer(_manager, fsmFileInst);
        var goPtr = fsmBaseField["m_GameObject"];
        fsmObject.GoName = namer.GetName(goPtr["m_FileID"].AsInt, goPtr["m_PathID"].AsLong) ?? "<Unknown GO>";

        var fsmDoc = fsmObject.MakeDocument();
        Documents.Add(fsmDoc);
        ActiveDocument = fsmDoc;
    }

    public async void FileOpen()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
            return;

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a file",
            FileTypeFilter = [StorageService.Any],
        });

        var fileNames = FileDialogUtils.GetOpenFileDialogFiles(result);
        if (fileNames.Length == 0)
            return;

        var fileName = fileNames[0];
        LastOpenedPath = fileName;

        var selectedFsms = await PickFsms(fileName);
        if (selectedFsms is null)
            return;

        foreach (var fsm in selectedFsms)
        {
            LoadPlaymakerFsm(fsm);
        }
    }

    public async void FileOpenSceneList()
    {
        string? ggmPath;
        if (ConfigurationManager.Settings.DefaultGamePath is { } gamePath)
            ggmPath = Path.Combine(gamePath, "globalgamemanagers");
        else
            ggmPath = await PickGamePathWithFile("globalgamemanagers");

        if (ggmPath is null)
            return;

        var scenePath = await PickScene(ggmPath);
        if (string.IsNullOrEmpty(scenePath))
            return;

        LastOpenedPath = scenePath;

        var selectedFsms = await PickFsms(scenePath);
        if (selectedFsms is null)
            return;

        foreach (var fsm in selectedFsms)
        {
            LoadPlaymakerFsm(fsm);
        }
    }

    public async void FileOpenResourcesAssets()
    {
        string? resourcesPath;
        if (ConfigurationManager.Settings.DefaultGamePath is { } gamePath)
            resourcesPath = Path.Combine(gamePath, "resources.assets");
        else
            resourcesPath = await PickGamePathWithFile("resources.assets");

        if (resourcesPath is null)
            return;

        LastOpenedPath = resourcesPath;

        var selectedFsms = await PickFsms(resourcesPath);
        if (selectedFsms is null)
            return;

        foreach (var fsm in selectedFsms)
        {
            LoadPlaymakerFsm(fsm);
        }
    }

    public async Task<bool> TryLoadCatalog()
    {
        if (ConfigurationManager.Settings.DefaultGamePath is not { } gamePath)
            return false;

        var catalogPath = Path.Combine(gamePath, "StreamingAssets/aa/catalog.bin");
        if (!File.Exists(catalogPath))
        {
            catalogPath = Path.Combine(gamePath, "StreamingAssets/aa/catalog.json");
            if (!File.Exists(catalogPath))
            {
                return false;
            }
        }

        // read catalog
        var fileType = CatalogFileType.None;
        using (FileStream fs = File.OpenRead(catalogPath))
        {
            fileType = AddressablesCatalogFileParser.GetCatalogFileType(fs);
        }

        switch (fileType)
        {
            case CatalogFileType.Json:
                _catalog = AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(catalogPath));
                break;
            case CatalogFileType.Binary:
                _catalog = AddressablesCatalogFileParser.FromBinaryData(File.ReadAllBytes(catalogPath));
                break;
            default:
                await MessageBoxUtil.ShowDialog("Invalid catalog", "Couldn't detect catalog file format. Dependencies may not load.");
                return false;
        }

        // generate lookup
        TitleText = "Loading catalog...";
        await Task.Yield();

        var aaPath = Path.GetDirectoryName(catalogPath)!;
        GenerateCatalogDepLookup(_catalog, aaPath);

        TitleText = DEFAULT_TITLE_TEXT;

        return true;
    }

    public async void FileOpenLast()
    {
        if (LastOpenedPath is null)
        {
            return;
        }

        if (!File.Exists(LastOpenedPath))
        {
            await MessageBoxUtil.ShowDialog($"No {LastOpenedPath}", $"Couldn't load {LastOpenedPath}. Was it deleted?");
            return;
        }

        var selectedFsms = await PickFsms(LastOpenedPath);
        if (selectedFsms is null)
            return;

        foreach (var fsm in selectedFsms)
        {
            LoadPlaymakerFsm(fsm);
        }
    }

    public async void ConfigSetGamePath()
    {
        // we're checking ggm path for sanity here
        var ggmPath = await PickGamePathWithFile("globalgamemanagers");
        if (ggmPath is null)
            return;

        ConfigurationManager.Settings.DefaultGamePath = Path.GetDirectoryName(ggmPath);
        await TryLoadCatalog();
    }

    public void CloseTab()
    {
        SelectedNode = null;
        if (ActiveDocument is not null)
            Documents.Remove(ActiveDocument);
    }

    public void CloseAllTabs()
    {
        ActiveDocument = null;
        SelectedNode = null;
        Documents.Clear();

        _manager.UnloadAllAssetsFiles(true);
        _manager.UnloadAllBundleFiles();
    }

    private static async Task<string?> PickGamePathWithFile(params string[] fileNames)
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
            return null;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open a [Game name]_Data folder"
        });

        var folderNames = FileDialogUtils.GetOpenFolderDialogFolders(result);
        if (folderNames.Length == 0)
            return null;

        var folderName = folderNames[0];

        var foundFilePath = string.Empty;
        if (fileNames.Length == 1)
        {
            var fileName = fileNames[0];
            var filePath = Path.Combine(folderName, fileName);
            if (!File.Exists(filePath))
            {
                await MessageBoxUtil.ShowDialog($"No {fileName}", $"Couldn't load {fileName}. Did you open the right folder?");
                return null;
            }

            foundFilePath = filePath;
        }
        else if (fileNames.Length > 1)
        {
            bool success = false;
            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(folderName, fileName);
                if (File.Exists(filePath))
                {
                    success = true;
                    foundFilePath = filePath;
                    break;
                }
            }

            if (!success)
            {
                var firstFileName = fileNames[0];
                await MessageBoxUtil.ShowDialog($"No {firstFileName} or others", $"Couldn't load {firstFileName}, among other files. Did you open the right folder?");
                return null;
            }
        }

        return foundFilePath;
    }

    private void GenerateCatalogDepLookup(ContentCatalogData catalog, string aaPath)
    {
        _catalogDeps.Clear();
        foreach (var rsrcLocs in catalog.Resources.Values)
        {
            foreach (var rsrcLoc in rsrcLocs)
            {
                if (rsrcLoc.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                {
                    List<ResourceLocation> locDeps;
                    if (rsrcLoc.Dependencies != null)
                    {
                        // new version
                        locDeps = rsrcLoc.Dependencies;
                    }
                    else if (rsrcLoc.DependencyKey != null)
                    {
                        // old version
                        locDeps = catalog.Resources[rsrcLoc.DependencyKey];
                    }
                    else
                    {
                        continue;
                    }

                    if (locDeps.Count < 1)
                    {
                        continue;
                    }

                    var baseBundlePath = CatalogPathToAbsolutePath(locDeps[0].InternalId, aaPath);
                    var bundleDepPaths = new List<string>();
                    if (locDeps.Count > 1)
                    {
                        for (int i = 1; i < locDeps.Count; i++)
                        {
                            bundleDepPaths.Add(CatalogPathToAbsolutePath(locDeps[i].InternalId, aaPath));
                        }
                    }

                    _catalogDeps[baseBundlePath] = bundleDepPaths;
                }
            }
        }
    }

    private static string CatalogPathToAbsolutePath(string catalogPath, string aaPath)
    {
        return Path.GetFullPath(catalogPath
            .Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", aaPath)
            .Replace('\\', '/'));
    }

    private async Task LoadCatalogDeps(string bundlePath)
    {
        if (_catalog is null)
            return;

        bundlePath = Path.GetFullPath(bundlePath);

        if (!_catalogDeps.TryGetValue(bundlePath, out var depPaths))
            return;

        for (var i = 0; i < depPaths.Count; i++)
        {
            var depPath = depPaths[i];
            string lookupKey = AssetsManager.GetFileLookupKey(depPath);
            if (!_manager.BundleLookup.TryGetValue(lookupKey, out var bunInst))
                bunInst = _manager.LoadBundleFile(depPath);

            // ignore result, we just want the bundle loaded
            _manager.LoadBundleMainFile(bunInst);

            if ((i % 5) != 0)
            {
                TitleText = $"Loading dependencies ({Math.Ceiling((float)i / depPaths.Count * 100)}% done) {bunInst.name}";
                await Task.Yield();
            }
        }

        TitleText = DEFAULT_TITLE_TEXT;
    }
}
