using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FSMExpress.Common.Logic.MeshLoaders;
using SkiaSharp;
using System.Drawing;

// from uabea's sprite loader code
namespace FSMExpress.Common.Logic.SpriteLoaders;
public class TextureLoader
{
    private readonly Dictionary<AssetPPtr, SKImage> _spriteImageCache = [];
    private readonly Queue<AssetPPtr> _spriteBitmapQueue = new();
    private readonly SpriteAtlasLookup _spriteAtlasLookup = new();
    private readonly Dictionary<AssetsFileInstance, Dictionary<string, (AssetsFileInstance, AssetFileInfo)>> _nameToSpriteAtlasLookup = [];

    // todo: this should be configurable
    public const int DEFAULT_MAX_SPRITE_BITMAP_CACHE_SIZE = 10;

    public Bitmap? GetSpriteAvaloniaBitmap(
        AssetsManager workspace,
        AssetsFileInstance fileInst, AssetFileInfo assetInf,
        Color tint)
    {
        SKBitmap? skBitmap = GetSpriteSkBitmap(workspace, fileInst, assetInf, tint);
        if (skBitmap == null)
        {
            return null;
        }

        var croppedByteSize = skBitmap.Width * skBitmap.Height * 4;
        var bitmap = new WriteableBitmap(new PixelSize(skBitmap.Width, skBitmap.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var croppedPixels = skBitmap.PeekPixels();
        using var frameBuffer = bitmap.Lock();
        {
            var destByteSize = frameBuffer.RowBytes * frameBuffer.Size.Height;
            unsafe
            {
                // marshal.copy can't do native -> native so we have to do this unsafe copy
                Buffer.MemoryCopy(croppedPixels.GetPixels().ToPointer(), frameBuffer.Address.ToPointer(), destByteSize, croppedByteSize);
            }
        }

        skBitmap.Dispose();
        return bitmap;
    }

    // format output is only for error messages. the byte output is rgba32.
    public SKBitmap? GetSpriteSkBitmap(
        AssetsManager workspace,
        AssetsFileInstance fileInst, AssetFileInfo assetInf,
        Color tint)
    {
        var spriteBf = workspace.GetBaseField(fileInst, assetInf);
        if (spriteBf == null)
        {
            return null;
        }

        var renderData = spriteBf["m_RD"];
        var spriteAtlas = GetSpriteAtlas(workspace, fileInst, assetInf, spriteBf);

        AssetPPtr texturePtr;
        if (spriteAtlas != null)
        {
            texturePtr = spriteAtlas.texture;
        }
        else
        {
            texturePtr = AssetPPtr.FromField(renderData["texture"]);
            if (texturePtr.IsNull())
            {
                return null;
            }
        }

        var textureAssetExt = workspace.GetExtAsset(fileInst, texturePtr.FileId, texturePtr.PathId);
        if (textureAssetExt.baseField == null)
        {
            return null;
        }

        var texturePptr = new AssetPPtr(textureAssetExt.file.name, 0, textureAssetExt.info.PathId);

        // we use skia so we can crop, then convert to avalonia bitmap at the end
        SKImage baseImage;
        if (_spriteImageCache.TryGetValue(texturePptr, out var cachedImage))
        {
            baseImage = cachedImage;
        }
        else
        {
            var textureEditBf = TextureHelper.GetByteArrayTexture(workspace, textureAssetExt.file, textureAssetExt.info);
            var texture = TextureFile.ReadTextureFile(textureEditBf);
            var format = (TextureFormat)texture.m_TextureFormat;

            TextureHelper.SwizzleOptIn(texture, textureAssetExt.file.file);

            var encTextureData = texture.FillPictureData(textureAssetExt.file);
            var textureData = texture.DecodeTextureRaw(encTextureData);
            if (textureData == null)
            {
                return null;
            }

            var baseBitmap = new SKBitmap(texture.m_Width, texture.m_Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var basePixels = baseBitmap.PeekPixels();
            var basePixelsSpan = basePixels.GetPixelSpan<byte>();
            MemoryExtensions.CopyTo(textureData, basePixelsSpan);

            baseImage = SKImage.FromBitmap(baseBitmap);

            // just like the lz4 block decoder, this only pulls whichever item
            // was added earliest since we can't reset the position of elements
            // with a stock .net queue
            if (_spriteBitmapQueue.Count >= DEFAULT_MAX_SPRITE_BITMAP_CACHE_SIZE)
            {
                var lastKey = _spriteBitmapQueue.Dequeue();
                var lastValue = _spriteImageCache[lastKey];
                lastValue.Dispose();
                _spriteImageCache.Remove(lastKey);
            }

            _spriteImageCache[texturePptr] = baseImage;
            _spriteBitmapQueue.Enqueue(texturePptr);
        }

        var pixelsToUnits = spriteBf["m_PixelsToUnits"].AsFloat;

        var pivot = spriteBf["m_Pivot"];
        var pivotX = pivot["x"].AsFloat;
        var pivotY = pivot["y"].AsFloat;

        var rect = spriteBf["m_Rect"];
        var rectWidth = rect["width"].AsFloat;
        var rectHeight = rect["height"].AsFloat;

        float textureRectOffsetX, textureRectOffsetY;
        float textureRectX, textureRectY, textureRectWidth, textureRectHeight;
        uint settingsRaw;

        if (spriteAtlas != null)
        {
            textureRectX = spriteAtlas.textureRectX;
            textureRectY = spriteAtlas.textureRectY;
            textureRectWidth = spriteAtlas.textureRectWidth;
            textureRectHeight = spriteAtlas.textureRectHeight;

            textureRectOffsetX = spriteAtlas.textureRectOffsetX;
            textureRectOffsetY = spriteAtlas.textureRectOffsetY;

            settingsRaw = spriteAtlas.settingsRaw;
        }
        else
        {
            var textureRect = renderData["textureRect"];
            textureRectX = (float)Math.Floor(textureRect["x"].AsFloat);
            textureRectY = (float)Math.Floor(textureRect["y"].AsFloat);
            textureRectWidth = (float)Math.Ceiling(textureRect["width"].AsFloat);
            textureRectHeight = (float)Math.Ceiling(textureRect["height"].AsFloat);

            var textureRectOffset = renderData["textureRectOffset"];
            textureRectOffsetX = textureRectOffset["x"].AsFloat;
            textureRectOffsetY = textureRectOffset["y"].AsFloat;

            settingsRaw = renderData["settingsRaw"].AsUInt;
        }

        // todo
        var flipX = (settingsRaw & 4) != 0;
        var flipY = (settingsRaw & 8) != 0;
        var rot90 = (settingsRaw & 16) != 0;

        // full crop: bounded by the sprite texture rect
        // regular crop: bounded by the sprite's working area
        SKBitmap croppedBitmap = new SKBitmap((int)Math.Ceiling(rectWidth), (int)Math.Ceiling(rectHeight));

        var version = fileInst.file.Metadata.UnityVersion;
        var mesh = new MeshObj(fileInst, renderData, new UnityVersion(version));
        if (mesh.Vertices.Length % 3 != 0)
        {
            return null;
        }

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using (var path = new SKPath())
            {
                var offX = rectWidth * pivotX;
                var offY = rectHeight * pivotY;

                for (var i = 0; i < mesh.Indices.Length; i += 3)
                {
                    var pointAIdx = mesh.Indices[i] * 3;
                    var pointBIdx = mesh.Indices[i + 1] * 3;
                    var pointCIdx = mesh.Indices[i + 2] * 3;
                    var pointA = new SKPoint(
                        mesh.Vertices[pointAIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointAIdx + 1] * pixelsToUnits + offY
                    );
                    var pointB = new SKPoint(
                        mesh.Vertices[pointBIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointBIdx + 1] * pixelsToUnits + offY
                    );
                    var pointC = new SKPoint(
                        mesh.Vertices[pointCIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointCIdx + 1] * pixelsToUnits + offY
                    );
                    var points = new SKPoint[] { pointA, pointB, pointC };
                    path.AddPoly(points);
                }
                canvas.ClipPath(path);

                float xOff;
                float yOff;
                if (flipX)
                {
                    canvas.Translate(croppedBitmap.Width, 0);
                    canvas.Scale(-1, 1);
                    xOff = -textureRectX + rectWidth - textureRectWidth - textureRectOffsetX;
                }
                else
                {
                    xOff = -textureRectX + textureRectOffsetX;
                }

                if (flipY)
                {
                    canvas.Translate(0, croppedBitmap.Height);
                    canvas.Scale(1, -1);
                    yOff = -textureRectY + rectHeight - textureRectHeight - textureRectOffsetY;
                }
                else
                {
                    yOff = -textureRectY + textureRectOffsetY;
                }
                // todo: rot90

                var paint = new SKPaint()
                {
                    ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(tint.R, tint.G, tint.B), SKBlendMode.Modulate)
                };
                canvas.DrawImage(baseImage, xOff, yOff, paint);
            }
        }

        return croppedBitmap;
    }

    private SpriteAtlasData? GetSpriteAtlas(
        AssetsManager workspace,
        AssetsFileInstance fileInst, AssetFileInfo assetInf,
        AssetTypeValueField spriteBf)
    {
        var spriteAtlas = spriteBf["m_SpriteAtlas"];
        var spriteAtlasPtr = AssetPPtr.FromField(spriteAtlas);
        if (spriteAtlasPtr.IsNull())
        {
            var atlasTags = spriteBf["m_AtlasTags.Array"];
            if (atlasTags.Children.Count == 0)
            {
                // nothing we can do. there's no reference to an atlas/texture anywhere.
                return null;
            }

            // in some games, m_SpriteAtlas is not set, but a SpriteAtlas in the same
            // file references this sprite. m_AtlasTags has a list of atlas names.
            // I am not sure why this list would have multiple entries. this field may
            // have more than one only in an editor project.
            var atlasTag = atlasTags[0].AsString;

            // we're going to assume the sprite atlas is always in the same file.
            // it would probably be good to do a last resort option, but tbd on that.
            var atlasNameLookup = GetSpriteAtlasNameLookup(workspace, fileInst);

            var atlasAsset = atlasNameLookup[atlasTag];
            if (!atlasNameLookup.TryGetValue(atlasTag, out var atlasAssetPair))
            {
                // nothing we can do. give up.
                return null;
            }

            spriteAtlasPtr = new AssetPPtr(0, atlasAssetPair.Item2.PathId);
            //spriteAtlasPtr.SetFilePathFromFile(atlasAssetPair.Item1.file);
        }

        spriteAtlasPtr.SetFilePathFromFile(workspace, fileInst);
        var key = SpriteAtlasLookup.MakeRenderKeyGuid(spriteBf["m_RenderDataKey"]["first"]);
        var atlasData = _spriteAtlasLookup.GetAtlasData(spriteAtlasPtr, key);
        if (atlasData != null)
        {
            return atlasData;
        }

        var spriteAtlasExt = workspace.GetExtAsset(fileInst, spriteAtlasPtr.FileId, spriteAtlasPtr.PathId);
        if (spriteAtlasExt.baseField == null)
        {
            return null;
        }

        _spriteAtlasLookup.AddSpriteAtlas(spriteAtlasPtr, spriteAtlasExt.baseField);
        return _spriteAtlasLookup.GetAtlasData(spriteAtlasPtr, key);
    }

    private Dictionary<string, (AssetsFileInstance, AssetFileInfo)> GetSpriteAtlasNameLookup(AssetsManager workspace, AssetsFileInstance fileInstance)
    {
        if (_nameToSpriteAtlasLookup.TryGetValue(fileInstance, out var nameLookup))
        {
            return nameLookup;
        }

        var atlasNameLookup = new Dictionary<string, (AssetsFileInstance, AssetFileInfo)>();
        foreach (var atlasInf in fileInstance.file.GetAssetsOfType(AssetClassID.SpriteAtlas))
        {
            var atlasAssetExt = workspace.GetExtAsset(fileInstance, 0, atlasInf.PathId);
            if (atlasAssetExt.baseField == null)
                continue;

            var atlasBf = atlasAssetExt.baseField;
            var atlasTag = atlasBf["m_Tag"].AsString;
            atlasNameLookup[atlasTag] = (atlasAssetExt.file, atlasAssetExt.info);
        }

        _nameToSpriteAtlasLookup[fileInstance] = atlasNameLookup;
        return atlasNameLookup;
    }

    public void Cleanup()
    {
        foreach (var bitmap in _spriteImageCache.Values)
        {
            bitmap.Dispose();
        }
        _spriteImageCache.Clear();
        _spriteBitmapQueue.Clear();
        _spriteAtlasLookup.Clear();
        _nameToSpriteAtlasLookup.Clear();
    }
}
