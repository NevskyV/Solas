using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.GenericSerializers;

public interface IGenericSerializer
{
    public string Write(MemberMetadata member, string accessPath);
    public string Read(MemberMetadata member);
}