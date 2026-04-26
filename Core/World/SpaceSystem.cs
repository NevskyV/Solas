using Newtonsoft.Json;
using Orbitality.Components;

namespace Orbitality.World;

public class SpaceSystem()
{
    private string _globalSpacePath;
    private string[] _localSpacesPaths;

    public void SetPaths(string globalSpacePath, string localSpacesFolder)
    {
        _globalSpacePath = globalSpacePath;
        _localSpacesPaths = Directory.GetFiles(localSpacesFolder, "*.space", SearchOption.AllDirectories);
    }

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        Converters = { new EntityJsonConverter() }
    };

    public Space LoadGlobalSpace()
    {
        return LoadSpace(_globalSpacePath).Result;
    }

    public Space LoadLocalSpace(string name)
    {
        var space = LoadSpace(_localSpacesPaths.First(x => Path.GetFileNameWithoutExtension(x) == name)).Result;
        Engine.WorldContext.LocalSpaces.Add(space);
        return space;
    }

    private async Task<Space> LoadSpace(string path)
    {
        var space = new Space(Path.GetFileNameWithoutExtension(path));
        Console.WriteLine($"Loading space: {space.Name}");
        if (File.Exists(path))
        {
            EntityJsonConverter.InjectedSpace = space;
            JsonConvert.DeserializeObject<List<Entity>>(await File.ReadAllTextAsync(path), _jsonSerializerSettings);
        }

        return space;
    }

    public void SaveGlobalSpace()
    {
        SaveSpace(Engine.WorldContext.GlobalSpace, _globalSpacePath);
    }

    public async void SaveSpace(Space space, string path)
    {
        var text = JsonConvert.SerializeObject(Engine.GetEntities(space), _jsonSerializerSettings);
        await File.WriteAllTextAsync(path, text);
    }
}