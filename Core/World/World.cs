namespace Orbitality.World;

public record struct World()
{
    public Space GlobalSpace { get; init; }
    public List<Space> LocalSpaces = [];
}