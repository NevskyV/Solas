using Solas.Components;
using Solas.ComponentUtils;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class EntitySerializer : ICustomSerializer<Entity>
{
    public void Write(Entity value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.BeginObject(stream, name);
        EngineContext.Serializer.Write(value.Id, stream, nameof(value.Id));
        EngineContext.Serializer.Write(value.MetaData, stream, nameof(value.MetaData));

        // =========================
        // Data
        // =========================

        EngineContext.Serializer.Write((uint)value.Data.Length, stream, nameof(value.Data.Length));
        EngineContext.Serializer.BeginObject(stream, "Data");
        foreach (var data in value.Data)
        {
            var type = data.GetType();
            EngineContext.Serializer.BeginObject(stream);
            EngineContext.Serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream,  nameof(Type));
            EngineContext.Serializer.Write(data, stream);
            EngineContext.Serializer.EndObject(stream);
            
            EngineContext.InjectSerializationRegistry.Write(data, stream);
        }
        EngineContext.Serializer.EndObject(stream);
        // =========================
        // Logic
        // =========================

        EngineContext.Serializer.Write((uint)value.Logics.Length, stream);
        EngineContext.Serializer.BeginObject(stream, "Logic");
        foreach (var logic in value.Logics)
        {
            var type = logic.GetType();
            EngineContext.Serializer.BeginObject(stream);
            EngineContext.Serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream);
            EngineContext.InjectSerializationRegistry.Write(logic, stream);
            EngineContext.Serializer.EndObject(stream);
        }
        EngineContext.Serializer.EndObject(stream);
        EngineContext.Serializer.EndObject(stream);
    }

    public Entity Read(FileStream stream)
    {
        var entity = new Entity(
            id: EngineContext.Serializer.ReadGuid(stream),
            entityMetaData: EngineContext.Serializer.Read<EntityMetaData>(stream));
        
        var dataLength = EngineContext.Serializer.ReadUInt32(stream);
        for (int i = 0; i < dataLength; i++)
        {
            var typeName = EngineContext.Serializer.ReadString(stream);
            var data = EngineContext.DataReadingRegistry.Read(typeName, stream);
            var guids = EngineContext.InjectSerializationRegistry.Read(typeName, stream);
            entity.AddData(data);
            
            EngineContext.DISystem.AddInjectable(data, guids);
        }
        
        var logicLength = EngineContext.Serializer.ReadUInt32(stream);
        for (int i = 0; i < logicLength; i++)
        {
            var typeName = EngineContext.Serializer.ReadString(stream);
            var guids = EngineContext.InjectSerializationRegistry.Read(typeName, stream);
            var logic = EngineContext.LogicAddingRegistry.AddLogic(typeName, entity);
            
            EngineContext.DISystem.AddInjectable(logic, guids);
        }
        
        return entity;
    }
}