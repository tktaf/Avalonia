using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Build;

public sealed class BuildGuardTests
{
    private const string GuardMessage = "Avalonia.Diagnostics.AutomationBridge is dev-only and must not be included in Release, publish, or pack outputs.";

    [Fact]
    public void DebugBuild_Succeeds_WhenBridgeIsReferenced()
    {
        using var project = TemporaryBridgeConsumerProject.Create();

        var result = project.Run("build -c Debug");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(GuardMessage, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseBuild_Fails_WhenBridgeIsReferenced()
    {
        using var project = TemporaryBridgeConsumerProject.Create();

        var result = project.Run("build -c Release");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(GuardMessage, result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("publish -c Debug")]
    [InlineData("pack -c Debug")]
    public void PublishAndPack_Fail_WhenBridgeIsReferenced(string command)
    {
        using var project = TemporaryBridgeConsumerProject.Create();

        var result = project.Run(command);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(GuardMessage, result.Output, StringComparison.Ordinal);
    }

    private sealed class TemporaryBridgeConsumerProject : IDisposable
    {
        private TemporaryBridgeConsumerProject(string projectDirectory)
        {
            ProjectDirectory = projectDirectory;
        }

        public string ProjectDirectory { get; }

        public static TemporaryBridgeConsumerProject Create()
        {
            var projectDirectory = Path.Combine(Path.GetTempPath(), $"avalonia-bridge-guard-{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDirectory);

            var repoRoot = FindRepoRoot();
            var bridgeProjectPath = Path.Combine(
                repoRoot,
                "src",
                "Avalonia.Diagnostics.AutomationBridge",
                "Avalonia.Diagnostics.AutomationBridge.csproj");

            File.WriteAllText(
                Path.Combine(projectDirectory, "BridgeConsumer.csproj"),
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <IsPackable>true</IsPackable>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{{bridgeProjectPath}}" />
                  </ItemGroup>
                </Project>
                """);

            File.WriteAllText(
                Path.Combine(projectDirectory, "Program.cs"),
                """
                using Avalonia.Diagnostics.AutomationBridge;

                Console.WriteLine(typeof(AutomationBridgeOptions).FullName);
                """);

            return new TemporaryBridgeConsumerProject(projectDirectory);
        }

        public CommandResult Run(string command)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", command)
                {
                    WorkingDirectory = ProjectDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new CommandResult(process.ExitCode, stdout + stderr);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(ProjectDirectory, recursive: true);
            }
            catch
            {
            }
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Avalonia.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate the repository root.");
        }
    }

    private sealed record CommandResult(int ExitCode, string Output);
}
