using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2Meter.Services;
using AionFlex.Core.Combat;
using AionFlex.Core.Models;
using AionFlex.Core.Overlay;

namespace Aion2Meter.ViewModels;

public sealed class MeterViewModel : BindableBase, IDisposable
{
    private const int MaximumVisiblePartyRows = 10;
    private static readonly CultureInfo UiCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Brush ReadyBrush = MakeBrush("#22D3EE");
    private static readonly Brush CombatBrush = MakeBrush("#34D399");
    private static readonly Brush DemoBrush = MakeBrush("#FBBF24");
    private static readonly Brush ErrorBrush = MakeBrush("#FB7185");

    private readonly DispatcherTimer _timer;
    private readonly SkillIconService _skillIconService = new();
    private MeterRuntime? _runtime;
    private MeterSnapshot? _latestSnapshot;
    private MeterEventScheduleSnapshot? _eventScheduleSnapshot;
    private EnrageTimerView _latestEnrageTimer = EnrageTimerView.Hidden;
    private DateTime _nextEventRefreshUtc = DateTime.MinValue;
    private DateTime _demoStarted = DateTime.UtcNow;
    private bool _initialized;
    private bool _isDemo;
    private bool _isPaused;
    private bool _bossOnly = true;
    private bool _showParty;
    private MeterDisplayMode _displayMode = MeterDisplayMode.Damage;
    private bool _needsRelog = true;
    private string _statusText = "ЗАПУСК";
    private string _statusTooltip = "Инициализация";
    private Brush _statusBrush = ReadyBrush;
    private string _targetName = "Ожидание данных";
    private TargetFrameKind _targetFrame;
    private double _targetHpPercent;
    private string _targetHpText = "HP";
    private string _battleTime = "00:00";
    private string _totalDpsText = "0";
    private string _totalDamageText = "0 урона";
    private string _captureText = "Подготовка захвата";
    private string _petStatusText = "Откройте коллекцию питомцев в игре — список появится автоматически";
    private string _partyTitle = "Комната группы";
    private string _partyCapacityText = "0/0";
    private string _partyStatusText = "Откройте подбор или комнату подземелья — состав появится автоматически";
    private string _eventRegionText = "Регион не определён";
    private string _eventClockText = "Часы сервера ожидаются";
    private string _eventScheduleStatus = "Войдите на персонажа для синхронизации календаря";
    private bool _hasPlayers;
    private bool _hasPets;
    private bool _hasParty;
    private bool _hasEventTimers;
    private bool _hasTargetDebuffs;
    private bool _showTargetDebuffs = true;
    private bool _isInCombat;
    private bool _enrageVisible;
    private string _enrageText = string.Empty;
    private Brush _enrageBackground = MakeGradient("#CC3A4256", 0.16, 0.34);
    private Brush _enrageBorder = MakeBrush("#00FFFFFF");
    private bool _eventNotificationVisible;
    private string _eventNotificationText = string.Empty;
    private Brush _eventNotificationBackground = MakeGradient("#CC3A4256", 0.16, 0.34);
    private Brush _eventNotificationBorder = MakeBrush("#00FFFFFF");
    private Brush _targetBarBrush = MakeGradient("#F0314C", 0.12, 0.30);

    public MeterViewModel()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += (_, _) => Refresh();

        ResetCommand = new RelayCommand(Reset);
        TogglePauseCommand = new RelayCommand(TogglePause);
        ToggleModeCommand = new RelayCommand(ToggleMode);
        ToggleParticipantsCommand = new RelayCommand(ToggleParticipants);
        ToggleDebuffsCommand = new RelayCommand(ToggleDebuffs);
        ShowSelfCommand = new RelayCommand(ShowSelf);
        ShowPartyCommand = new RelayCommand(ShowParty);
        ShowDamageCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.Damage));
        ShowHealingCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.Healing));
        ShowDamageTakenCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.DamageTaken));
    }

    public ObservableCollection<PlayerRowViewModel> Players { get; } = new();
    public ObservableCollection<MeterPetSnapshot> Pets { get; } = new();
    public ObservableCollection<PartyMemberRowViewModel> PartyMembers { get; } = new();
    public ObservableCollection<EventTimerRowViewModel> EventTimers { get; } = new();
    public ObservableCollection<DebuffTimerViewModel> TargetDebuffs { get; } = new();

    public MeterNavigationPosition? GetNavigationPosition() => _runtime?.GetNavigationPosition();

    public ICommand ResetCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand ToggleParticipantsCommand { get; }
    public ICommand ToggleDebuffsCommand { get; }
    public ICommand ShowSelfCommand { get; }
    public ICommand ShowPartyCommand { get; }
    public ICommand ShowDamageCommand { get; }
    public ICommand ShowHealingCommand { get; }
    public ICommand ShowDamageTakenCommand { get; }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string StatusTooltip { get => _statusTooltip; private set => SetProperty(ref _statusTooltip, value); }
    public Brush StatusBrush { get => _statusBrush; private set => SetProperty(ref _statusBrush, value); }
    public string TargetName { get => _targetName; private set => SetProperty(ref _targetName, value); }
    public TargetFrameKind TargetFrame
    {
        get => _targetFrame;
        private set
        {
            if (SetProperty(ref _targetFrame, value))
            {
                OnPropertyChanged(nameof(TargetUsesNormalFrame));
                OnPropertyChanged(nameof(TargetUsesEliteFrame));
                OnPropertyChanged(nameof(TargetUsesHeroFrame));
                OnPropertyChanged(nameof(TargetUsesLegendFrame));
            }
        }
    }
    public bool TargetUsesNormalFrame => TargetFrame == TargetFrameKind.Normal;
    public bool TargetUsesEliteFrame => TargetFrame == TargetFrameKind.Elite;
    public bool TargetUsesHeroFrame => TargetFrame == TargetFrameKind.Hero;
    public bool TargetUsesLegendFrame => TargetFrame == TargetFrameKind.Legend;
    public double TargetHpPercent { get => _targetHpPercent; private set => SetProperty(ref _targetHpPercent, value); }
    public string TargetHpText { get => _targetHpText; private set => SetProperty(ref _targetHpText, value); }
    public string BattleTime { get => _battleTime; private set => SetProperty(ref _battleTime, value); }
    public string TotalDpsText { get => _totalDpsText; private set => SetProperty(ref _totalDpsText, value); }
    public string TotalDamageText { get => _totalDamageText; private set => SetProperty(ref _totalDamageText, value); }
    public string CompactTotalText => $"{CompactDamageText} ({TotalDpsText})";
    public string CompactDamageText => TotalDamageText
        .Replace(" урона", string.Empty, StringComparison.Ordinal)
        .Replace(" лечения", string.Empty, StringComparison.Ordinal);
    public string CompactDpsText => $"{TotalDpsText}/с";
    public string CaptureText { get => _captureText; private set => SetProperty(ref _captureText, value); }
    public string PetStatusText { get => _petStatusText; private set => SetProperty(ref _petStatusText, value); }
    public string PartyTitle { get => _partyTitle; private set => SetProperty(ref _partyTitle, value); }
    public string PartyCapacityText { get => _partyCapacityText; private set => SetProperty(ref _partyCapacityText, value); }
    public string PartyStatusText { get => _partyStatusText; private set => SetProperty(ref _partyStatusText, value); }
    public string EventRegionText { get => _eventRegionText; private set => SetProperty(ref _eventRegionText, value); }
    public string EventClockText { get => _eventClockText; private set => SetProperty(ref _eventClockText, value); }
    public string EventScheduleStatus { get => _eventScheduleStatus; private set => SetProperty(ref _eventScheduleStatus, value); }
    public bool HasPlayers { get => _hasPlayers; private set => SetProperty(ref _hasPlayers, value); }
    public bool HasPets { get => _hasPets; private set => SetProperty(ref _hasPets, value); }
    public bool HasParty { get => _hasParty; private set => SetProperty(ref _hasParty, value); }
    public bool HasEventTimers { get => _hasEventTimers; private set => SetProperty(ref _hasEventTimers, value); }
    public bool HasTargetDebuffs { get => _hasTargetDebuffs; private set => SetProperty(ref _hasTargetDebuffs, value); }
    public bool ShowTargetDebuffs
    {
        get => _showTargetDebuffs;
        private set
        {
            if (SetProperty(ref _showTargetDebuffs, value))
            {
                OnPropertyChanged(nameof(DebuffToggleTooltip));
            }
        }
    }
    public bool IsInCombat { get => _isInCombat; private set => SetProperty(ref _isInCombat, value); }
    public bool EnrageVisible
    {
        get => _enrageVisible;
        private set
        {
            if (SetProperty(ref _enrageVisible, value))
            {
                OnPropertyChanged(nameof(HasNotifications));
            }
        }
    }
    public string EnrageText { get => _enrageText; private set => SetProperty(ref _enrageText, value); }
    public Brush EnrageBackground { get => _enrageBackground; private set => SetProperty(ref _enrageBackground, value); }
    public Brush EnrageBorder { get => _enrageBorder; private set => SetProperty(ref _enrageBorder, value); }
    public bool EventNotificationVisible
    {
        get => _eventNotificationVisible;
        private set
        {
            if (SetProperty(ref _eventNotificationVisible, value))
            {
                OnPropertyChanged(nameof(HasNotifications));
            }
        }
    }
    public string EventNotificationText { get => _eventNotificationText; private set => SetProperty(ref _eventNotificationText, value); }
    public Brush EventNotificationBackground { get => _eventNotificationBackground; private set => SetProperty(ref _eventNotificationBackground, value); }
    public Brush EventNotificationBorder { get => _eventNotificationBorder; private set => SetProperty(ref _eventNotificationBorder, value); }
    public bool HasNotifications => EnrageVisible || EventNotificationVisible;
    public Brush TargetBarBrush { get => _targetBarBrush; private set => SetProperty(ref _targetBarBrush, value); }
    public bool IsDemo { get => _isDemo; private set => SetProperty(ref _isDemo, value); }
    public bool NeedsRelog
    {
        get => _needsRelog;
        private set
        {
            if (SetProperty(ref _needsRelog, value))
            {
                OnPropertyChanged(nameof(EmptyStateText));
            }
        }
    }

    public bool IsSelfView => !ShowPartyMembers;
    public bool IsPartyView => ShowPartyMembers;
    public bool IsDamageMode => DisplayMode == MeterDisplayMode.Damage;
    public bool IsHealingMode => DisplayMode == MeterDisplayMode.Healing;
    public bool IsDamageTakenMode => DisplayMode == MeterDisplayMode.DamageTaken;
    public bool CanFilterParticipants => DisplayMode != MeterDisplayMode.DamageTaken;
    public string DisplayModeText => DisplayMode switch
    {
        MeterDisplayMode.Healing => "ЛЕЧЕНИЕ",
        MeterDisplayMode.DamageTaken => "ПОЛУЧЕННЫЙ УРОН",
        _ => "УРОН"
    };
    public string TargetSectionLabel => DisplayMode == MeterDisplayMode.Damage ? "ТЕКУЩАЯ ЦЕЛЬ" : "РЕЖИМ";
    public string ParticipantsHeader => DisplayMode == MeterDisplayMode.DamageTaken
        ? "ИСТОЧНИК"
        : ShowPartyMembers ? "ГРУППА" : "ПЕРСОНАЖ";
    public string SummaryDpsLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => ShowPartyMembers ? "HPS ГРУППЫ" : "МОЙ HPS",
        MeterDisplayMode.DamageTaken => "ВХОДЯЩИЙ /С",
        _ => ShowPartyMembers ? "DPS ГРУППЫ" : "МОЙ DPS"
    };
    public string SummaryDamageLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => ShowPartyMembers ? "Лечение группы" : "Моё лечение",
        MeterDisplayMode.DamageTaken => "Полученный урон",
        _ => ShowPartyMembers ? "Урон группы" : "Мой урон"
    };
    public string TotalAmountLabel => DisplayMode == MeterDisplayMode.Healing ? "ОБЩЕЕ ЛЕЧЕНИЕ" : "ОБЩИЙ УРОН";
    public string PerSecondColumnLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => "HPS/С",
        MeterDisplayMode.DamageTaken => "УРОН/С",
        _ => "DPS/С"
    };
    public string AmountColumnLabel => DisplayMode == MeterDisplayMode.Healing ? "ЛЕЧЕНИЕ" : "УРОН";
    public string EmptyStateText => NeedsRelog
        ? "ПЕРЕЗАЙДИТЕ НА ПЕРСОНАЖА\nМЕТР УЖЕ ГОТОВ К ЗАХВАТУ"
        : DisplayMode switch
        {
            MeterDisplayMode.Healing => "НЕТ ДАННЫХ ЛЕЧЕНИЯ",
            MeterDisplayMode.DamageTaken => "НЕТ ПОЛУЧЕННОГО УРОНА",
            _ => "ОЖИДАНИЕ БОЯ"
        };

    public MeterDisplayMode DisplayMode
    {
        get => _displayMode;
        private set
        {
            if (!SetProperty(ref _displayMode, value))
            {
                return;
            }

            if (value == MeterDisplayMode.DamageTaken)
            {
                ShowPartyMembers = true;
            }

            OnPropertyChanged(nameof(IsDamageMode));
            OnPropertyChanged(nameof(IsHealingMode));
            OnPropertyChanged(nameof(IsDamageTakenMode));
            OnPropertyChanged(nameof(CanFilterParticipants));
            OnPropertyChanged(nameof(DisplayModeText));
            OnPropertyChanged(nameof(TargetSectionLabel));
            NotifyMetricLabelsChanged();
            OnPropertyChanged(nameof(EmptyStateText));
        }
    }

    public bool ShowPartyMembers
    {
        get => _showParty;
        private set
        {
            if (SetProperty(ref _showParty, value))
            {
                OnPropertyChanged(nameof(IsSelfView));
                OnPropertyChanged(nameof(IsPartyView));
                OnPropertyChanged(nameof(ParticipantsModeTooltip));
                NotifyMetricLabelsChanged();
                RefreshSummary();
                RefreshRows();
            }
        }
    }

    private void NotifyMetricLabelsChanged()
    {
        OnPropertyChanged(nameof(ParticipantsHeader));
        OnPropertyChanged(nameof(SummaryDpsLabel));
        OnPropertyChanged(nameof(SummaryDamageLabel));
        OnPropertyChanged(nameof(TotalAmountLabel));
        OnPropertyChanged(nameof(PerSecondColumnLabel));
        OnPropertyChanged(nameof(AmountColumnLabel));
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseText));
                OnPropertyChanged(nameof(PauseGlyph));
            }
        }
    }

    public bool BossOnly
    {
        get => _bossOnly;
        private set
        {
            if (SetProperty(ref _bossOnly, value))
            {
                OnPropertyChanged(nameof(ModeText));
                OnPropertyChanged(nameof(ModeShortText));
                OnPropertyChanged(nameof(ModeTooltip));
            }
        }
    }

    public string ModeText => BossOnly ? "БОСС" : "ВСЕ ЦЕЛИ";
    public string ModeShortText => BossOnly ? "БОСС" : "ВСЕ";
    public string ModeTooltip => BossOnly ? "Только боссы" : "Все цели";
    public string ParticipantsModeTooltip => ShowPartyMembers
        ? "Сейчас: вся группа · нажмите, чтобы оставить только себя"
        : "Сейчас: только я · нажмите, чтобы показать всю группу";
    public string DebuffToggleTooltip => ShowTargetDebuffs
        ? "Дебафы активного персонажа: показываются · нажмите, чтобы скрыть"
        : "Дебафы активного персонажа: скрыты · нажмите, чтобы показать";
    public string PauseText => IsPaused ? "ПРОДОЛЖИТЬ" : "ПАУЗА";
    public string PauseGlyph => IsPaused ? "▶" : "Ⅱ";

    public void RestorePreferences(
        bool bossOnly,
        bool showPartyMembers,
        bool showTargetDebuffs,
        MeterDisplayMode displayMode)
    {
        BossOnly = bossOnly;
        DisplayMode = displayMode;
        ShowPartyMembers = displayMode == MeterDisplayMode.DamageTaken || showPartyMembers;
        ShowTargetDebuffs = showTargetDebuffs;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        bool forceDemo = Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(arg, "--demo", StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(Path.Combine(AppContext.BaseDirectory, "demo.flag"));

        if (forceDemo)
        {
            EnableDemo("Запущено с параметром --demo");
            _timer.Start();
            Refresh();
            return;
        }

        StatusText = "ПОДКЛЮЧЕНИЕ";
        StatusTooltip = "Загрузка справочников и запуск Npcap";
        try
        {
            _runtime = await MeterRuntime.StartAsync();
            _runtime.SetBossOnlyMode(BossOnly);
            StatusText = "ГОТОВ";
            StatusBrush = ReadyBrush;
            StatusTooltip = "Захват сетевых пакетов активен";
            CaptureText = "Npcap · ожидание боя";
        }
        catch (Exception exception)
        {
            EnableDemo(exception.Message);
        }

        _timer.Start();
        Refresh();
    }

    private void EnableDemo(string reason)
    {
        IsDemo = true;
        _demoStarted = DateTime.UtcNow - TimeSpan.FromSeconds(86);
        StatusText = "ДЕМО";
        StatusBrush = DemoBrush;
        StatusTooltip = reason;
        CaptureText = "Демонстрационные данные";
    }

    private void Refresh()
    {
        if (IsPaused)
        {
            return;
        }

        try
        {
            _latestSnapshot = IsDemo ? BuildDemoSnapshot() : _runtime?.GetSnapshot(DisplayMode);
            if (_latestSnapshot is null)
            {
                return;
            }

            ApplySnapshot(_latestSnapshot);
            RefreshFeatureData();
        }
        catch (Exception exception)
        {
            StatusText = "ОШИБКА";
            StatusBrush = ErrorBrush;
            StatusTooltip = exception.Message;
        }
    }

    private void ApplySnapshot(MeterSnapshot snapshot)
    {
        IsInCombat = snapshot.IsInCombat;
        NeedsRelog = !IsDemo &&
            (!snapshot.LocalPlayerId.HasValue || string.IsNullOrWhiteSpace(snapshot.LocalPlayerName));
        TargetName = string.IsNullOrWhiteSpace(snapshot.TargetName)
            ? (BossOnly ? "Цель не определена" : "Все цели")
            : snapshot.TargetName;
        TargetFrame = snapshot.TargetFrame;
        if (DisplayMode == MeterDisplayMode.Damage)
        {
            TargetHpPercent = snapshot.TargetHpPercent ?? 0;
            TargetHpText = snapshot.TargetHpPercent.HasValue
                ? $"HP {snapshot.TargetHpPercent.Value:0.#}%"
                : "HP";
            TargetBarBrush = snapshot.TargetFrame switch
            {
                TargetFrameKind.Legend => MakeGradient("#C71932", 0.20, 0.38),
                TargetFrameKind.Hero => MakeGradient("#D94A2B", 0.18, 0.34),
                TargetFrameKind.Elite => MakeGradient("#E26335", 0.15, 0.32),
                _ => MakeGradient("#F0314C", 0.12, 0.30)
            };
        }
        else
        {
            TargetHpPercent = snapshot.Players.Count > 0 ? 100 : 0;
            TargetHpText = DisplayMode == MeterDisplayMode.Healing
                ? $"HPS {FormatCompact(snapshot.TotalDps)}"
                : $"ВХОДЯЩИЙ {FormatCompact(snapshot.TotalDps)}/с";
            TargetBarBrush = DisplayMode == MeterDisplayMode.Healing
                ? MakeGradient("#55B889", 0.12, 0.30)
                : MakeGradient("#D28B5E", 0.12, 0.30);
        }
        BattleTime = FormatDuration(snapshot.BattleTimeMs);
        _latestEnrageTimer = snapshot.EnrageTimer;
        RefreshSummary();

        if (!IsDemo)
        {
            if (NeedsRelog)
            {
                StatusText = "НУЖЕН РЕЛОГ";
                StatusBrush = DemoBrush;
                StatusTooltip = "Выйдите к выбору персонажа и войдите снова — метр должен увидеть пакет входа";
                CaptureText = $"Npcap · {snapshot.ReceivedPackets:N0} пакетов · перезайдите на персонажа";
            }
            else
            {
                StatusText = snapshot.IsInCombat ? "В БОЮ" : "ГОТОВ";
                StatusBrush = snapshot.IsInCombat ? CombatBrush : ReadyBrush;
                StatusTooltip = $"Персонаж: {snapshot.LocalPlayerName}";
                CaptureText = $"{snapshot.LocalPlayerName} · Npcap {snapshot.ReceivedPackets:N0} пакетов · {snapshot.DroppedPackets:N0} потерь";
            }
        }

        RefreshRows();
    }

    private void RefreshFeatureData()
    {
        IReadOnlyList<MeterDebuffSnapshot> debuffs = DisplayMode != MeterDisplayMode.Damage
            ? []
            : IsDemo
                ? BuildDemoDebuffs()
                : _runtime?.GetActiveTargetDebuffs() ?? [];
        SyncDebuffs(debuffs);

        IReadOnlyList<MeterPetSnapshot> pets = IsDemo
            ?
            [
                BuildDemoPet(1001, "Black Dragon", "Особый", "Special", "BlackDragon_01", 3, 3, "Особый эффект"),
                BuildDemoPet(1004, "Fossa", "Фера", "Feral", "Fossa_01", 3, 3, "Точность +2 · Ловкость +1 · Скорость передвижения +3"),
                BuildDemoPet(1003, "Forest Specter", "Натура", "Nature", "ForestWisp_01", 2, 3, "Сопротивление криту +1 · Макс. энергии +600")
            ]
            : _runtime?.GetPets() ?? [];
        SyncCollection(Pets, pets);
        HasPets = Pets.Count > 0;
        PetStatusText = HasPets
            ? $"Найдено питомцев: {Pets.Count}"
            : "Откройте коллекцию питомцев в игре — список появится автоматически";

        PartyRoomRoster? roster = IsDemo ? BuildDemoPartyRoster() : _runtime?.GetPartyRoster();
        IReadOnlyList<PartyMemberRowViewModel> members = roster?.Members
            .OrderBy(member => member.Slot)
            .Select(member => new PartyMemberRowViewModel(
                member.Slot,
                member.Name,
                ClassLabel(member.JobClass),
                member.Level,
                member.GearScore,
                member.CombatPower,
                roster.IsLeader(member) ? "◆" : string.Empty,
                member.IsInInstance ? "В подземелье" : "Вне подземелья"))
            .ToArray() ?? [];
        SyncCollection(PartyMembers, members);
        HasParty = PartyMembers.Count > 0;
        PartyTitle = string.IsNullOrWhiteSpace(roster?.Title) ? "Комната группы" : roster.Title;
        PartyCapacityText = roster is null ? "0/0" : $"{PartyMembers.Count}/{roster.MaxMembers}";
        PartyStatusText = HasParty
            ? $"Состав комнаты обновлён · {PartyCapacityText}"
            : "Откройте подбор или комнату подземелья — состав появится автоматически";

        if (DateTime.UtcNow >= _nextEventRefreshUtc)
        {
            _nextEventRefreshUtc = DateTime.UtcNow.AddMilliseconds(250);
            _eventScheduleSnapshot = IsDemo
                ? BuildDemoEventSchedule()
                : _runtime?.GetEventSchedule();
            if (_eventScheduleSnapshot is not null)
            {
                SyncEventSchedule(_eventScheduleSnapshot);
            }
        }

        ApplyNotification();
    }

    private static MeterPetSnapshot BuildDemoPet(
        int id,
        string name,
        string family,
        string familyCode,
        string model,
        int level,
        int maxLevel,
        string bonus)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "textures",
            "pets",
            $"UT_Vehicle_Portrait_{model}.png");
        return new MeterPetSnapshot(id, name, family, familyCode, level, maxLevel, bonus, File.Exists(path) ? path : null);
    }

    private void SyncDebuffs(IReadOnlyList<MeterDebuffSnapshot> snapshots)
    {
        Dictionary<int, DebuffTimerViewModel> existing = TargetDebuffs.ToDictionary(item => item.SkillCode);
        for (int index = 0; index < snapshots.Count; index++)
        {
            MeterDebuffSnapshot snapshot = snapshots[index];
            if (!existing.TryGetValue(snapshot.SkillCode, out DebuffTimerViewModel? row))
            {
                row = new DebuffTimerViewModel { SkillCode = snapshot.SkillCode };
                TargetDebuffs.Insert(Math.Min(index, TargetDebuffs.Count), row);
            }

            int oldIndex = TargetDebuffs.IndexOf(row);
            if (oldIndex != index)
            {
                TargetDebuffs.Move(oldIndex, index);
            }
            row.Update(snapshot, _skillIconService.GetIconPath(snapshot.SkillCode));
        }

        HashSet<int> activeSkills = snapshots.Select(snapshot => snapshot.SkillCode).ToHashSet();
        for (int index = TargetDebuffs.Count - 1; index >= 0; index--)
        {
            if (!activeSkills.Contains(TargetDebuffs[index].SkillCode))
            {
                TargetDebuffs.RemoveAt(index);
            }
        }

        HasTargetDebuffs = TargetDebuffs.Count > 0;
    }

    private IReadOnlyList<MeterDebuffSnapshot> BuildDemoDebuffs()
    {
        long cycle = (long)(DateTime.UtcNow - _demoStarted).TotalMilliseconds;
        return
        [
            new(11100000, "Ослабление защиты", 18_000 - cycle % 18_000, 18_000, 1),
            new(11200000, "Кровотечение", 10_000 - cycle % 10_000, 10_000, 2)
        ];
    }

    private void SyncEventSchedule(MeterEventScheduleSnapshot snapshot)
    {
        EventRegionText = snapshot.RegionText;
        EventClockText = snapshot.ClockText;
        EventScheduleStatus = snapshot.StatusText;

        Dictionary<string, EventTimerRowViewModel> existing = EventTimers.ToDictionary(row => row.Key);
        for (int index = 0; index < snapshot.Timers.Count; index++)
        {
            MeterEventTimerSnapshot timer = snapshot.Timers[index];
            if (!existing.TryGetValue(timer.Key, out EventTimerRowViewModel? row))
            {
                row = new EventTimerRowViewModel { Key = timer.Key };
                EventTimers.Insert(Math.Min(index, EventTimers.Count), row);
            }

            int oldIndex = EventTimers.IndexOf(row);
            if (oldIndex != index)
            {
                EventTimers.Move(oldIndex, index);
            }
            row.Update(timer);
        }

        HashSet<string> activeKeys = snapshot.Timers.Select(timer => timer.Key).ToHashSet(StringComparer.Ordinal);
        for (int index = EventTimers.Count - 1; index >= 0; index--)
        {
            if (!activeKeys.Contains(EventTimers[index].Key))
            {
                EventTimers.RemoveAt(index);
            }
        }
        HasEventTimers = EventTimers.Count > 0;
    }

    private MeterEventScheduleSnapshot BuildDemoEventSchedule()
    {
        DateTime now = DateTime.Now;
        DateTime riftAt = _demoStarted.ToLocalTime().AddMinutes(5);
        TimeSpan riftUntil = riftAt - now;
        IReadOnlyList<MeterEventTimerSnapshot> timers =
        [
            new("demo-rift", "РИФТ", "Разлом", "Пространственный разлом", riftAt, riftUntil, false, "#FF6196FF"),
            new("demo-shugo", "ФЕСТИВАЛЬ", "Фестиваль шуго", "Открыта регистрация на событие", now.AddMinutes(18), TimeSpan.FromMinutes(18), false, "#FF4EDACB"),
            new("demo-invasion", "ВТОРЖЕНИЕ", "Пространственное вторжение", "Глобальное событие", now.AddMinutes(48), TimeSpan.FromMinutes(48), false, "#FFFFA04D")
        ];
        MeterEventAlertSnapshot? alert = riftUntil <= TimeSpan.FromMinutes(10)
            ? new MeterEventAlertSnapshot("Разлом через", EventScheduleService.Countdown(riftUntil), "Пространственный разлом", "#E63D68C9", "#FF6F9EFF")
            : null;
        return new MeterEventScheduleSnapshot(
            true,
            "Global · Europe",
            "Europe/Berlin · игровое время",
            $"Ближайшее: Разлом · {EventScheduleService.Countdown(riftUntil)}",
            timers,
            alert);
    }

    private static void SyncCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        if (target.Count == source.Count && target.SequenceEqual(source))
        {
            return;
        }

        target.Clear();
        foreach (T item in source)
        {
            target.Add(item);
        }
    }

    private static PartyRoomRoster BuildDemoPartyRoster() => new(
        1,
        "Экспедиция · Эфирный Страж",
        6,
        101,
        1001,
        1,
        [
            new PartyRoomMember(1, 1001, 1, "Veloria", 0, JobClass.Assassin, 55, 1840, 21750, 1, 2),
            new PartyRoomMember(2, 1002, 1, "Asterion", 0, JobClass.Sorcerer, 55, 1790, 20980, 1, 2),
            new PartyRoomMember(3, 1003, 1, "Nephira", 0, JobClass.Ranger, 55, 1765, 20540, 1, 2),
            new PartyRoomMember(4, 1004, 1, "Kael", 0, JobClass.Gladiator, 55, 1885, 22410, 1, 2),
            new PartyRoomMember(5, 1005, 1, "Mireya", 0, JobClass.Chanter, 55, 1710, 19820, 1, 2),
            new PartyRoomMember(6, 1006, 1, "Solenne", 0, JobClass.Cleric, 55, 1815, 21360, 1, 2)
        ]);

    private void RefreshRows()
    {
        if (_latestSnapshot is null)
        {
            return;
        }

        List<MeterPlayerSnapshot> allPlayers = _latestSnapshot.Players
            .OrderByDescending(player => player.Dps)
            .ThenBy(player => player.Nickname, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        List<(MeterPlayerSnapshot Player, int Rank)> ordered;
        if (ShowPartyMembers || DisplayMode == MeterDisplayMode.DamageTaken)
        {
            if (DisplayMode != MeterDisplayMode.DamageTaken && LocalPlayerRowPolicy.NeedsPlaceholder(
                    allPlayers.Select(player => (player.PlayerId, (string?)player.Nickname)),
                    _latestSnapshot.LocalPlayerId,
                    _latestSnapshot.LocalPlayerName))
            {
                allPlayers.Add(CreateLocalPlaceholder(_latestSnapshot));
            }

            int localIndex = DisplayMode == MeterDisplayMode.DamageTaken
                ? -1
                : allPlayers.FindIndex(player => LocalPlayerRowPolicy.IsLocalPlayer(
                    player.PlayerId,
                    player.Nickname,
                    _latestSnapshot.LocalPlayerId,
                    _latestSnapshot.LocalPlayerName));
            ordered = allPlayers
                .Take(MaximumVisiblePartyRows)
                .Select((player, index) => (player, index + 1))
                .ToList();
            if (localIndex >= MaximumVisiblePartyRows)
            {
                ordered = allPlayers
                    .Take(MaximumVisiblePartyRows - 1)
                    .Select((player, index) => (player, index + 1))
                    .Append((allPlayers[localIndex], localIndex + 1))
                    .ToList();
            }
        }
        else
        {
            int localIndex = allPlayers.FindIndex(player =>
                LocalPlayerRowPolicy.IsLocalPlayer(
                    player.PlayerId,
                    player.Nickname,
                    _latestSnapshot.LocalPlayerId,
                    _latestSnapshot.LocalPlayerName));
            ordered = localIndex >= 0
                ? [(allPlayers[localIndex], localIndex + 1)]
                : string.IsNullOrWhiteSpace(_latestSnapshot.LocalPlayerName)
                    ? []
                    : [(CreateLocalPlaceholder(_latestSnapshot), allPlayers.Count + 1)];
        }
        double maximum = allPlayers.Count == 0 ? 1 : Math.Max(1, allPlayers[0].Dps);
        Dictionary<int, PlayerRowViewModel> existing = Players.ToDictionary(row => row.PlayerId);

        for (int index = 0; index < ordered.Count; index++)
        {
            (MeterPlayerSnapshot player, int trueRank) = ordered[index];
            if (!existing.TryGetValue(player.PlayerId, out PlayerRowViewModel? row))
            {
                row = new PlayerRowViewModel { PlayerId = player.PlayerId };
                Players.Insert(Math.Min(index, Players.Count), row);
            }

            int oldIndex = Players.IndexOf(row);
            if (oldIndex != index)
            {
                Players.Move(oldIndex, index);
            }

            row.Rank = trueRank;
            row.Nickname = player.Nickname;
            row.ClassName = player.Job == JobClass.Unknown && DisplayMode == MeterDisplayMode.DamageTaken
                ? "Противник"
                : ClassLabel(player.Job);
            row.IconPath = ClassIcon(player.Job);
            row.AccentBrush = ClassBrush(player.Job);
            row.BarPercent = Math.Clamp(player.Dps / maximum * 100.0, 0, 100);
            row.DpsText = FormatCompact(player.Dps) + "/с";
            row.DamageText = FormatCompact(player.Damage);
            row.ShareText = player.Share.ToString("0.0", UiCulture) + "%";
            row.Dps = player.Dps;
            row.Damage = player.Damage;
            row.Share = player.Share;
            row.CritRate = player.CritRate;
            row.Hits = player.Hits;
            row.MitigationRate = player.MitigationRate;
            row.MitigationText = $"Mit {player.MitigationRate.ToString("0", UiCulture)}%";
            row.ShowMitigation = DisplayMode != MeterDisplayMode.DamageTaken;
            row.IsLocalPlayer = DisplayMode != MeterDisplayMode.DamageTaken && LocalPlayerRowPolicy.IsLocalPlayer(
                player.PlayerId,
                player.Nickname,
                _latestSnapshot.LocalPlayerId,
                _latestSnapshot.LocalPlayerName);
            row.Skills = player.Skills;
            row.RateLabel = PerSecondColumnLabel;
            row.AmountLabel = AmountColumnLabel;
            row.EmptySkillsText = DisplayMode switch
            {
                MeterDisplayMode.Healing => "Навыки появятся после первого лечения.",
                MeterDisplayMode.DamageTaken => "Атаки появятся после первого полученного урона.",
                _ => "Навыки появятся после первого нанесённого урона."
            };
        }

        HashSet<int> activeIds = ordered.Select(item => item.Player.PlayerId).ToHashSet();
        for (int index = Players.Count - 1; index >= 0; index--)
        {
            if (!activeIds.Contains(Players[index].PlayerId))
            {
                Players.RemoveAt(index);
            }
        }

        HasPlayers = Players.Count > 0;
    }

    private void RefreshSummary()
    {
        if (_latestSnapshot is null)
        {
            return;
        }

        double dps = _latestSnapshot.TotalDps;
        long damage = _latestSnapshot.TotalDamage;
        if (!ShowPartyMembers && DisplayMode != MeterDisplayMode.DamageTaken)
        {
            MeterPlayerSnapshot? localPlayer = _latestSnapshot.Players.FirstOrDefault(player =>
                LocalPlayerRowPolicy.IsLocalPlayer(
                    player.PlayerId,
                    player.Nickname,
                    _latestSnapshot.LocalPlayerId,
                    _latestSnapshot.LocalPlayerName));
            dps = localPlayer?.Dps ?? 0;
            damage = localPlayer?.Damage ?? 0;
        }

        TotalDpsText = FormatCompact(dps);
        TotalDamageText = DisplayMode == MeterDisplayMode.Healing
            ? $"{FormatCompact(damage)} лечения"
            : $"{FormatCompact(damage)} урона";
        OnPropertyChanged(nameof(CompactTotalText));
        OnPropertyChanged(nameof(CompactDamageText));
        OnPropertyChanged(nameof(CompactDpsText));
    }

    private static MeterPlayerSnapshot CreateLocalPlaceholder(MeterSnapshot snapshot) => new(
        snapshot.LocalPlayerId ?? LocalPlayerRowPolicy.PlaceholderPlayerId,
        snapshot.LocalPlayerName ?? "Персонаж",
        snapshot.LocalPlayerJob,
        0,
        0,
        0,
        0,
        0,
        0,
        []);

    private void ShowSelf()
    {
        if (CanFilterParticipants)
        {
            ShowPartyMembers = false;
        }
    }

    private void ShowParty() => ShowPartyMembers = true;

    private void ToggleParticipants()
    {
        if (CanFilterParticipants)
        {
            ShowPartyMembers = !ShowPartyMembers;
        }
    }

    private void ToggleDebuffs() => ShowTargetDebuffs = !ShowTargetDebuffs;

    private void SetDisplayMode(MeterDisplayMode mode)
    {
        if (DisplayMode == mode)
        {
            return;
        }

        DisplayMode = mode;
        Players.Clear();
        TargetDebuffs.Clear();
        HasPlayers = false;
        HasTargetDebuffs = false;
        _latestSnapshot = null;
        _latestEnrageTimer = EnrageTimerView.Hidden;
        EnrageVisible = false;
        TargetName = mode switch
        {
            MeterDisplayMode.Healing => "Лечение группы",
            MeterDisplayMode.DamageTaken => "Полученный урон",
            _ => BossOnly ? "Цель не определена" : "Все цели"
        };
        Refresh();
    }

    private void ToggleMode()
    {
        BossOnly = !BossOnly;
        _runtime?.SetBossOnlyMode(BossOnly);
        Refresh();
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
        StatusTooltip = IsPaused ? "Обновление панели приостановлено; захват продолжается" : StatusTooltip;
        if (!IsPaused)
        {
            Refresh();
        }
    }

    private void Reset()
    {
        _runtime?.Reset();
        _demoStarted = DateTime.UtcNow;
        _latestSnapshot = null;
        _latestEnrageTimer = EnrageTimerView.Hidden;
        Players.Clear();
        HasPlayers = false;
        TargetName = DisplayMode switch
        {
            MeterDisplayMode.Healing => "Лечение группы",
            MeterDisplayMode.DamageTaken => "Полученный урон",
            _ => BossOnly ? "Цель не определена" : "Все цели"
        };
        TargetHpPercent = 0;
        TargetHpText = "HP";
        EnrageVisible = false;
        EnrageText = string.Empty;
        BattleTime = "00:00";
        TotalDpsText = "0";
        TotalDamageText = DisplayMode == MeterDisplayMode.Healing ? "0 лечения" : "0 урона";
        OnPropertyChanged(nameof(CompactTotalText));
        OnPropertyChanged(nameof(CompactDamageText));
        OnPropertyChanged(nameof(CompactDpsText));
        Refresh();
    }

    private MeterSnapshot BuildDemoSnapshot()
    {
        double seconds = Math.Max(1, (DateTime.UtcNow - _demoStarted).TotalSeconds);
        double pulse = 1 + Math.Sin(seconds / 5.0) * 0.018;
        (int Id, string Name, JobClass Job, double Rate, double CritRate, int Hits)[] source = DisplayMode switch
        {
            MeterDisplayMode.Healing =>
            [
                (101, "Veloria", JobClass.Assassin, 46_800d, 12.0d, 44),
                (105, "Mireya", JobClass.Chanter, 284_500d, 31.4d, 218),
                (106, "Solenne", JobClass.Cleric, 638_200d, 38.7d, 376)
            ],
            MeterDisplayMode.DamageTaken =>
            [
                (201, "Эфирный Страж", JobClass.Unknown, 412_600d, 18.5d, 96),
                (202, "Призванный клинок", JobClass.Unknown, 188_300d, 11.2d, 142),
                (203, "Осколок бездны", JobClass.Unknown, 74_900d, 7.8d, 61)
            ],
            _ =>
            [
                (101, "Veloria", JobClass.Assassin, 1_284_600d, 41.2d, 682),
                (102, "Asterion", JobClass.Sorcerer, 1_137_900d, 38.0d, 451),
                (103, "Nephira", JobClass.Ranger, 946_300d, 34.7d, 519),
                (104, "Kael", JobClass.Gladiator, 812_700d, 31.5d, 604),
                (105, "Mireya", JobClass.Chanter, 556_200d, 27.8d, 390),
                (106, "Solenne", JobClass.Cleric, 318_400d, 22.1d, 248)
            ]
        };
        double totalDps = source.Sum(item => item.Item4 * pulse);
        long totalDamage = (long)(totalDps * seconds);
        IReadOnlyList<MeterPlayerSnapshot> players = source.Select(item =>
        {
            double dps = item.Item4 * pulse;
            double share = dps / totalDps * 100.0;
            IReadOnlyList<MeterSkillSnapshot> skills =
            [
                new(10101, "Теневой удар", dps * 0.42, (long)(dps * seconds * 0.42), 42, 38, 55.3, 64.0, 8.0, 12.0, dps * seconds * 0.42 / 38, (long)(dps * 2.8)),
                new(10102, "Клеймо убийцы", dps * 0.34, (long)(dps * seconds * 0.34), 34, 27, 44.4, 51.8, 0, 7.4, dps * seconds * 0.34 / 27, (long)(dps * 2.1)),
                new(10103, "Ядовитый клинок", dps * 0.24, (long)(dps * seconds * 0.24), 24, 31, 32.3, 38.7, 0, 0, dps * seconds * 0.24 / 31, (long)(dps * 1.4))
            ];
            return new MeterPlayerSnapshot(
                item.Item1,
                item.Item2,
                item.Item3,
                dps,
                (long)(dps * seconds),
                share,
                item.Item5,
                item.Item6,
                8 + item.Item1 % 5 * 7,
                skills);
        }).ToArray();

        bool damageMode = DisplayMode == MeterDisplayMode.Damage;
        return new MeterSnapshot(
            DisplayMode switch
            {
                MeterDisplayMode.Healing => "Лечение группы",
                MeterDisplayMode.DamageTaken => "Полученный урон",
                _ => "Эфирный Страж"
            },
            damageMode ? TargetFrameKind.Legend : TargetFrameKind.Normal,
            damageMode ? 72 : null,
            damageMode ? 7_200_000 : null,
            damageMode ? 10_000_000 : null,
            (long)(seconds * 1000),
            totalDps,
            totalDamage,
            players,
            true,
            18_421 + (long)seconds * 12,
            0,
            101,
            "Veloria",
            JobClass.Assassin,
            damageMode
                ? new EnrageTimerView(true, false, "Enrage 03:14 · kill ~02:08", EnrageTimerTone.OnTrack)
                : EnrageTimerView.Hidden);
    }

    private void ApplyEnrageTimer(EnrageTimerView view)
    {
        EnrageVisible = view.Visible;
        EnrageText = view.Text
            .Replace("ENRAGED", "БОСС В ЯРОСТИ", StringComparison.Ordinal)
            .Replace("Enrage ", "Ярость через ", StringComparison.Ordinal)
            .Replace(" · kill ~", " · убийство ~", StringComparison.Ordinal)
            .Replace(" · kill ", " · убийство ", StringComparison.Ordinal);

        (EnrageBackground, EnrageBorder) = view.Tone switch
        {
            EnrageTimerTone.OnTrack => (MakeGradient("#CC2F9E6E", 0.18, 0.42), MakeBrush("#506BD4A4")),
            EnrageTimerTone.Tight => (MakeGradient("#CCD69A2D", 0.18, 0.42), MakeBrush("#70F0C36A")),
            EnrageTimerTone.TooSlow => (MakeGradient("#CCE23B4E", 0.18, 0.42), MakeBrush("#80FF7185")),
            EnrageTimerTone.Enraged => (MakeGradient("#E6B0102A", 0.18, 0.42), MakeBrush("#FFE23B4E")),
            _ => (MakeGradient("#CC3A4256", 0.16, 0.34), MakeBrush("#00FFFFFF"))
        };
    }

    private void ApplyNotification()
    {
        ApplyEnrageTimer(_latestEnrageTimer);

        MeterEventAlertSnapshot? alert = _eventScheduleSnapshot?.Alert;
        if (alert is null)
        {
            EventNotificationVisible = false;
            EventNotificationText = string.Empty;
            return;
        }

        EventNotificationVisible = true;
        EventNotificationText = $"{alert.Title}  {alert.Countdown}";
        EventNotificationBackground = MakeGradient(alert.BackgroundColor, 0.18, 0.42);
        EventNotificationBorder = MakeBrush(alert.BorderColor);
    }

    private static string FormatDuration(long milliseconds)
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatCompact(double value)
    {
        double absolute = Math.Abs(value);
        if (absolute >= 1_000_000_000)
        {
            return (value / 1_000_000_000d).ToString("0.00", UiCulture) + "b";
        }
        if (absolute >= 1_000_000)
        {
            return (value / 1_000_000d).ToString("0.00", UiCulture) + "m";
        }
        if (absolute >= 1_000)
        {
            return (value / 1_000d).ToString("0.0", UiCulture) + "k";
        }
        return value.ToString("0", UiCulture);
    }

    private static string ClassLabel(JobClass job) => job switch
    {
        JobClass.Gladiator => "Гладиатор",
        JobClass.Templar => "Страж",
        JobClass.Ranger => "Стрелок",
        JobClass.Assassin => "Убийца",
        JobClass.Sorcerer => "Волшебник",
        JobClass.Cleric => "Целитель",
        JobClass.Elementalist => "Заклинатель",
        JobClass.Chanter => "Чародей",
        JobClass.Fighter => "Кулачник",
        _ => "Неизвестно"
    };

    private static string? ClassIcon(JobClass job)
    {
        if (job == JobClass.Unknown)
        {
            return null;
        }
        return Path.Combine(AppContext.BaseDirectory, "data", "textures", "classes", job + ".png");
    }

    private static Brush ClassBrush(JobClass job) => job switch
    {
        JobClass.Gladiator => MakeGradient("#D49362"),
        JobClass.Templar => MakeGradient("#7EA6BC"),
        JobClass.Ranger => MakeGradient("#76B48E"),
        JobClass.Assassin => MakeGradient("#9788BA"),
        JobClass.Sorcerer => MakeGradient("#5AA6AF"),
        JobClass.Cleric => MakeGradient("#BEAA68"),
        JobClass.Elementalist => MakeGradient("#A0819E"),
        JobClass.Chanter => MakeGradient("#69A9A1"),
        JobClass.Fighter => MakeGradient("#B97980"),
        _ => MakeGradient("#89969C")
    };

    private static Brush MakeGradient(string color, double lighten = 0.16, double darken = 0.30)
    {
        Color source = (Color)ColorConverter.ConvertFromString(color);
        Color left = Blend(source, Colors.White, lighten);
        Color right = Blend(source, Colors.Black, darken);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(left, 0));
        brush.GradientStops.Add(new GradientStop(source, 0.58));
        brush.GradientStops.Add(new GradientStop(right, 1));
        brush.Freeze();
        return brush;
    }

    private static Color Blend(Color source, Color target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
        return Color.FromArgb(source.A, Mix(source.R, target.R), Mix(source.G, target.G), Mix(source.B, target.B));
    }

    private static Brush MakeBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    public void Dispose()
    {
        _timer.Stop();
        _runtime?.Dispose();
        _runtime = null;
    }
}
