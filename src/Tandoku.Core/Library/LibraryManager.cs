﻿namespace Tandoku.Library;

using System.IO.Abstractions;

public sealed class LibraryManager
{
    private const string LibraryDefinitionFileName = "library.tdkl.yaml";
    private const string DefaultLanguage = "ja";
    private const string DefaultReferenceLanguage = "en";

    private readonly IFileSystem fileSystem;

    public LibraryManager(IFileSystem? fileSystem = null)
    {
        this.fileSystem = fileSystem ?? new FileSystem();
    }

    public Task<LibraryInfo> InitializeAsync(string? path, bool force = false)
    {
        var pathInfo = this.fileSystem.DirectoryInfo.New(
            !string.IsNullOrEmpty(path) ? path : this.fileSystem.Directory.GetCurrentDirectory());
        
        return this.InitializeAsync(pathInfo, force);
    }

    private async Task<LibraryInfo> InitializeAsync(IDirectoryInfo pathInfo, bool force)
    {
        if (pathInfo.Exists)
        {
            if (!force && pathInfo.EnumerateFileSystemInfos().Any())
                throw new ArgumentException("The specified directory is not empty and force is not specified.");
        }
        else
        {
            // Note: this can throw if a conflicting file exists at the path
            pathInfo.Create();
        }

        var definition = new LibraryDefinition
        {
            Language = DefaultLanguage,
            ReferenceLanguage = DefaultReferenceLanguage,
        };

        var definitionPath = this.fileSystem.Path.Join(pathInfo.FullName, LibraryDefinitionFileName);

        using var definitionWriter = this.fileSystem.File.CreateText(definitionPath);
        await definition.WriteYamlAsync(definitionWriter);

        return new LibraryInfo(pathInfo.FullName, definitionPath);
    }

    public async Task<LibraryInfo> GetInfoAsync(string definitionPath)
    {
        throw new NotImplementedException();
    }
}
