namespace Solas.Assets;

public class TextAsset : Asset
{
    public string[] Lines;
    
    public override void Write(BinaryWriter writer)
    {
        var count = Lines.Length;
        writer.Write(count);
        for(var i = 0; i < count; i++)
        {
            writer.Write(Lines[i]);
        }
    }

    public override Asset Read(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        Lines = new string[count];
        for(var i = 0; i < count; i++)
        {
            Lines[i] = reader.ReadString();
        }
        return this;
    }
}