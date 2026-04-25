using Core.Components;
using Core.Containers;
using Core.Interfaces;
using Core.Systems;
using Core.World;

namespace Core;

public class Engine
{
    public static Engine Instance { get; } = new();
    private readonly Updater _updater = new();

    private readonly EngineContext _context = new EngineContext
    (
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
    
    public async void Test()
    {
        for (int i = 0; i < 100000; i++)
        {
            var newEntity = Context.Creator.CreateEntity();
            newEntity.AddData(new TextData("And I'm a unicorn!"));
            newEntity.AddLogic<TextLogic>();
        }
        Console.WriteLine($"Created Entities");
        await Task.Delay(1000);
        Context.SpaceSystem.SaveGlobalSpace();
        Instance.State = GameState.Pause;
        var gotEntity = Context.EntityPool.GetEntityWith(WorldContext.GlobalSpace,typeof(TextLogic), typeof(TextData));
        Console.WriteLine(gotEntity.MetaData);
        await Task.Delay(1000);
        Instance.State = GameState.Update;
        await Task.Delay(1000);
        Instance.State = GameState.None;
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

    public static List<Entity> GetEntities(Space space) => Instance._context.EntityPool.Entities[space];
    
    
    public record struct TextData(string Text) : IData;
    public class TextLogic : Logic, IInitializable, ILateUpdatable, IDestroyable
    {
        public void Initialize()
        {
            //Console.WriteLine($"{nameof(TextLogic)} initialized.");
            //Console.WriteLine($"I'm {Entity.MetaData.Name}!");
            //Console.WriteLine(Entity.GetData<TextData>().Text);
        }

        public void LateUpdate()
        {
            //Console.WriteLine($"Late Update Text Logic.");
        }
    
        public void Destroy()
        {
            //Console.WriteLine($"TextLogic destroyed.");
        }
    }
}