using Orbitality.Components;
using Orbitality.Containers;
using Orbitality.Systems;
using Orbitality.World;

namespace Orbitality;

public class Engine
{
    public static Engine Instance { get; } = new();
    private readonly Updater _updater = new();

    private readonly EngineContext _context = new(
        new Creator(),
        new Destroyer(),
        new EntityPool(),
        new SpaceSystem()
    );

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

    public void CreateWorld(string globalSpacePath, string localSpacesFolder)
    {
        Context.SpaceSystem.SetPaths(globalSpacePath, localSpacesFolder);
        _worldContext = new WorldContext
        (
            _context.SpaceSystem.LoadGlobalSpace(),
            new HashSet<Space>()
        );
    }

    private async void StartGame()
    {
        var globalSpaceInitialization = _worldContext.GlobalSpace.Initializer.InitializeDependencies();
        await Task.WhenAll(globalSpaceInitialization);
        State = GameState.Update;
    }

    private void StartUpdate()
    {
        Time.TimeScale = 1;
        _updater.Start(_context.EntityPool);
    }

    private void StopUpdate()
    {
        Console.WriteLine("Pause.");
        Time.TimeScale = 0;
    }

    private void StopGame()
    {
        _updater.Stop();
        Context.Destroyer.DestroyAll();
    }

    public static List<Entity> GetEntities(Space space)
    {
        return Instance._context.EntityPool.Entities[space];
    }
}