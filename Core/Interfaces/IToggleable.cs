using Solas.ComponentUtils;

namespace Solas.Interfaces;

public interface IToggleable
{
    public ReactiveProperty<bool> IsEnabled { get; set; }
}