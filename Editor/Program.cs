using Core;
using Core.Systems;

Engine e = new Engine();
e.State = GameState.Start;

//TEST
var newEntity = Engine.AppContext.Creator.CreateEntity();
newEntity.AddData(new TextData("And I'm a bitch!"));
newEntity.AddLogic<TextLogic>();

await Task.Delay(1000);
e.State = GameState.Pause;
await Task.Delay(1000);
e.State = GameState.Update;
await Task.Delay(1000);
e.State = GameState.None;

//END TEST
