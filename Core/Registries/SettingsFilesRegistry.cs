namespace Solas.Registries;

public interface ISettingsFilesRegistration : IRegistration;

public class SettingsFilesRegistry() : Registry(typeof(ISettingsFilesRegistration))
{
    private readonly List<Action> _creators = [];

    public void Register(Action creator)
    {
        _creators.Add(creator);
    }

    public void CreateAll()
    {
        foreach (var creator in _creators)
            creator();
    }
}