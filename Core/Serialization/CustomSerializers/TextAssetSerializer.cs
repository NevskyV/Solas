using Solas.Assets;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class TextAssetSerializer : ICustomSerializer<TextAsset>
{
    public void Write(TextAsset value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.Write(value.Id, stream);
        var count = value.Lines.Length;
        EngineContext.Serializer.Write(count, stream);
        for (var i = 0; i < count; i++) 
            EngineContext.Serializer.Write(value.Lines[i], stream);
    }

    public TextAsset Read(FileStream stream)
    {
        var asset = new TextAsset() {Id = EngineContext.Serializer.ReadGuid(stream)};
        
        var count = EngineContext.Serializer.ReadInt32(stream);
        asset.Lines = new string[count];
        for (var i = 0; i < count; i++) 
            asset.Lines[i] = EngineContext.Serializer.ReadString(stream);
        
        return asset;
    }
}