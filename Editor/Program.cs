using Core;
using Core.Components;
using Core.Interfaces;
using Core.Systems;

Engine e = new Engine();
e.State = GameState.Start;

//TEST
var newEntity = Creator.CreateEntity();
newEntity.AddData(new TextData("And I'm a bitch!"));
newEntity.AddLogic<TextLogic>();

await Task.Delay(1000);
e.State = GameState.Pause;
await Task.Delay(1000);
e.State = GameState.Update;
await Task.Delay(1000);
e.State = GameState.None;

//END TEST

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
