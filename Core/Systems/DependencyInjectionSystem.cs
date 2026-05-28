using System.Collections;
using System.Reflection;
using Orbitality.Attributes;
using Orbitality.Components;
using Orbitality.Containers;
using Orbitality.World;

namespace Orbitality.Systems;

public class DependencyInjectionSystem
{
    private readonly Dictionary<Space, DependencyPool> _cache = [];
    
    public void BuildDependencies(Space space)
    {
        List<Type> typesToInject = [];
        List<Type> typesToBuild = [];
        var entityPool = Engine.Context.EntityPool;
        foreach (var type in entityPool.GetTypesWithComponentPoolsIn(space).Keys)
        {
            var members = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.MemberType is MemberTypes.Field or MemberTypes.Property 
                    && m.GetCustomAttribute<AutoInjectAttribute>() != null &&
                    !typesToBuild.Contains(m.ReflectedType))
                .Select(m =>
                {
                    if (m is FieldInfo fieldInfo)
                        return fieldInfo.FieldType;
                    return ((PropertyInfo)m).PropertyType;
                }).ToArray();
            
            if (members.Any())
            {
                typesToInject.Add(type);
                typesToBuild.AddRange(members);
            }
        }

        var dependencyPool = new DependencyPool((uint)typesToBuild.Count);
        var componentMethod = typeof(EntityPool).GetMethod(nameof(EntityPool.GetComponentBySingleType));
        
        foreach (var type in typesToBuild)
        {
            var genericMethod = componentMethod!.MakeGenericMethod(type);
            if(genericMethod!.Invoke(entityPool, [space]) is Logic result)
                dependencyPool.Add(result);
            else if(genericMethod!.Invoke(entityPool, [Engine.GlobalSpace]) is Logic globalResult)
                dependencyPool.Add(globalResult);
        }
        
        var componentsMethod = typeof(EntityPool).GetMethod(nameof(EntityPool.GetComponentsBySingleType));
        foreach (var type in typesToInject)
        {
            var injectMethod = type.GetMethod("__AutoInject");
            var genericMethod = componentsMethod!.MakeGenericMethod(type);

            foreach (var logic in ((IEnumerable)genericMethod.Invoke(entityPool, [space]))!)
            {
                injectMethod?.Invoke(logic, [dependencyPool]);
            }
        }
        
        _cache.Add(space, dependencyPool);
    }

    public void InjectEntity(Logic logic, Space space)
    {
        if (!_cache.ContainsKey(space)) return;
        var injectMethod = logic.GetType().GetMethod("__AutoInject");
        injectMethod?.Invoke(logic, [_cache[space]]);
    }
}