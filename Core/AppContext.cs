using Core.Containers;
using Core.Systems;

namespace Core;

public record AppContext(EntityPool EntityPool, Creator Creator, Destroyer Destroyer);

public record WorldContext;