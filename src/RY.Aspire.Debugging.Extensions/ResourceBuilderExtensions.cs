using System.Diagnostics;
using System.Text;

namespace RY.Aspire.Debugging.Extensions;

/// <summary>
/// Provides an extension method to configure the Visual Studio debugger inside a container.
/// </summary>
public static class ResourceBuilderExtensions
{
    public static IResourceBuilder<TResource> WithVsDebug<TResource>(this IResourceBuilder<TResource> builder)
        where TResource : IResourceWithEndpoints
    {
        string displayName = "Setup VS Debugger";
        string iconName = "WindowDevEditRegular";

        builder.WithCommand("setup-vs-debugger", displayName, async context =>
        {
            if (!builder.Resource.IsContainer())
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = "Resource is not a container." };
            }

            string containerName = builder.Resource.Name;
            try
            {
                // 1. Update apt-get and install wget
                await ExecuteDockerCommandAsync(
                    $"exec -u root {containerName} sh -c \"apt-get update && apt-get install wget -y\"");

                // 2. Download and configure the Visual Studio debugger
                await ExecuteDockerCommandAsync(
                    $"exec {containerName} sh -c \"mkdir -p ~/.vs-debugger && wget https://aka.ms/getvsdbgsh -O ~/.vs-debugger/GetVsDbg.sh && chmod a+x ~/.vs-debugger/GetVsDbg.sh\"");
            }
            catch (Exception ex)
            {
                return new ExecuteCommandResult { Success = false, ErrorMessage = $"Error configuring debugger: {ex.Message}" };
            }

            return new ExecuteCommandResult { Success = true };
        },
        iconName: iconName,
        iconVariant: IconVariant.Regular);

        return builder;
    }

    private static async Task ExecuteDockerCommandAsync(string arguments)
    {
        var processInfo = new ProcessStartInfo("docker", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process { StartInfo = processInfo };
        process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for up to 1 minute
        await Task.Run(() => process.WaitForExit(60000));

        if (!process.HasExited)
        {
            process.Kill();
        }

        int exitCode = process.ExitCode;
        string output = outputBuilder.ToString();
        string error = errorBuilder.ToString();

        if (exitCode != 0)
        {
            throw new Exception($"Docker command failed with exit code {exitCode}: {error}");
        }
    }
}
