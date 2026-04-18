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
        var newEntity = AppContext.Creator.CreateEntity();
        newEntity.AddData(new TextData("And I'm ready!"));
        newEntity.AddLogic<TextLogic>();
    }
}

record struct TextData(string Text) : IData;
public class TextLogic : Logic, IInitializable
{
    public void OnInitialize()
    {
        Console.WriteLine($"{nameof(TextLogic)} initialized.");
        Console.WriteLine($"I'm {Entity.MetaData.Name}!");
        Console.WriteLine(Entity.GetData<TextData>().Text);
    }
}