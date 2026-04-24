using Core.Components;
using Newtonsoft.Json;

namespace Core.World;

public class SpaceSystem
{
    private const string GlobalSpacePath = @"D:\C# Projects\Orbitality\OrbitalityEngine\Core\World\Global.space";
    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        Converters = {new EntityJsonConverter()}
    };
    
    public Space LoadGlobalSpace() => LoadSpace(GlobalSpacePath).Result;
    public Space LoadLocalSpace(string path)
    {
        var space = LoadSpace(path).Result;
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
    
    public void SaveGlobalSpace() => SaveSpace(Engine.WorldContext.GlobalSpace, GlobalSpacePath);

    public async void SaveSpace(Space space, string path)
    {
        var text = JsonConvert.SerializeObject(Engine.GetEntities(space), _jsonSerializerSettings);
        await File.WriteAllTextAsync(path, text);
    }
}