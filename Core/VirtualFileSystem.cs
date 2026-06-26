namespace Solas;

public class VirtualFileSystem(string rootDirectory)
{
    private readonly Dictionary<string, string> _mounts = [];

    public void Mount(string mountName, string mountPath)
    {
        _mounts[mountName] = Path.Combine(rootDirectory, mountPath);
    }
    
    public string GetMountPath(string mount)
    {
        return _mounts[mount];
    }

    public string GetPath(string path)
    {
        var parts = path.Split("://");

        if (parts.Length == 1)
            return GetMountPath(parts[0]);
        var (mount, relativePath) = (parts[0], parts[1]);

        return Path.Combine(_mounts[mount],relativePath);
    }
}