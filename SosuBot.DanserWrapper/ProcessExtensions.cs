using System.Diagnostics;

namespace SosuBot.DanserWrapper;

public static class ProcessExtensions
{
    /// <summary>
    /// Extension method for timeout support
    /// </summary>
    /// <param name="process"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}