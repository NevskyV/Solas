using Core;

Engine e = Engine.Instance;
e.CreateWorld();
e.Test();
e.State = GameState.Start;
e.State = GameState.None;
