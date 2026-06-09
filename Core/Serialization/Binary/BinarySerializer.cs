using Solas.Serialization.Core;

namespace Solas.Serialization.Binary;

public class BinarySerializer() : Serializer(typeof(CustomBinarySerializer<>))
{
    public override void Write(byte value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(byte[] value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(bool value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(char value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(string value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(short value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(int value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(long value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(ushort value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(uint value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(ulong value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(float value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(double value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
    }

    public override void Write(Guid value, FileStream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(value.ToByteArray());
    }

    public override byte ReadByte(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadByte();
    }

    public override byte[] ReadBytes(int count, FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadBytes(count);
    }

    public override bool ReadBool(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadBoolean();
    }

    public override char ReadChar(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadChar();
    }

    public override string ReadString(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadString();
    }

    public override short ReadInt16(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadInt16();
    }

    public override int ReadInt32(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadInt32();
    }

    public override long ReadInt64(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadInt64();
    }

    public override ushort ReadUInt16(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadUInt16();
    }

    public override uint ReadUInt32(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadUInt32();
    }

    public override ulong ReadUInt64(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadUInt64();
    }

    public override float ReadFloat(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadSingle();
    }

    public override double ReadDouble(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadDouble();
    }

    public override Guid ReadGuid(FileStream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return new Guid(reader.ReadBytes(16));
    }
}