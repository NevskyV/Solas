using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Solas.Registries;

public abstract class Registry
{
    protected Registry(Type registrationType)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        var loadedDict = new Dictionary<string, Assembly>();

        foreach (var asm in loaded)
        {
            loadedDict[asm.GetName().Name] = asm; 
        }
        
        foreach (var lib in DependencyContext.Default!.RuntimeLibraries)
        {
            if (!loadedDict.TryGetValue(lib.Name, out var asm))
            {
                try
                {
                    asm = Assembly.Load(new AssemblyName(lib.Name));
                }
                catch
                {
                    continue;
                }
            }

            if (asm.FullName != null && (asm.FullName.StartsWith("System") || asm.FullName.StartsWith("Microsoft"))) 
                continue;
            var types = asm.GetTypes();
            
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