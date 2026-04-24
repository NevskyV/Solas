using System.Collections;

namespace Core.Components;

public class ComponentMask
{
    private readonly BitArray _bits = new(1024);

    public void Set(Type type) => _bits.Set(ComponentRegistry.GetId(type), true);
    
    public bool ContainsAll(ComponentMask filter)
    {
        for (int i = 0; i < _bits.Length; i++)
        {
            if (filter._bits[i] && !_bits[i]) return false;
        }
        return true;
    }
}