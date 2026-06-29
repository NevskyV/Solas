using Solas.Components;
using Solas.ComponentUtils;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class EntitySerializer : ICustomSerializer<Entity>
{
    public void Write(Entity value, FileStream stream, Serializer serializer, string name = null)
    {
        serializer.Write(value.Id, stream, nameof(value.Id));
        serializer.Write(value.MetaData, stream, nameof(value.MetaData));

        // Data
        serializer.Write((uint)value.Data.Length, stream, "DataCount");
        serializer.BeginObject(stream, "Data");
        foreach (var data in value.Data)
        {
            var type = data.GetType();
            serializer.BeginObject(stream);
            serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream, nameof(Type));
            EngineContext.DataSerializationRegistry.Write(type, data, stream, data.GetType().Name);
            data.WriteInject(stream, value);
            serializer.EndObject(stream);
        }

        serializer.EndObject(stream);

        // Logic
        serializer.Write((uint)value.Logics.Length, stream, "LogicsCount");
        serializer.BeginObject(stream, "Logic");
        foreach (var logic in value.Logics)
        {
            var type = logic.GetType();
            serializer.BeginObject(stream);
            serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream, nameof(Type));
            logic.WriteInject(stream, value);
            serializer.EndObject(stream);
        }

        serializer.EndObject(stream);
    }

    public Entity Read(FileStream stream)
    {
        var entity = new Entity(
            EngineContext.Serializer.ReadGuid(stream),
            entityMetaData: EngineContext.Serializer.Read<EntityMetaData>(stream));

        var dataLength = EngineContext.Serializer.ReadUInt32(stream);
        for (var i = 0; i < dataLength; i++)
        {
            var typeName = EngineContext.Serializer.ReadString(stream);
            var data = EngineContext.DataSerializationRegistry.Read(typeName, stream);
            var guids = data.ReadInject(stream);
            entity.AddData(data);

            EngineContext.DISystem.AddInjectable(data, guids);
        }

        var logicLength = EngineContext.Serializer.ReadUInt32(stream);
        for (var i = 0; i < logicLength; i++)
        {
            var typeName = EngineContext.Serializer.ReadString(stream);
            var logic = EngineContext.LogicAddingRegistry.AddLogic(typeName, entity);
            var guids = logic.ReadInject(stream);
            
            EngineContext.DISystem.AddInjectable(logic, guids);
        }

        return entity;
    }
}