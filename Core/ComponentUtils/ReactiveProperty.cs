namespace Orbitality.ComponentUtils;

public class ReactiveProperty<T>(T value)
{
    private readonly List<Action<T>> _listeners = [];

    public T Value
    {
        get;
        set
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
 
            for (var i = 0; i < _listeners.Count; i++)
                _listeners[i](field);
        }
    } = value;

    public IDisposable Subscribe(Action<T> listener)
    {
        _listeners.Add(listener);
        return new Subscription(this, listener);
    }

    public void Unsubscribe(Action<T> listener)
    {
        _listeners.Remove(listener);
    }

    private class Subscription(ReactiveProperty<T> prop, Action<T> listener) : IDisposable
    {
        private ReactiveProperty<T> _prop = prop;
        private Action<T> _listener = listener;

        public void Dispose()
        {
            _prop?.Unsubscribe(_listener);
            _prop = null;
            _listener = null;
        }
    }
}