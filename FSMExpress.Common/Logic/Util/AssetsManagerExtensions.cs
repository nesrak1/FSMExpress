using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using FSMExpress.Logic.Util;

namespace FSMExpress.Common.Logic.Util;
public static class AssetsManagerExtensions
{
    public static bool LoadClassDatabase(this AssetsManager manager, AssetsFileInstance forFile)
    {
        if (manager.ClassPackage == null)
        {
            var classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
                manager.LoadClassPackage(classDataPath);
            else
                return false;
        }

        var unityVersion = forFile.file.Metadata.UnityVersion;
        if (manager.ClassDatabase == null && unityVersion != "0.0.0")
        {
            manager.LoadClassDatabaseFromPackage(unityVersion);
        }

        return true;
    }

    public static bool LoadMonoBehaviours(this AssetsManager manager, AssetsFileInstance forFile)
    {
        if (manager.MonoTempGenerator is null)
        {
            var fileDir = PathUtils.GetAssetsFileDirectory(forFile);
            var managedDir = Path.Combine(fileDir, "Managed");
            if (Directory.Exists(managedDir))
            {
                var hasDll = Directory.GetFiles(managedDir, "*.dll").Length > 0;
                if (hasDll)
                {
                    manager.MonoTempGenerator = new MonoCecilTempGenerator(managedDir);
                    return true;
                }
            }

            var il2cppFiles = FindCpp2IlFiles.Find(fileDir);
            if (il2cppFiles.success)
            {
                manager.MonoTempGenerator = new Cpp2IlTempGenerator(il2cppFiles.metaPath, il2cppFiles.asmPath);
                return true;
            }

            return false;
        }

        return true;
    }

    public static AssetsFileInstance? LoadBundleMainFile(this AssetsManager manager, BundleFileInstance bunInst)
    {
        var dirInf = bunInst.file.BlockAndDirInfo.DirectoryInfos.FirstOrDefault(i => (i.Flags & 4) != 0 && !i.Name.EndsWith(".sharedAssets"));
        if (dirInf is not null)
        {
            return manager.LoadAssetsFileFromBundle(bunInst, dirInf.Name);
        }

        return null;
    }

    public static Dictionary<string, AssetPPtr> LoadBundleContainers(
        this AssetsManager manager, AssetsFileInstance assetBundleInst, AssetFileInfo assetBundleInf)
    {
        var map = new Dictionary<string, AssetPPtr>();
        var assetBundleBf = manager.GetBaseField(assetBundleInst, assetBundleInf);
        var container = assetBundleBf["m_Container.Array"];
        foreach (var entry in container)
        {
            var first = entry["first"].AsString;
            var second = AssetPPtr.FromField(entry["second.asset"]);
            map[first] = second;
        }
        return map;
    }
}
