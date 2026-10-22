namespace MitaAI.Plugin;

internal static class FailureCleanup
{
    // Every step runs even if an earlier one throws, so a failed binding reset cannot leave the voice worker resident.
    internal static void Run(Action<string> report, params (string Name, Action Step)[] steps)
    {
        foreach (var (name, step) in steps)
        {
            try { step(); }
            catch (Exception error) { report($"MITAAI_FAILURE_CLEANUP_FAILED step={name} error={error}"); }
        }
    }
}
