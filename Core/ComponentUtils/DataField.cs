using Orbitality.ComponentUtils.Modifiers;

namespace Orbitality.ComponentUtils;

public class DataField<T>(T value)
{
    public Action<T> OnChange =  delegate { };

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
    } = value;
    
    private readonly List<DataModifier<T>> _modifiers = [];

    public TModifier AddModifier<TModifier>() where TModifier : DataModifier<T>, new()
    {
        var newModifier = _modifiers.FirstOrDefault(m => m is TModifier) as TModifier;
        if (newModifier == null)
        {
            newModifier = new TModifier() { Field = this };
            _modifiers.Add(newModifier);
        }

        return newModifier;
    }

    public void RemoveModifier<TModifier>() where TModifier : DataModifier<T>
    {
        _modifiers.RemoveAll(m => m is TModifier);
    }
    
    public TModifier GetModifier<TModifier>() where TModifier : DataModifier<T>
    {
        return _modifiers.Find(m => m is TModifier) as TModifier;
    }
}