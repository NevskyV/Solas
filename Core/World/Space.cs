using Core.Components;
using Core.Systems;
using Microsoft.Extensions.DependencyInjection;

namespace Core.World;

public class Space
{
    //DI
    private readonly IServiceScope _scope;
    public IServiceProvider Provider => _scope.ServiceProvider;
    public readonly Initializer Initializer = new Initializer();

    public readonly List<Entity> Entities;

    public Space(IServiceCollection services, List<Entity> entities)
    {
        Entities = entities;
        services.AddSingleton(Engine.AppContext.Creator);
        services.AddSingleton(Engine.AppContext.Destroyer);
        
        _scope = services.BuildServiceProvider().CreateScope();
    }
}