namespace Directory.Tests.Unit.TestSupport;

internal static class FakeDbMember
{
    internal static InvalidOperationException NotSet(string memberName, string typeName) =>
        new InvalidOperationException($"{memberName} was never set on this {typeName}.");

    internal static IEnumerable<string> ParameterReferences(string commandText)
    {
        for (var start = 0; start < commandText.Length; start++)
        {
            if (commandText[start] != '@')
            {
                continue;
            }

            var end = start + 1;
            while (end < commandText.Length && (char.IsLetterOrDigit(commandText[end]) || commandText[end] == '_'))
            {
                end++;
            }

            if (end > start + 1 && char.IsLetter(commandText[start + 1]))
            {
                yield return commandText[start..end];
            }
        }
    }
}
