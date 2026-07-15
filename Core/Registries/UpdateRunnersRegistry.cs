namespace Solas.Registries;

public interface IUpdateRunnersRegistration : IRegistration
{
    public void RegisterAssembly();
}

public sealed class UpdateRunnersRegistry() : Registry(typeof(IUpdateRunnersRegistration))
{
    private readonly List<IUpdateRunnersRegistration> _runnersRegistrations = new();
    public void AddRegistration(IUpdateRunnersRegistration runnersRegistration)
    {
        _runnersRegistrations.Add(runnersRegistration);
    }

    internal void RegisterAll()
    {
        foreach (var registration in _runnersRegistrations)
        {
            registration.RegisterAssembly();
        }   
    }
}