using System.Numerics;

namespace Solas.Transform;

public static class WorldTransform
{
    public static Vector3 GetWorldPosition(TransformData data)
    {
        var space = data.Entity.CurrentSpace;
        var resultPosition = data.Position.Value;

        var allEntitiesWithTransformData = Query.GetEntitiesByType<TransformData>(space).ToArray();
        var currentData = data;
        var foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        while (foundEntity != null)
        {
            currentData = foundEntity.GetData<TransformData>();
            resultPosition += currentData.Position.Value;
            foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        }

        return resultPosition;
    }
    
    public static Vector3 GetWorldRotation(TransformData data)
    {
        var space = data.Entity.CurrentSpace;
        var resultRotation = data.Rotation.Value;

        var allEntitiesWithTransformData = Query.GetEntitiesByType<TransformData>(space).ToArray();
        var currentData = data;
        var foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        while (foundEntity != null)
        {
            currentData = foundEntity.GetData<TransformData>();
            resultRotation += currentData.Rotation.Value;
            foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        }

        return resultRotation;
    }
    
    public static Vector3 GetWorldScale(TransformData data)
    {
        var space = data.Entity.CurrentSpace;
        var resultScale = data.Scale.Value;

        var allEntitiesWithTransformData = Query.GetEntitiesByType<TransformData>(space).ToArray();
        var currentData = data;
        var foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        while (foundEntity != null)
        {
            currentData = foundEntity.GetData<TransformData>();
            resultScale *= currentData.Scale.Value;
            foundEntity = allEntitiesWithTransformData.FirstOrDefault(x => x.Id == currentData.RootId);
        }

        return resultScale;
    }
}