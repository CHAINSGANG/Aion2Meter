using System.Windows.Media;
using System.Collections.Generic;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class PlayerRowViewModel : BindableBase
{
    private int _rank;
    private string _nickname = "";
    private string _className = "";
    private string? _iconPath;
    private string _dpsText = "0";
    private string _damageText = "0";
    private string _shareText = "0%";
    private double _barPercent;
    private Brush _accentBrush = Brushes.MediumPurple;
    private double _dps;
    private long _damage;
    private double _share;
    private double _critRate;
    private int _hits;
    private double _mitigationRate;
    private string _mitigationText = "Mit 0%";
    private bool _showMitigation = true;
    private bool _isLocalPlayer;
    private string _rateLabel = "DPS/С";
    private string _amountLabel = "УРОН";
    private string _emptySkillsText = "Навыки появятся после первого нанесённого урона.";
    private IReadOnlyList<MeterSkillSnapshot> _skills = [];

    public int PlayerId { get; init; }
    public int Rank { get => _rank; set => SetProperty(ref _rank, value); }
    public string Nickname { get => _nickname; set => SetProperty(ref _nickname, value); }
    public string ClassName { get => _className; set => SetProperty(ref _className, value); }
    public string? IconPath { get => _iconPath; set => SetProperty(ref _iconPath, value); }
    public string DpsText { get => _dpsText; set => SetProperty(ref _dpsText, value); }
    public string DamageText { get => _damageText; set => SetProperty(ref _damageText, value); }
    public string ShareText { get => _shareText; set => SetProperty(ref _shareText, value); }
    public double BarPercent { get => _barPercent; set => SetProperty(ref _barPercent, value); }
    public Brush AccentBrush { get => _accentBrush; set => SetProperty(ref _accentBrush, value); }
    public double Dps { get => _dps; set => SetProperty(ref _dps, value); }
    public long Damage { get => _damage; set => SetProperty(ref _damage, value); }
    public double Share { get => _share; set => SetProperty(ref _share, value); }
    public double CritRate { get => _critRate; set => SetProperty(ref _critRate, value); }
    public int Hits { get => _hits; set => SetProperty(ref _hits, value); }
    public double MitigationRate { get => _mitigationRate; set => SetProperty(ref _mitigationRate, value); }
    public string MitigationText { get => _mitigationText; set => SetProperty(ref _mitigationText, value); }
    public bool ShowMitigation { get => _showMitigation; set => SetProperty(ref _showMitigation, value); }
    public bool IsLocalPlayer { get => _isLocalPlayer; set => SetProperty(ref _isLocalPlayer, value); }
    public string RateLabel { get => _rateLabel; set => SetProperty(ref _rateLabel, value); }
    public string AmountLabel { get => _amountLabel; set => SetProperty(ref _amountLabel, value); }
    public string EmptySkillsText { get => _emptySkillsText; set => SetProperty(ref _emptySkillsText, value); }
    public IReadOnlyList<MeterSkillSnapshot> Skills { get => _skills; set => SetProperty(ref _skills, value); }
}
