using Core.Containers;
using Core.Systems;
using Core.World;

namespace Core;

public class Engine
{
    private readonly Updater _updater = new();

    public static readonly AppContext AppContext = new AppContext
    (
        new EntityPool()
    );

    public static readonly WorldContext WorldContext = new WorldContext
    (
        SpaceReader.GetGlobalSpace(),
        new()
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
        _updater.Start();
        while (State != GameState.None)
        {
            _updater.Tick();
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
        _updater.Stop();
        Destroyer.DestroyAll();
    }
}