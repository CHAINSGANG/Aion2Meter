using System.Collections.Generic;

namespace AionFlex.Infrastructure.PacketParsing.Diagnostics;

public sealed record ProtocolHealthSnapshot(IReadOnlyDictionary<string, long> Opcodes, IReadOnlyDictionary<string, long> Features, IReadOnlyList<(string Watch, string Feature)> Breaks, int WindowSeconds, IReadOnlyDictionary<string, long> DecodeOk, IReadOnlyDictionary<string, long> DecodeMiss, IReadOnlyList<DecodeMissSample> DecodeMissSamples);
