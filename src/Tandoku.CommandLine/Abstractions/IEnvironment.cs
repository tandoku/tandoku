namespace Tandoku.CommandLine.Abstractions;

public interface IEnvironment
{
    string? GetEnvironmentVariable(string variable);
    void SetEnvironmentVariable(string variable, string? value);
}
