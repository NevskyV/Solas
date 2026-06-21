using Solas.Assets;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class TextAssetSerializer : ICustomSerializer<TextAsset>
{
    public void Write(TextAsset value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.Write(value.Id, stream, "Id");
        EngineContext.Serializer.WriteArray(value.Lines, stream, EngineContext.Serializer.Write,"Lines");
    }

    public TextAsset Read(FileStream stream)
    {
        var asset = new TextAsset
        {
            Id = EngineContext.Serializer.ReadGuid(stream),
            Lines = EngineContext.Serializer.ReadArray<string>(stream,  EngineContext.Serializer.ReadString)
        };

        return asset;
    }
}