using System;

namespace AionFlex.Infrastructure.PacketCapture;

internal readonly record struct TcpPayloadSequenceDecision(
    bool Drop,
    int SkipBytes,
    int GapBytes,
    uint NextExpected);

internal static class TcpPayloadSequencePolicy
{
    public static TcpPayloadSequenceDecision Evaluate(uint? expected, uint sequence, int payloadLength)
    {
        if (payloadLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(payloadLength));

        uint end = unchecked(sequence + (uint)payloadLength);
        if (!expected.HasValue)
            return new TcpPayloadSequenceDecision(false, 0, 0, end);

        // TCP sequence arithmetic is modulo 2^32. Real packet distances stay
        // well below half the sequence space, so a signed delta handles wrap.
        int delta = unchecked((int)(sequence - expected.Value));
        if (delta >= 0)
            return new TcpPayloadSequenceDecision(false, 0, delta, end);

        long overlap = -(long)delta;
        return overlap >= payloadLength
            ? new TcpPayloadSequenceDecision(true, 0, 0, expected.Value)
            : new TcpPayloadSequenceDecision(false, (int)overlap, 0, end);
    }
}
