using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orbitality.Components;

namespace Orbitality.World;

public class EntityJsonConverter : JsonConverter<Entity>
{
    public static Space InjectedSpace;

    public override Entity ReadJson(JsonReader reader, Type objectType, Entity existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        var jObject = JObject.Load(reader);

        var metaData = jObject["MetaData"]!.ToObject<EntityMetaData>(serializer);
        var logics = jObject["Logics"]!.ToObject<Logic[]>(serializer);
        var data = jObject["Data"]!.ToObject<IData[]>(serializer);
        var entity = Engine.Context.Creator.CreateEntity(InjectedSpace, metaData);
        using (var innerReader = jObject.CreateReader())
        {
            serializer.Populate(innerReader, entity);
        }

        foreach (var logic in logics)
        {
            if (logic == null) continue;

            var logicType = logic.GetType();
            var addMethod = typeof(Entity).GetMethod(nameof(Entity.AddLogic));
            if (addMethod != null)
            {
                var genericAdd = addMethod.MakeGenericMethod(logicType);
                genericAdd.Invoke(entity, null);
            }
        }
        
        foreach (var d in data)
        {
            if (d == null) continue;
            entity.AddData(d);
        }

        return entity;
    }

    public override void WriteJson(JsonWriter writer, Entity value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var originalConverters = serializer.Converters.ToList();
        serializer.Converters.Remove(this);

        try
        {
            serializer.Serialize(writer, value);
        }
        finally
        {
            serializer.Converters.Clear();
            foreach (var conv in originalConverters)
                serializer.Converters.Add(conv);
        }
    }
}