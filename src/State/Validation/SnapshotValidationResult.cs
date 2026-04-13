namespace TakuAgentMod.State.Validation;

internal sealed record SnapshotValidationResult(
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}
