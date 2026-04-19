using Core.Components;
using Core.Systems;
using Microsoft.Extensions.DependencyInjection;

namespace Core.World;

public class Space
{
    //DI
    private readonly IServiceScope _scope;
    public IServiceProvider Provider => _scope.ServiceProvider;
    public readonly Initializer Initializer;

    public readonly List<Entity> Entities;

    public Space(IServiceCollection services, List<Entity> entities)
    {
        Initializer = new Initializer(this);
        Entities = entities;
        
        _scope = services.BuildServiceProvider().CreateScope();
    }
}