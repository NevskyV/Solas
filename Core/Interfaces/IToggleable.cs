using Orbitality.ComponentUtils;

namespace Orbitality.Interfaces;

public interface IToggleable
{
    public ReactiveProperty<bool> IsEnabled { get; set; }
}