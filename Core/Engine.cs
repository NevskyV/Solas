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
                    Test();
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
    
    async void Test()
    {
        await Task.Delay(500);
        var newEntity = Engine.Context.Creator.CreateEntity();
        newEntity.AddData(new TextData("And I'm a bitch!"));
        newEntity.AddLogic<TextLogic>();
        await Task.Delay(1000);
        Context.SpaceSystem.SaveGlobalSpace();
        Instance.State = GameState.Pause;
        var gotEntity = Engine.Context.EntityPool.GetEntityWith(Engine.WorldContext.GlobalSpace,
            new[] { typeof(TextLogic), typeof(TextData) });
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
        _updater.Start();
    }

    private void StopUpdate()
    {
        Time.TimeScale = 0;
    }

    private void StopGame()
    {
        _updater.Stop();
        Context.Destroyer.DestroyAll();
    }

    public static List<Entity> GetEntities(Space space) => Instance._context.EntityPool.Entities[space];
    
    
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
}