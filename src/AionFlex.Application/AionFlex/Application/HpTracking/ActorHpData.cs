namespace AionFlex.Application.HpTracking;

public sealed class ActorHpData
{
    public int ActorId { get; init; }

    public uint FirstHp { get; set; }

    public uint LastHp { get; set; }

    public uint MaxHp { get; set; }

    public uint ServerMaxHp { get; set; }

    public long FirstHpTimestamp { get; set; }

    public long LastHpTimestamp { get; set; }

    public int UpdateCount { get; set; }

    public bool ReachedZeroHp { get; set; }

    public long ZeroHpTimestamp { get; set; }

    public uint MinAliveHp { get; set; }

    public long MinAliveHpTimestamp { get; set; }

    public long AttemptStartMs { get; set; }

    public uint? LastWireHp { get; set; }

    public int LastSpawnNpcId { get; set; }
}
