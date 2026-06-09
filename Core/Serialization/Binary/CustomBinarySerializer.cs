using Solas.Serialization.Core;

namespace Solas.Serialization.Binary;

public abstract class CustomBinarySerializer<T> : ICustomSerializer<T>
{
    public abstract void Write(T value, FileStream stream);

    public abstract T Read(FileStream stream);
}