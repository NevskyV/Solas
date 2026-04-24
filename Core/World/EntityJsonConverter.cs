using System.Text.Json;
using Core.Components;
using Core.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Core.World;

public class EntityJsonConverter : JsonConverter<Entity>
{
    public static Space InjectedSpace;
    public override Entity ReadJson(JsonReader reader, Type objectType, Entity existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == (JsonToken)JsonTokenType.Null)
            return null;
        JObject jObject = JObject.Load(reader);
        
        EntityMetaData metaData = jObject["MetaData"]!.ToObject<EntityMetaData>(serializer);
        Entity entity = Engine.Context.Creator.CreateEntity(InjectedSpace, metaData);
        using (var innerReader = jObject.CreateReader())
        {
            serializer.Populate(innerReader, entity);
        }
        
        var savedLogics = new List<Logic>(entity.Logics);
        entity.Logics.Clear();

        foreach (var logic in savedLogics)
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
        serializer.Converters.Remove(this);   // убираем себя

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