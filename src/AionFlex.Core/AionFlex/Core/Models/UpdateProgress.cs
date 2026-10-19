namespace AionFlex.Core.Models;

public record UpdateProgress(string Status, int PercentComplete, bool IsIndeterminate = false);
