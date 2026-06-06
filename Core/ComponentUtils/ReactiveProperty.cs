namespace Solas.ComponentUtils;

public class ReactiveProperty<T>()
{
    public Action<T> OnChange = delegate { };

    public T Value
    {
        get;
        set
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            OnChange.Invoke(field);
        }
    } = default;
}