using Newtonsoft.Json;

namespace Orbitality.ComponentUtils;

public class ReactiveProperty<T>(T value)
{
    [JsonIgnore] private readonly List<Action<T>> _listeners = [];

    public T Value
    {
        get;
        set
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            if (_listeners == null) return;

            for (var i = 0; i < _listeners.Count; i++)
                _listeners[i](field);
        }
    } = value;

    public void Subscribe(Action<T> listener)
    {
        _listeners.Add(listener);
    }

    public void Unsubscribe(Action<T> listener)
    {
        _listeners.Remove(listener);
    }
}