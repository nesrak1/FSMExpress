using AssetsTools.NET;
using System.Text.RegularExpressions;

namespace FSMExpress.Common.Logic.Util;
public static partial class FileTypeDetector
{
    public static DetectedFileType DetectFileType(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var r = new AssetsFileReader(fs);
        return DetectFileType(r, 0);
    }

    public static DetectedFileType DetectFileType(AssetsFileReader r, long startAddress)
    {
        r.BigEndian = true;
        if (r.BaseStream.Length < 0x20)
        {
            return DetectedFileType.Unknown;
        }

        r.Position = startAddress;
        string possibleBundleHeader = r.ReadStringLength(7);
        r.Position = startAddress + 0x08;
        int possibleFormat = r.ReadInt32();

        r.Position = startAddress + (possibleFormat >= 0x16 ? 0x30 : 0x14);

        string possibleVersion = "";
        char curChar;
        while (r.Position < r.BaseStream.Length && (curChar = (char)r.ReadByte()) != 0x00)
        {
            possibleVersion += curChar;
            if (possibleVersion.Length > 0xFF)
            {
                break;
            }
        }

        string emptyVersion = PrintableEmptyVersionRegex().Replace(possibleVersion, "");
        string fullVersion = PrintableFullVersionRegex().Replace(possibleVersion, "");

        if (possibleBundleHeader == "UnityFS")
        {
            return DetectedFileType.BundleFile;
        }
        else if (possibleFormat < 0xFF && emptyVersion.Length == 0 && fullVersion.Length >= 5)
        {
            return DetectedFileType.AssetsFile;
        }
        return DetectedFileType.Unknown;
    }

    [GeneratedRegex("[a-zA-Z0-9\\-\\.\\n]")]
    private static partial Regex PrintableEmptyVersionRegex();

    [GeneratedRegex("[^a-zA-Z0-9\\-\\.\\n]")]
    private static partial Regex PrintableFullVersionRegex();
}

public enum DetectedFileType
{
    Unknown,
    AssetsFile,
    BundleFile
}