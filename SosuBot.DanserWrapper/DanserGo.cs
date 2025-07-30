using System.Diagnostics;
using System.Text;

namespace SosuBot.DanserWrapper;

public class DanserGo
{
    private readonly string _danserGoPath;

    public DanserGo(string danserGoPath)
    {
        _danserGoPath = danserGoPath ?? throw new ArgumentNullException(nameof(danserGoPath));
        
        if (!File.Exists(_danserGoPath))
        {
            throw new FileNotFoundException($"danser-go executable not found at: {_danserGoPath}");
        }
    }

    public async Task<DanserResult> ExecuteAsync(string arguments, int timeoutMs = 30000)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _danserGoPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_danserGoPath)
        };

        using var process = new Process { StartInfo = processStartInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Handle output asynchronously
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await process.WaitForExitAsync(TimeSpan.FromMilliseconds(timeoutMs));
        
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"danser-go process timed out after {timeoutMs}ms");
        }

        return new DanserResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
            Success = process.ExitCode == 0
        };
    }
/// <summary>
/// Synchronous version for simple cases
/// </summary>
/// <param name="arguments"></param>
/// <param name="timeoutMs"></param>
/// <returns></returns>
    public DanserResult Execute(string arguments, int timeoutMs = 30000)
    {
        return ExecuteAsync(arguments, timeoutMs).GetAwaiter().GetResult();
    }
}

public class DanserResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public bool Success { get; set; }
}