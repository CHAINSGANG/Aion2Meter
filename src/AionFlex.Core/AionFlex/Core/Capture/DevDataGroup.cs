using System;
using System.Collections.Generic;

namespace AionFlex.Core.Capture;

public sealed record DevDataGroup(string Key, DevDataCategory Category, long SizeBytes, DateTime NewestWriteUtc, bool Pinned, bool InUse, IReadOnlyList<string> Files);
