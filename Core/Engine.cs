using Core.Components;
using Core.Containers;
using Core.Systems;
using Core.World;

namespace Core;

public class Engine
{
    public static Engine Instance { get; } = new();
    private readonly Updater _updater = new();

    private readonly EngineContext _context;
    private WorldContext _worldContext;
    public static EngineContext Context => Instance._context;
    public static WorldContext WorldContext => Instance._worldContext;

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
    
    private Engine()
    {
        _context = new EngineContext
        (
            new Creator(),
            new Destroyer(),
            new EntityPool(),
            new SpaceSystem()
        );
    }

    public void CreateWorld()
    {
        _worldContext = new WorldContext
        (
            _context.SpaceSystem.LoadGlobalSpace(),
            new()
        );
    }

    private async void StartGame()
    {
        var globalSpaceInitialization = _worldContext.GlobalSpace.Initializer.InitializeDependencies();
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
        Context.Destroyer.DestroyAll();
    }

    public static List<Entity> GetEntities(Space space) => Instance._context.EntityPool.Entities[space];
}