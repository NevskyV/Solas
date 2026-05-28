namespace Orbitality.World;

public static class SpaceTree
{
    public static void Create(List<Space> spaces)
    {
        var lookup = spaces.ToLookup(x => x.RootId);
        foreach (var space in spaces)
        {
            space.BranchesIds = lookup[space.Id].Select(s => s.Id).ToList();
        }
    }

    public static void Attach(Space space, Space spaceToAttach)
    {
        if (space.Id == spaceToAttach.Id) return;

        Engine.Context.SpacePool.GetSpace(space.RootId).BranchesIds.Remove(spaceToAttach.Id);
        space.RootId = spaceToAttach.Id;
        spaceToAttach.BranchesIds.Add(space.Id);
    }

    public static void Detach(Space space)
    {
        var root = space.GetRoot();
        if (root != Engine.GlobalSpace)
        {
            foreach (var branch in space.GetBranches())
            {
                branch.RootId = root.Id;
            }
        }
    }

    public static List<Space> GetAllAvailableSpacesFor(Space space)
    {
        List<Space> result = [space];
        var lookupSpace = space;
        
        var visited = new HashSet<Guid> { space.Id }; 

        while (lookupSpace.RootId != Engine.GlobalSpace.Id)
        {
            var nextRootId = lookupSpace.RootId;
            if (!visited.Add(nextRootId)) break;

            lookupSpace = Engine.Context.SpacePool.GetSpace(nextRootId);
            result.Add(lookupSpace);
        }
        
        FillBranchables(space.BranchesIds, result);

        if (space != Engine.GlobalSpace)
            result.Add(Engine.GlobalSpace);
        
        return result;
    }
    
    private static void FillBranchables(List<Guid> branchIds, List<Space> accumulator)
    {
        foreach (var id in branchIds)
        {
            var space = Engine.Context.SpacePool.GetSpace(id);
            accumulator.Add(space);
            if (space.BranchesIds.Count > 0)
            {
                FillBranchables(space.BranchesIds, accumulator);
            }
        }
    }
}