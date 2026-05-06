using Microsoft.Extensions.DependencyInjection;
using Orbitality.Systems;

namespace Orbitality.World;

public class Space
{
    public string Name { get; init; }

    //DI
    private readonly IServiceScope _scope;
    public IServiceProvider Provider => _scope.ServiceProvider;
    public readonly Initializer Initializer;

    public Space(string name)
    {
        Name = name;
        Initializer = new Initializer(this);
        _scope = new ServiceCollection().BuildServiceProvider().CreateScope();
        Engine.Context.EntityPool.RegisterNewSpace(this);
    }
}