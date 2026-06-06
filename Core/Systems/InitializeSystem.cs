using Solas.Components;
using Solas.Containers;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Systems;

internal class InitializeSystem(Space space)
{
    internal InitializationPool Pool;
    
    internal IEnumerable<Task> InitializeDependencies()
    {
        var entities = Engine.GetEntitiesIn(space).ToArray();

        var guidsCount = Pool.OrderedEntitiesIds.Length;
        Entity[] orderedEntities = new Entity[guidsCount];

        if (Pool.OrderType == InitializationOrder.Custom)
        {
            var entitiesCopy = entities.ToArray();
            for (var i = 0; i < guidsCount; i++)
            {
                entities[i] = entitiesCopy.First(e => e.Id == Pool.OrderedEntitiesIds[i]);
            }
        }
        else if (Pool.OrderType != InitializationOrder.Random)
        {
            Entity[] result = new Entity[entities.Length];
            var count = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                for (var j = 0; j < guidsCount; j++)
                {
                    if (entities[i].Id == Pool.OrderedEntitiesIds[j])
                    {
                        orderedEntities[j] = entities[i];
                        entities[i] = null;
                        break;
                    }
                }

                if (entities[i] != null && Pool.OrderType == InitializationOrder.Suffixal)
                {
                    result[count] = entities[i];
                    count++;
                }
            }

            for (var j = 0; j < guidsCount; j++)
            {
                result[count] = orderedEntities[j];
                count++;
            }

            if (Pool.OrderType == InitializationOrder.Prefixal)
            {
                foreach (var entity in entities)
                {
                    if (entity != null)
                    {
                        result[count] = entity;
                        count++;
                    }
                }
            }
            
            entities = result;
        }

        var allTasks = entities.SelectMany(entity => entity.Logics.ToArray().Select(InitializeLogic));
        return allTasks;
    }

    private async Task InitializeLogic(Logic logic)
    {
        await Task.Run((logic as IInitializable).Initialize);
    }
}