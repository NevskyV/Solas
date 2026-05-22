namespace Orbitality.Attributes;

[AttributeUsage(AttributeTargets.Struct)]
public class SettingsSectionAttribute(string Name) : Attribute
{
}