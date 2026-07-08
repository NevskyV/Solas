using Solas.Interfaces;

namespace Solas.World;

internal static class SpaceTree
{
    internal static void Create(List<Space> spaces)
    {
        var lookup = spaces.ToLookup(x => x.RootId);
        foreach (var space in spaces) space.BranchesIds = lookup[space.Id].Select(s => s.Id).ToList();
    }

    internal static void Attach(Space space, Space spaceToAttach)
    {
        if (space.Id == spaceToAttach.Id) return;

        EngineContext.SpacePool.GetSpace(space.RootId).BranchesIds.Remove(spaceToAttach.Id);
        space.RootId = spaceToAttach.Id;
        spaceToAttach.BranchesIds.Add(space.Id);
    }

    internal static void Detach(Space space)
    {
        var root = space.GetRoot();
        if (root != WorldContext.GlobalSpace)
            foreach (var branch in space.GetBranches())
                branch.RootId = ((IReferenceable)root).Id;
    }

    internal static List<Space> GetAllAvailableSpacesFor(Space space)
    {
        List<Space> result = [space];
        var lookupSpace = space;

        var visited = new HashSet<Guid> { space.Id };
        while (lookupSpace.RootId != Guid.Empty && lookupSpace.RootId != WorldContext.GlobalSpace.Id)
        {
            var nextRootId = lookupSpace.RootId;
            if (!visited.Add(nextRootId)) break;

            lookupSpace = EngineContext.SpacePool.GetSpace(nextRootId);
            result.Add(lookupSpace);
        }

        FillBranchables(space.BranchesIds, result);

        if (space != WorldContext.GlobalSpace)
            result.Add(WorldContext.GlobalSpace);

        return result;
    }

    private static void FillBranchables(List<Guid> branchIds, List<Space> accumulator)
    {
        foreach (var id in branchIds)
        {
            var space = EngineContext.SpacePool.GetSpace(id);
            accumulator.Add(space);
            if (space.BranchesIds.Count > 0) FillBranchables(space.BranchesIds, accumulator);
        }
    }
}