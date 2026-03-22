using AddressablesTools.Catalog;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using FSMExpress.Common.Logic.SpriteLoaders;
using FSMExpress.Common.Logic.Util;
using System.Numerics;

namespace FSMExpress.Silksong.Logic.MapLoader;
public class MapLoader
{
    private AssetsManager _manager;
    private ContentCatalogData _catalog;
    private string _gamePath;
    private TextureLoader _texLoader;

    public MapLoader(AssetsManager manager, ContentCatalogData catalog, string gamePath)
    {
        _manager = manager;
        _catalog = catalog;
        _gamePath = gamePath;

        _texLoader = new TextureLoader();
    }

    public MapLevelCollection LoadMap()
    {
        LoadCatalogDependencies();

        // load map bundle
        var mapAssetsAllBun = _manager.BundleLookup[AssetsManager.GetFileLookupKey("maps_assets_all.bundle")];
        // this is okay because LoadCatalogDependencies already loaded the main file
        var mapAssetsAllInst = mapAssetsAllBun.loadedAssetsFiles.FirstOrDefault()
            ?? throw new Exception("Couldn't find serialized file on map assets bundle.");

        var assetBundle = mapAssetsAllInst.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault()
            ?? throw new Exception("Couldn't find bundle asset on map assets bundle.");

        var bundleContMap = _manager.LoadBundleContainers(mapAssetsAllInst, assetBundle);

        // get root map gameobject
        var gameMapHornetGoPtr = bundleContMap["Assets/Prefabs/UI/Map/Game_Map_Hornet.prefab"];
        var gameMapHornetGo = _manager.GetBaseField(mapAssetsAllInst, gameMapHornetGoPtr.PathId);
        var gameMapHornetTfm = GetGoTransform(mapAssetsAllInst, gameMapHornetGo);

        // find script index of GameMapScene monobehaviour
        var scriptLookup = AssetHelper.GetAssetsFileScriptInfos(_manager, mapAssetsAllInst);
        var gameMapSceneSi = scriptLookup
            .Where(p => p.Value.ClassName == "GameMapScene")
            .Select(p => (int?)p.Key)
            .FirstOrDefault()
            ?? throw new Exception("Couldn't find GameMapScene MB on map assets bundle.");

        var mapCol = new MapLevelCollection();
        foreach (var regionTfmPtr in gameMapHornetTfm["m_Children.Array"])
        {
            var regionTfm = _manager.GetExtAsset(mapAssetsAllInst, regionTfmPtr).baseField;
            var regionTfmPos = GetTransformVec(regionTfm);

            foreach (var levelTfmPtr in regionTfm["m_Children.Array"])
            {
                var levelTfm = _manager.GetExtAsset(mapAssetsAllInst, levelTfmPtr).baseField;
                var levelTfmPos = GetTransformVec(levelTfm);

                var levelGo = GetTransformGo(mapAssetsAllInst, levelTfm);

                // name of level (confirmed that gameobject name matches the level name)
                var levelName = levelGo["m_Name"].AsString;

                // level map sprite
                var gameMapScenePtrFld = levelGo["m_Component.Array"].FirstOrDefault(a =>
                {
                    var compInf = _manager.GetExtAsset(mapAssetsAllInst, a["component"], true).info;
                    if (compInf.TypeId != (int)AssetClassID.MonoBehaviour)
                        return false;

                    var scriptIndex = compInf.GetScriptIndex(mapAssetsAllInst.file);
                    if (scriptIndex == ushort.MaxValue)
                        return false;

                    return scriptIndex == gameMapSceneSi;
                });

                // not all gameobjects are level sprites
                if (gameMapScenePtrFld is null)
                    continue;

                // also get SpriteRenderer (for at least colors, but also sometimes the sprite)
                var spriteRendererPtrFld = levelGo["m_Component.Array"].FirstOrDefault(a =>
                {
                    var compInf = _manager.GetExtAsset(mapAssetsAllInst, a["component"], true).info;
                    return compInf.TypeId == (int)AssetClassID.SpriteRenderer;
                });

                // give up
                if (spriteRendererPtrFld is null)
                    continue;

                var spriteRendererBf = _manager.GetExtAsset(mapAssetsAllInst, spriteRendererPtrFld["component"]).baseField;
                var spriteColorFld = spriteRendererBf["m_Color"];
                var spriteTint = System.Drawing.Color.FromArgb(
                    (byte)(spriteColorFld["a"].AsFloat * 255),
                    (byte)(spriteColorFld["r"].AsFloat * 255),
                    (byte)(spriteColorFld["g"].AsFloat * 255),
                    (byte)(spriteColorFld["b"].AsFloat * 255)
                );

                // get correct sprite
                var gameMapSceneBf = _manager.GetExtAsset(mapAssetsAllInst, gameMapScenePtrFld["component"]).baseField;
                var spriteExt = _manager.GetExtAsset(mapAssetsAllInst, gameMapSceneBf["fullSprite"], true);

                // not all GameMapScenes have sprites set for whatever reason, check SpriteRenderer if it exists
                // also use the SpriteRenderer if initialState == States.Hidden
                if (spriteExt.info is null || gameMapSceneBf["initialState"].AsInt == 0)
                {
                    // use SpriteRenderer's sprite
                    spriteExt = _manager.GetExtAsset(mapAssetsAllInst, spriteRendererBf["m_Sprite"], true);
                }

                // give up
                if (spriteExt.info is null)
                    continue;

                var spriteImage = _texLoader.GetSpriteAvaloniaBitmap(_manager, spriteExt.file, spriteExt.info, spriteTint);

                // can't load the image (we could replace this with placeholder,
                // but the sprite missing just shouldn't happen in the first place)
                if (spriteImage is null)
                    continue;

                // now make level object
                mapCol.Levels.Add(new MapLevel(levelName, regionTfmPos + levelTfmPos, spriteImage));
            }
        }

        return mapCol;
    }

    private void LoadCatalogDependencies()
    {
        var aaPath = Path.Combine(_gamePath, "StreamingAssets", "aa");
        foreach (object k in _catalog.Resources.Keys)
        {
            // Scenes/Menu_Title is also an option, but it seems to load more deps
            if (k is not string s || s != "_GameCameras")
                continue;

            var rsrcs = _catalog.Resources[s];
            foreach (var rsrc in rsrcs)
            {
                if (rsrc.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                    continue;

                var deps = rsrc.Dependencies;
                foreach (var dep in deps)
                {
                    var filePath = dep.InternalId;
                    filePath = filePath.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", aaPath);

                    // need to fix returning already loaded bundles in AT. for now we'll check manually.
                    if (!_manager.BundleLookup.TryGetValue(AssetsManager.GetFileLookupKey(filePath), out var bunInst))
                        bunInst = _manager.LoadBundleFile(filePath);

                    _manager.LoadBundleMainFile(bunInst);
                }
            }

            return; // we found what we wanted, break out now
        }
    }

    private AssetTypeValueField GetGoTransform(AssetsFileInstance fileInst, AssetTypeValueField gameObjectBf)
    {
        return _manager.GetBaseField(fileInst,
            gameObjectBf["m_Component.Array"][0]["component"]["m_PathID"].AsLong);
    }

    private AssetTypeValueField GetTransformGo(AssetsFileInstance fileInst, AssetTypeValueField transformBf)
    {
        return _manager.GetBaseField(fileInst,
            transformBf["m_GameObject"]["m_PathID"].AsLong);
    }

    private static Vector3 GetTransformVec(AssetTypeValueField transformBf)
    {
        var posFld = transformBf["m_LocalPosition"];
        return new Vector3(
            posFld["x"].AsFloat,
            posFld["y"].AsFloat,
            posFld["z"].AsFloat
        );
    }
}
