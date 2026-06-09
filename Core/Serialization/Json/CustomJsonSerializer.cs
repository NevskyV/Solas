using Solas.Serialization.Core;

namespace Solas.Serialization.Json;

public abstract class CustomJsonSerializer<T> : ICustomSerializer<T>
{
    public abstract void Write(T value, FileStream stream);

    public abstract T Read(FileStream stream);
}