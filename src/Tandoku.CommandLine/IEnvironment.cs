namespace Tandoku.CommandLine;

public interface IEnvironment
{
    string? GetEnvironmentVariable(string variable);
    void SetEnvironmentVariable(string variable, string? value);
}

public class EnvironmentWrapper : IEnvironment
{
    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
    public void SetEnvironmentVariable(string variable, string? value) => Environment.SetEnvironmentVariable(variable, value);
}

public class MockEnvironment : IEnvironment
{
    private readonly Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);

    public string? GetEnvironmentVariable(string variable) =>
        this.variables.TryGetValue(variable, out var value) ? value : null;

    public void SetEnvironmentVariable(string variable, string? value)
    {
        if (value is not null)
            this.variables[variable] = value;
        else
            this.variables.Remove(variable);
    }
}
