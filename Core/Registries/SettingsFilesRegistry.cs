namespace Solas.Registries;

public interface ISettingsFilesRegistration : IRegistration;

public class SettingsFilesRegistry() : Registry(typeof(ISettingsFilesRegistration))
{
    private readonly List<Action> _creators = [];

    public void Register(Action creator)
    {
        _creators.Add(creator);
        Console.WriteLine($"Registering {creator.Method}");
    }

    public void CreateALl()
    {
        foreach (var creator in _creators)
            creator();
    }
}