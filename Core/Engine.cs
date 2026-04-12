using Core.Containers;
using Core.Systems;

namespace Core;

public class Engine
{
    public static readonly AppContext AppContext = new AppContext
    (
        new EntityPool(),
        new Creator(),
        new Destroyer()
    );

    public void Boot()
    {
        
    }
}