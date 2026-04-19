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
        SpaceReader.GetGlobalSpace(),
        []
    );

    public GameState State
    {
        get;
        set
        {
            field = value;
            switch (value)
            {
                case GameState.Start:
                    StartGame();
                    break;

                case GameState.Update:
                    StartUpdate();
                    break;

                case GameState.Pause:
                    StopUpdate();
                    break;

                case GameState.None:
                    StopGame();
                    break;
            }
        }
    }

    private async void StartGame()
    {
        var globalSpaceInitialization = WorldContext.GlobalSpace.Initializer.InitializeDependencies();
        await Task.WhenAll(globalSpaceInitialization);
        State = GameState.Update;
    }

    private async void StartUpdate()
    {
        Time.TimeScale = 1;
        AppContext.Updater.SetupUpdatables(WorldContext.GlobalSpace);
        AppContext.Updater.Start();
        while (State != GameState.None)
        {
            AppContext.Updater.Tick();
            await Task.Yield();
        }
    }
    
    private void StopUpdate()
    {
        Console.WriteLine($"Paused.");
        Time.TimeScale = 0;
    }

    private void StopGame()
    {
        AppContext.Updater.Stop();
        AppContext.Destroyer.DestroyAll();
    }
}

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