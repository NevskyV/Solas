using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orbitality.Components;

namespace Orbitality.World;

public class SpaceSystem()
{
    private string _globalSpacePath;
    private string _localSpacesFolder;
    private string[] _localSpacesPaths;
    private readonly HashSet<Space> _localSpaces = [];

    public void SetPaths(string globalSpacePath, string localSpacesFolder)
    {
        _globalSpacePath = globalSpacePath;
        _localSpacesFolder = localSpacesFolder;
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

    public List<Task> InitializeLocalSpaces()
    {
        List<Task> result = [];
        foreach (var space in _localSpaces)
        {
            result.AddRange(space.Initializer.InitializeDependencies());
        }
        return result;
    }

    public Space LoadGlobalSpace()
    {
        return LoadSpace(_globalSpacePath).Result;
    }

    public Space LoadLocalSpace(string name)
    {
        var space = LoadSpace(_localSpacesPaths.First(x => Path.GetFileNameWithoutExtension(x) == name)).Result;
        _localSpaces.Add(space);
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
        Engine.Context.Destroyer.AddSpace(space);
        return space;
    }

    public void SaveGlobalSpace()
    {
        SaveSpace(Engine.GlobalSpace, _globalSpacePath);
    }

    public async void SaveSpace(Space space, string path)
    {
        var text = JsonConvert.SerializeObject(Engine.GetEntitiesIn(space), _jsonSerializerSettings);
        await File.WriteAllTextAsync(path, text);
    }

    public void SaveEntity(Entity entity)
    {
        string tempPath = Path.Combine(
            Path.GetDirectoryName(_localSpacesFolder)!, 
            Guid.NewGuid() + ".tmp"
        );
        
        try
        {
            var allSpaces = _localSpacesPaths.ToList();
            allSpaces.Add(_globalSpacePath);
            string originalPath = allSpaces.First(x => x.EndsWith(entity.CurrentSpace.Name + ".space"));
            
            using (var sr = new StreamReader(originalPath))
            using (var jReader = new JsonTextReader(sr))
            using (var sw = new StreamWriter(tempPath))
            using (var jWriter = new JsonTextWriter(sw))
            {
                jWriter.Formatting = Formatting.Indented;
                while (jReader.Read())
                {
                    if (jReader.TokenType == JsonToken.StartObject)
                    {
                        JObject currentObj = JObject.Load(jReader);
                        
                        if (currentObj["Id"]?.ToObject<Guid>() == entity.Id)
                        {
                            JObject.FromObject(entity).WriteTo(jWriter);
                        }
                        else
                        {
                            currentObj.WriteTo(jWriter);
                        }
                    }
                    else
                    {
                        jWriter.WriteToken(jReader, false);
                    }
                    
                }
            }
            File.Replace(tempPath, originalPath, null);
            File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw new Exception($"Ошибка при обновлении JSON: {ex.Message}");
        }
    }
}