using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Solas.Registries;

public abstract class Registry
{
        protected Registry(Type registrationType)
    {
        var loadedDict = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

#pragma warning disable IL3002
        var runtimeLibraries = DependencyContext.Default?.RuntimeLibraries;
#pragma warning restore IL3002
        
        if (runtimeLibraries != null)
        {
            foreach (var lib in runtimeLibraries)
            {
                try
                {
                    var asm = Assembly.Load(new AssemblyName(lib.Name));
                    loadedDict[lib.Name] = asm;
                }
                catch
                {
                    // ignored
                }
            }
        }
        else
        {
            var bootstrapAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var queue = new Queue<Assembly>(bootstrapAssemblies);
            
            foreach (var asm in bootstrapAssemblies)
            {
                var name = asm.GetName().Name;
                if (name != null) loadedDict[name] = asm;
            }

            while (queue.Count > 0)
            {
                var currentAsm = queue.Dequeue();
                foreach (var refName in currentAsm.GetReferencedAssemblies())
                {
                    if (refName.Name != null && !loadedDict.ContainsKey(refName.Name))
                    {
                        try
                        {
                            var loadedAsm = Assembly.Load(refName);
                            loadedDict[refName.Name] = loadedAsm;
                            queue.Enqueue(loadedAsm);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
        }
        
        foreach (var (_,asm) in loadedDict)
        {
            if (asm.FullName != null && (asm.FullName.StartsWith("System") || asm.FullName.StartsWith("Microsoft")))
                continue;
            var types = asm.GetTypes();

            var serializerModules = types.Where(t =>
                registrationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
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