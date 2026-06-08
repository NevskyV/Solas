using System.Text;

namespace Solas.SourceGenerators.Utils;

public sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indentLevel;

    public void Indent() => _indentLevel++;
    public void Unindent() => _indentLevel = Math.Max(0, _indentLevel - 1);

    public void WriteLine(string line = "")
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            _builder.AppendLine();
            return;
        }

        var indent = new string(' ', _indentLevel * 4);
        _builder.AppendLine($"{indent}{line}");
    }

    public void WriteLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            WriteLine(line);
        }
    }

    public override string ToString() => _builder.ToString();
}