using Avalonia.Media.Imaging;
using System.Numerics;

namespace FSMExpress.Silksong.Logic.MapLoader;
public class MapLevel
{
    public string LevelName { get; }
    public Vector3 Position { get; }
    public Bitmap Image { get; }

    public MapLevel(string levelName, Vector3 position, Bitmap image)
    {
        LevelName = levelName;
        Position = position;
        Image = image;
    }
}
