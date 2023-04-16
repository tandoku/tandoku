namespace Tandoku.CommandLine.Abstractions;

public class SystemEnvironment : IEnvironment
{
    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
    public void SetEnvironmentVariable(string variable, string? value) => Environment.SetEnvironmentVariable(variable, value);
}
