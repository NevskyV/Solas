using System.Diagnostics;
using System.Reflection;

namespace Solas.Registries;

public abstract class Registry
{
    protected Registry(Type registrationType)
    {
        var loadedDict = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        
        var entryAssembly = Assembly.GetEntryAssembly() ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

        if (entryAssembly != null)
        {
            LoadReferencedAssemblies(entryAssembly, loadedDict);
        }
        
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && asm.GetName().Name != null)
            {
                loadedDict.TryAdd(asm.GetName().Name, asm);
                Debug.WriteLine($"Loaded assembly '{asm.GetName().Name}'.");
            }
        }
        
        foreach (var (_, asm) in loadedDict)
        {
            if (asm.FullName != null && (asm.FullName.StartsWith("System") || asm.FullName.StartsWith("Microsoft")))
                continue;

            try
            {
                var types = asm.GetTypes();
                var serializerModules = types.Where(t =>
                    registrationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var moduleType in serializerModules)
                {
                    var module = (IRegistration)Activator.CreateInstance(moduleType);
                    AddMethodsFromRegistration(module);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                var serializerModules = ex.Types.Where(t =>
                    t != null && registrationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var moduleType in serializerModules)
                {
                    var module = (IRegistration)Activator.CreateInstance(moduleType);
                    AddMethodsFromRegistration(module);
                }
            }
            catch
            {
                // Fully ignored assembly
            }
        }
    }
    
    private static void LoadReferencedAssemblies(Assembly assembly, Dictionary<string, Assembly> loadedDict)
    {
        var name = assembly.GetName().Name;
        if (name == null || loadedDict.ContainsKey(name)) return;
        
        if (name.StartsWith("System") || name.StartsWith("Microsoft") || name.StartsWith("netstandard"))
            return;

        loadedDict[name] = assembly;

        foreach (var refName in assembly.GetReferencedAssemblies())
        {
            if (refName.Name != null && !loadedDict.ContainsKey(refName.Name))
            {
                try
                {
                    var loadedAsm = Assembly.Load(refName);
                    LoadReferencedAssemblies(loadedAsm, loadedDict);
                }
                catch
                {
                    // Ignored
                }
            }
        }
    }

    private void AddMethodsFromRegistration(IRegistration registration)
    {
        registration.Add(this);
    }
}