using Core.Components;
using Core.Containers;
using Core.Interfaces;
using Core.Systems;
using Core.World;

namespace Core;

public class Engine
{
    public static readonly AppContext AppContext = new AppContext
    (
        new EntityPool(),
        new Creator(),
        new Destroyer(),
        new Updater()
    );
    
    public static readonly WorldContext WorldContext = new WorldContext
    (
        SpaceReader.GetGlobalSpace()
    );

    public void Boot()
    {
        var globalSpaceInitialization = WorldContext.GlobalSpace.Initializer.InitializeDependencies();
        Task.WaitAll(globalSpaceInitialization);
        AppContext.Updater.SetupUpdatables(WorldContext.GlobalSpace);
        var newEntity = AppContext.Creator.CreateEntity();
        newEntity.AddData(new TextData("And I'm ready!"));
        newEntity.AddLogic<TextLogic>();
        AppContext.Updater.Run();
    }
}

record struct TextData(string Text) : IData;
public class TextLogic : Logic, IInitializable, IUpdatable
{
    public void Initialize()
    {
        Console.WriteLine($"{nameof(TextLogic)} initialized.");
        Console.WriteLine($"I'm {Entity.MetaData.Name}!");
        Console.WriteLine(Entity.GetData<TextData>().Text);
    }

    public void Update()
    {
        Console.WriteLine($"Update");
    }
}