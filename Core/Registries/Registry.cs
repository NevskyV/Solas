namespace Solas.Registries;

public abstract class Registry
{
    protected Registry(Type registrationType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            if (assembly.FullName != null && (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("Microsoft"))) 
                continue;
            
            var types = assembly.GetTypes();
            
            var serializerModules = types.Where(t => registrationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            foreach (var moduleType in serializerModules)
            {
                var module = (IRegistration)Activator.CreateInstance(moduleType);
                AddMethodsFromRegistration(module);
            }
        }
    }

    private void AddMethodsFromRegistration(IRegistration registration)
    {
        registration.Add(this);
    }
}