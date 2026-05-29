namespace Solas.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class UpdateAttribute : Attribute
{
    public bool Parallel { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
public class FixedUpdateAttribute : Attribute
{
    public bool Parallel { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
public class LateUpdateAttribute : Attribute
{
    public bool Parallel { get; set; }
}