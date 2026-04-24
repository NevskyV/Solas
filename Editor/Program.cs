using Core;
using Core.Components;
using Core.Interfaces;

Engine e = Engine.Instance;
e.CreateWorld();
e.State = GameState.Start;

//TEST
var newEntity = Engine.Context.Creator.CreateEntity();
newEntity.AddData(new TextData("And I'm a bitch!"));
newEntity.AddLogic<TextLogic>();
await Task.Delay(1000);
Engine.Context.SpaceSystem.SaveGlobalSpace();
e.State = GameState.Pause;
var gotEntity = Engine.Context.EntityPool.GetEntityWith(Engine.WorldContext.GlobalSpace,
    new[] { typeof(TextLogic), typeof(TextData) });
Console.WriteLine(gotEntity.MetaData);
await Task.Delay(1000);
e.State = GameState.Update;
await Task.Delay(1000);
e.State = GameState.None;

//END TEST

[Serializable]
public record struct TextData(string Text) : IData;
public class TextLogic : Logic, IInitializable, IFixedUpdatable, IDestroyable
{
    public void Initialize()
    {
        Console.WriteLine($"{nameof(TextLogic)} initialized.");
        Console.WriteLine($"I'm {Entity.MetaData.Name}!");
        Console.WriteLine(Entity.GetData<TextData>().Text);
    }

    public void FixedUpdate()
    {
        Console.WriteLine($"Fixed Update Text Logic.");
    }
    
    public void Destroy()
    {
        Console.WriteLine($"TextLogic destroyed.");
    }
}
