namespace Solas.Serialization.Core;

public interface ICustomSerializer<T>
{
    public void Write(T value, FileStream stream, string name);
    public T Read(FileStream stream);
}