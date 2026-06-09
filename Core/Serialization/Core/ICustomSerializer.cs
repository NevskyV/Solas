namespace Solas.Serialization.Core;

public interface ICustomSerializer<T>
{
    public void Write(T value, FileStream stream);
    public T Read(FileStream stream);
}