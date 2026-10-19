using System.Collections.Generic;

namespace AionFlex.Core.Search;

public sealed record SearchEntry(string Title, string Subtitle, SearchGroup Group, string Target, IReadOnlyList<string> Keywords, bool DevOnly = false, bool NeedsPvp = false, string? Toggle = null);
