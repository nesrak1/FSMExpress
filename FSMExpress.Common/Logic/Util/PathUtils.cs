using AssetsTools.NET.Extra;
using System.IO;

namespace FSMExpress.Logic.Util;
public class PathUtils
{
    public static string GetAssetsFileDirectory(AssetsFileInstance fileInst)
    {
        if (fileInst.parentBundle != null)
        {
            string dir = Path.GetDirectoryName(fileInst.parentBundle.path)!;

            // addressables (this may be a bit slow but I doubt we're calling it all that much)
            string? currentDir = dir;
            while (currentDir != null)
            {
                string? parentDir = Path.GetDirectoryName(currentDir);
                if (parentDir != null)
                {
                    if (Path.GetFileName(currentDir) == "aa" && Path.GetFileName(parentDir) == "StreamingAssets")
                    {
                        // found the aa folder under StreamingAssets, return the _Data folder
                        dir = Path.GetDirectoryName(parentDir)!;
                        break;
                    }
                }
                currentDir = parentDir;
            }

            return dir;
        }
        else
        {
            string dir = Path.GetDirectoryName(fileInst.path)!;
            if (fileInst.name == "unity default resources" || fileInst.name == "unity_builtin_extra")
            {
                dir = Path.GetDirectoryName(dir)!;
            }

            return dir;
        }
    }
}
