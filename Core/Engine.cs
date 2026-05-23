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
        new SpaceSystem(),
        new DependencyInjector(),
        new SettingsSystem()
    );
    
    private IData[] _settingsContext;
    private CoreSettings _coreSettings;
    public static IData[] SettingsContext => Instance._settingsContext;
    public static CoreSettings CoreSettings => Instance._coreSettings;

    private Space _globalSpace;
    public static EngineContext Context => Instance._context;
    public static Space GlobalSpace => Instance._globalSpace;

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

    public void LoadEngineSettings(string pathToSettingsFolder)
    {
        _settingsContext = _context.SettingsSystem.ReadAllSettings(pathToSettingsFolder);
        _coreSettings = (CoreSettings)_settingsContext.First(x => x.GetType().IsAssignableTo(typeof(CoreSettings)));
    }

    public void CreateWorld()
    {
        _context.SpaceSystem.SetPaths(_coreSettings.LocalSpacesFolderPath);
        _globalSpace = _context.SpaceSystem.LoadSpace(_coreSettings.GlobalSpacePath);
    }

    private  void StartGame()
    {
        var initializationTasks = _globalSpace.Initializer.InitializeDependencies().ToList();
        initializationTasks.AddRange(_context.SpaceSystem.InitializeLocalSpaces());
        Task.WhenAll(initializationTasks.ToArray());
        State = GameState.Update;
    }

    private void StartUpdate()
    {
        Time.TimeScale = 1;
        _updater.Start(_context.EntityPool);
    }

    private void StopUpdate()
    {
        Time.TimeScale = 0;
    }

    private void StopGame()
    {
        _updater.Stop();
        _context.Destroyer.DestroyAll();
    }

    public static IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return Instance._context.EntityPool.GetEntitiesIn(space);
    }
}