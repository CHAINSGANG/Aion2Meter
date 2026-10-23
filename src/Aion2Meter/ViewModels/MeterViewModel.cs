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
    private static CultureInfo UiCulture => CultureInfo.CurrentCulture;
    private static readonly Brush ReadyBrush = MakeBrush("#22D3EE");
    private static readonly Brush CombatBrush = MakeBrush("#34D399");
    private static readonly Brush DemoBrush = MakeBrush("#FBBF24");
    private static readonly Brush ErrorBrush = MakeBrush("#FB7185");
    private static readonly Brush DamageTargetBarBrush = MakeTargetHpGradient();
    private static readonly Brush HealingTargetBarBrush = MakeGradient("#55B889", 0.12, 0.30);
    private static readonly Brush DamageTakenTargetBarBrush = MakeGradient("#D28B5E", 0.12, 0.30);
    private static readonly (Brush Background, Brush Border, Brush Foreground) NeutralEnrageBrushes =
        (MakeGradient("#66545A61", 0.08, 0.20), MakeBrush("#668E969A"), MakeBrush("#FFD8D0BD"));
    private static readonly (Brush Background, Brush Border, Brush Foreground) OnTrackEnrageBrushes =
        (MakeGradient("#66348C67", 0.08, 0.20), MakeBrush("#AA5CB986"), MakeBrush("#FFB7EDD0"));
    private static readonly (Brush Background, Brush Border, Brush Foreground) BehindEnrageBrushes =
        (MakeGradient("#668F3445", 0.08, 0.20), MakeBrush("#AAD9717D"), MakeBrush("#FFFFC0C8"));
    private static readonly IReadOnlyDictionary<JobClass, Brush> ClassBrushes = new Dictionary<JobClass, Brush>
    {
        [JobClass.Gladiator] = MakeGradient("#D49362"),
        [JobClass.Templar] = MakeGradient("#7EA6BC"),
        [JobClass.Ranger] = MakeGradient("#76B48E"),
        [JobClass.Assassin] = MakeGradient("#9788BA"),
        [JobClass.Sorcerer] = MakeGradient("#5AA6AF"),
        [JobClass.Cleric] = MakeGradient("#BEAA68"),
        [JobClass.Elementalist] = MakeGradient("#A0819E"),
        [JobClass.Chanter] = MakeGradient("#69A9A1"),
        [JobClass.Fighter] = MakeGradient("#B97980")
    };
    private static readonly Brush UnknownClassBrush = MakeGradient("#89969C");

    private readonly DispatcherTimer _timer;
    private readonly SkillIconService _skillIconService = new();
    private MeterRuntime? _runtime;
    private MeterSnapshot? _latestSnapshot;
    private MeterEventScheduleSnapshot? _eventScheduleSnapshot;
    private string _lastUiAlertTrace = string.Empty;
    private string _lastRefreshError = string.Empty;
    private EnrageTimerView _latestEnrageTimer = EnrageTimerView.Hidden;
    private DateTime _nextEventRefreshUtc = DateTime.MinValue;
    private DateTime _demoStarted = DateTime.UtcNow;
    private bool _initialized;
    private bool _disposed;
    private bool _isDemo;
    private bool _isPaused;
    private bool _bossOnly = true;
    private bool _showParty;
    private MeterDisplayMode _displayMode = MeterDisplayMode.Damage;
    private bool _localIdentityPending = true;
    private string _statusText = T("STARTING", "ЗАПУСК");
    private string _statusTooltip = T("Initializing", "Инициализация");
    private Brush _statusBrush = ReadyBrush;
    private string _targetName = T("Waiting for data", "Ожидание данных");
    private TargetFrameKind _targetFrame;
    private double _targetHpPercent;
    private string _targetHpText = "HP";
    private string _battleTime = "00:00";
    private string _totalDpsText = "0";
    private string _totalDamageText = T("0 damage", "0 урона");
    private string _captureText = T("Preparing capture", "Подготовка захвата");
    private string _petStatusText = T("Open the pet collection in game — the list will appear automatically", "Откройте коллекцию питомцев в игре — список появится автоматически");
    private string _partyTitle = T("Party room", "Комната группы");
    private string _partyCapacityText = "0/0";
    private string _partyStatusText = T("Open matchmaking or a dungeon room — the roster will appear automatically", "Откройте подбор или комнату подземелья — состав появится автоматически");
    private string _eventRegionText = T("Region not detected", "Регион не определён");
    private string _eventClockText = T("Waiting for server clock", "Часы сервера ожидаются");
    private string _eventScheduleStatus = T("Log in to a character to synchronize the calendar", "Войдите на персонажа для синхронизации календаря");
    private bool _hasPlayers;
    private bool _hasPets;
    private bool _hasParty;
    private bool _hasEventTimers;
    private bool _hasTargetDebuffs;
    private bool _showTargetDebuffs = true;
    private bool _showOtherPlayersDebuffs;
    private bool _usePartyRowStyle;
    private bool _isPvpMode;
    private bool _overlayVisible = true;
    private bool _keepNotificationsWhenHidden;
    private bool _notificationUpdateInProgress;
    private int _notificationUpdateVersion;
    private bool _isInCombat;
    private bool _enrageVisible;
    private string _enrageText = string.Empty;
    private Brush _enrageBackground = NeutralEnrageBrushes.Background;
    private Brush _enrageBorder = NeutralEnrageBrushes.Border;
    private Brush _enrageForeground = NeutralEnrageBrushes.Foreground;
    private Brush _targetBarBrush = DamageTargetBarBrush;

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
        TogglePvpCommand = new RelayCommand(TogglePvp);
        ShowSelfCommand = new RelayCommand(ShowSelf);
        ShowPartyCommand = new RelayCommand(ShowParty);
        ShowDamageCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.Damage));
        ShowHealingCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.Healing));
        ShowDamageTakenCommand = new RelayCommand(() => SetDisplayMode(MeterDisplayMode.DamageTaken));
        EventTimers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EventTimerCountText));
        EventAlerts.CollectionChanged += (_, _) =>
        {
            if (!_notificationUpdateInProgress)
                NotifyNotificationVisibilityChanged();
        };
    }

    public ObservableCollection<PlayerRowViewModel> Players { get; } = new();
    public ObservableCollection<MeterPetSnapshot> Pets { get; } = new();
    public ObservableCollection<PartyMemberRowViewModel> PartyMembers { get; } = new();
    public ObservableCollection<EventTimerRowViewModel> EventTimers { get; } = new();
    public ObservableCollection<EventAlertRowViewModel> EventAlerts { get; } = new();

    public void TraceUiAlertState(string state) => _runtime?.TraceUiAlertState(state);
    public void TraceUiError(string area, Exception exception) => _runtime?.TraceUiError(area, exception);
    public ObservableCollection<DebuffTimerViewModel> TargetDebuffs { get; } = new();

    public MeterNavigationPosition? GetNavigationPosition() => _runtime?.GetNavigationPosition();

    public ICommand ResetCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand ToggleModeCommand { get; }
    public ICommand ToggleParticipantsCommand { get; }
    public ICommand ToggleDebuffsCommand { get; }
    public ICommand TogglePvpCommand { get; }
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
        .Replace(" damage", string.Empty, StringComparison.Ordinal)
        .Replace(" healing", string.Empty, StringComparison.Ordinal)
        .Replace(" урона", string.Empty, StringComparison.Ordinal)
        .Replace(" лечения", string.Empty, StringComparison.Ordinal);
    public string CompactDpsText => $"{TotalDpsText}/{T("s", "с")}";
    public string CaptureText { get => _captureText; private set => SetProperty(ref _captureText, value); }
    public string PetStatusText { get => _petStatusText; private set => SetProperty(ref _petStatusText, value); }
    public string PartyTitle { get => _partyTitle; private set => SetProperty(ref _partyTitle, value); }
    public string PartyCapacityText { get => _partyCapacityText; private set => SetProperty(ref _partyCapacityText, value); }
    public string PartyStatusText { get => _partyStatusText; private set => SetProperty(ref _partyStatusText, value); }
    public string EventRegionText { get => _eventRegionText; private set => SetProperty(ref _eventRegionText, value); }
    public string EventClockText { get => _eventClockText; private set => SetProperty(ref _eventClockText, value); }
    public string EventScheduleStatus { get => _eventScheduleStatus; private set => SetProperty(ref _eventScheduleStatus, value); }
    public string EventTimerCountText => T($"{EventTimers.Count} upcoming", $"{EventTimers.Count} ближайших");
    public bool HasPlayers { get => _hasPlayers; private set => SetProperty(ref _hasPlayers, value); }
    public bool HasPets { get => _hasPets; private set => SetProperty(ref _hasPets, value); }
    public bool HasParty { get => _hasParty; private set => SetProperty(ref _hasParty, value); }
    public bool HasEventTimers { get => _hasEventTimers; private set => SetProperty(ref _hasEventTimers, value); }
    public bool HasTargetDebuffs
    {
        get => _hasTargetDebuffs;
        private set
        {
            if (SetProperty(ref _hasTargetDebuffs, value))
                OnPropertyChanged(nameof(ShowDebuffIcons));
        }
    }
    public bool ShowTargetDebuffs
    {
        get => _showTargetDebuffs;
        private set
        {
            if (SetProperty(ref _showTargetDebuffs, value))
            {
                OnPropertyChanged(nameof(DebuffToggleTooltip));
                OnPropertyChanged(nameof(ShowDebuffIcons));
            }
        }
    }
    public bool ShowDebuffIcons => _overlayVisible && ShowTargetDebuffs && HasTargetDebuffs && IsDamageMode;
    public bool ShowOtherPlayersDebuffs
    {
        get => _showOtherPlayersDebuffs;
        set => SetProperty(ref _showOtherPlayersDebuffs, value);
    }
    public bool UsePartyRowStyle
    {
        get => _usePartyRowStyle;
        set => SetProperty(ref _usePartyRowStyle, value);
    }
    public bool KeepNotificationsWhenHidden
    {
        get => _keepNotificationsWhenHidden;
        set
        {
            if (SetProperty(ref _keepNotificationsWhenHidden, value))
                NotifyNotificationVisibilityChanged();
        }
    }
    public bool ShowNotificationPopup =>
        !_notificationUpdateInProgress && HasNotifications && _overlayVisible;
    public bool ShowDetachedNotifications =>
        !_notificationUpdateInProgress && HasNotifications && !_overlayVisible && KeepNotificationsWhenHidden;

    private void NotifyNotificationVisibilityChanged()
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(ShowNotificationPopup));
        OnPropertyChanged(nameof(ShowDetachedNotifications));
    }

    public void SetOverlayVisible(bool visible)
    {
        if (_overlayVisible == visible) return;
        _overlayVisible = visible;
        OnPropertyChanged(nameof(ShowDebuffIcons));
        NotifyNotificationVisibilityChanged();
    }
    public bool IsPvpMode
    {
        get => _isPvpMode;
        private set
        {
            if (!SetProperty(ref _isPvpMode, value)) return;
            OnPropertyChanged(nameof(IsPveMode));
            OnPropertyChanged(nameof(CanToggleBossFilter));
            OnPropertyChanged(nameof(PvpModeTooltip));
            OnPropertyChanged(nameof(CombatModeBadge));
            OnPropertyChanged(nameof(TargetSectionLabel));
            OnPropertyChanged(nameof(EmptyStateText));
            NotifyMetricLabelsChanged();
        }
    }
    public bool IsPveMode => !IsPvpMode;
    public string CombatModeBadge => IsPvpMode ? "PvP" : "PvE";
    public bool CanToggleBossFilter => IsDamageMode && IsPveMode;
    public string PvpModeTooltip => IsPvpMode
        ? T("PvP: damage to enemy players · click for PvE", "PvP: урон по игрокам · нажмите для PvE")
        : T("PvE: damage to monsters · click for PvP", "PvE: урон по мобам · нажмите для PvP");
    public bool IsInCombat { get => _isInCombat; private set => SetProperty(ref _isInCombat, value); }
    public bool EnrageVisible
    {
        get => _enrageVisible;
        private set => SetProperty(ref _enrageVisible, value);
    }
    public string EnrageText { get => _enrageText; private set => SetProperty(ref _enrageText, value); }
    public Brush EnrageBackground { get => _enrageBackground; private set => SetProperty(ref _enrageBackground, value); }
    public Brush EnrageBorder { get => _enrageBorder; private set => SetProperty(ref _enrageBorder, value); }
    public Brush EnrageForeground { get => _enrageForeground; private set => SetProperty(ref _enrageForeground, value); }
    public bool HasNotifications => EventAlerts.Count > 0;
    public Brush TargetBarBrush { get => _targetBarBrush; private set => SetProperty(ref _targetBarBrush, value); }
    public bool IsDemo { get => _isDemo; private set => SetProperty(ref _isDemo, value); }
    public bool IsLocalIdentityPending
    {
        get => _localIdentityPending;
        private set
        {
            if (SetProperty(ref _localIdentityPending, value))
            {
                OnPropertyChanged(nameof(EmptyStateText));
                OnPropertyChanged(nameof(IsSelfView));
                OnPropertyChanged(nameof(IsPartyView));
                OnPropertyChanged(nameof(ParticipantsModeTooltip));
                NotifyMetricLabelsChanged();
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
        MeterDisplayMode.Healing => T("HEALING", "ЛЕЧЕНИЕ"),
        MeterDisplayMode.DamageTaken => T("DAMAGE TAKEN", "ПОЛУЧЕННЫЙ УРОН"),
        _ => T("DAMAGE", "УРОН")
    };
    public string TargetSectionLabel => DisplayMode == MeterDisplayMode.Damage
        ? IsPvpMode ? T("PLAYER TARGET", "ИГРОК-ЦЕЛЬ") : T("CURRENT TARGET", "ТЕКУЩАЯ ЦЕЛЬ")
        : T("MODE", "РЕЖИМ");
    public string ParticipantsHeader => DisplayMode == MeterDisplayMode.DamageTaken
        ? T("SOURCE", "ИСТОЧНИК")
        : IsPartyView ? T("PARTY", "ГРУППА") : T("PLAYER", "ПЕРСОНАЖ");
    public string SummaryDpsLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => IsPartyView ? T("PARTY HPS", "HPS ГРУППЫ") : T("MY HPS", "МОЙ HPS"),
        MeterDisplayMode.DamageTaken => T("INCOMING /S", "ВХОДЯЩИЙ /С"),
        _ => IsPartyView ? T("PARTY DPS", "DPS ГРУППЫ") : T("MY DPS", "МОЙ DPS")
    };
    public string SummaryDamageLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => IsPartyView ? T("Party healing", "Лечение группы") : T("My healing", "Моё лечение"),
        MeterDisplayMode.DamageTaken => T("Damage taken", "Полученный урон"),
        _ => IsPartyView ? T("Party damage", "Урон группы") : T("My damage", "Мой урон")
    };
    public string TotalAmountLabel => DisplayMode == MeterDisplayMode.Healing ? T("TOTAL HEALING", "ОБЩЕЕ ЛЕЧЕНИЕ") : T("TOTAL DAMAGE", "ОБЩИЙ УРОН");
    public string PerSecondColumnLabel => DisplayMode switch
    {
        MeterDisplayMode.Healing => T("HPS/S", "HPS/С"),
        MeterDisplayMode.DamageTaken => T("DAMAGE/S", "УРОН/С"),
        _ => T("DPS/S", "DPS/С")
    };
    public string AmountColumnLabel => DisplayMode == MeterDisplayMode.Healing ? T("HEALING", "ЛЕЧЕНИЕ") : T("DAMAGE", "УРОН");
    public string EmptyStateText => IsLocalIdentityPending
        ? T("RELOG TO YOUR CHARACTER\nWITH THE METER RUNNING", "ПЕРЕЗАЙДИТЕ НА ПЕРСОНАЖА\nС ЗАПУЩЕННЫМ МЕТРОМ")
        : DisplayMode switch
        {
            MeterDisplayMode.Healing => T("NO HEALING DATA", "НЕТ ДАННЫХ ЛЕЧЕНИЯ"),
            MeterDisplayMode.DamageTaken => T("NO DAMAGE TAKEN", "НЕТ ПОЛУЧЕННОГО УРОНА"),
            _ => IsPvpMode ? T("WAITING FOR PVP", "ОЖИДАНИЕ PVP") : T("WAITING FOR COMBAT", "ОЖИДАНИЕ БОЯ")
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
            OnPropertyChanged(nameof(CanToggleBossFilter));
            OnPropertyChanged(nameof(ShowDebuffIcons));
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

    public string ModeText => BossOnly ? T("BOSS", "БОСС") : T("ALL TARGETS", "ВСЕ ЦЕЛИ");
    public string ModeShortText => BossOnly ? T("BOSS", "БОСС") : T("ALL", "ВСЕ");
    public string ModeTooltip => BossOnly ? T("Bosses only", "Только боссы") : T("All targets", "Все цели");
    public string ParticipantsModeTooltip => IsLocalIdentityPending
        ? T("Relog to your character to enable the participant list", "Перезайдите на персонажа, чтобы открыть список участников")
        : ShowPartyMembers
        ? T("Now: whole party · click to show only yourself", "Сейчас: вся группа · нажмите, чтобы оставить только себя")
        : T("Now: only me · click to show the whole party", "Сейчас: только я · нажмите, чтобы показать всю группу");
    public string DebuffToggleTooltip => ShowTargetDebuffs
        ? T("Target debuffs: shown · click to hide", "Дебафы цели: показываются · нажмите, чтобы скрыть")
        : T("Target debuffs: hidden · click to show", "Дебафы цели: скрыты · нажмите, чтобы показать");
    public string PauseText => IsPaused ? T("RESUME", "ПРОДОЛЖИТЬ") : T("PAUSE", "ПАУЗА");
    public string PauseGlyph => IsPaused ? "▶" : "Ⅱ";

    public void RestorePreferences(
        bool bossOnly,
        bool showPartyMembers,
        bool showTargetDebuffs,
        MeterDisplayMode displayMode,
        bool pvpMode = false,
        bool keepNotificationsWhenHidden = false,
        bool showOtherPlayersDebuffs = false,
        bool usePartyRowStyle = false)
    {
        BossOnly = bossOnly;
        DisplayMode = displayMode;
        ShowPartyMembers = displayMode == MeterDisplayMode.DamageTaken || showPartyMembers;
        ShowTargetDebuffs = showTargetDebuffs;
        ShowOtherPlayersDebuffs = showOtherPlayersDebuffs;
        UsePartyRowStyle = usePartyRowStyle;
        IsPvpMode = pvpMode && displayMode == MeterDisplayMode.Damage;
        KeepNotificationsWhenHidden = keepNotificationsWhenHidden;
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(DisplayModeText));
        OnPropertyChanged(nameof(TargetSectionLabel));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(ModeShortText));
        OnPropertyChanged(nameof(ModeTooltip));
        OnPropertyChanged(nameof(ParticipantsModeTooltip));
        OnPropertyChanged(nameof(DebuffToggleTooltip));
        OnPropertyChanged(nameof(PvpModeTooltip));
        OnPropertyChanged(nameof(PauseText));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(EventTimerCountText));
        NotifyMetricLabelsChanged();

        if (_latestSnapshot is not null)
        {
            ApplySnapshot(_latestSnapshot);
            RefreshFeatureData();
        }
        else
        {
            TargetName = BossOnly ? T("Target not detected", "Цель не определена") : T("All targets", "Все цели");
            RefreshSummary();
            RefreshRows();
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized || _disposed)
        {
            return;
        }

        _initialized = true;
        bool forceDemo = Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(arg, "--demo", StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(Path.Combine(AppContext.BaseDirectory, "demo.flag"));

        if (forceDemo)
        {
            EnableDemo(T("Started with --demo", "Запущено с параметром --demo"));
            _timer.Start();
            Refresh();
            return;
        }

        StatusText = T("CONNECTING", "ПОДКЛЮЧЕНИЕ");
        StatusTooltip = T("Loading game data and starting Npcap", "Загрузка справочников и запуск Npcap");
        try
        {
            MeterRuntime runtime = await MeterRuntime.StartAsync();
            if (_disposed)
            {
                runtime.Dispose();
                return;
            }

            _runtime = runtime;
            _runtime.SetBossOnlyMode(BossOnly);
            StatusText = T("READY", "ГОТОВ");
            StatusBrush = ReadyBrush;
            StatusTooltip = T("Network packet capture is active", "Захват сетевых пакетов активен");
            CaptureText = T("Npcap · waiting for combat", "Npcap · ожидание боя");
        }
        catch (Exception exception)
        {
            if (_disposed)
                return;

            StatusText = T("ERROR", "ОШИБКА");
            StatusBrush = ErrorBrush;
            StatusTooltip = exception.Message;
            CaptureText = T("Capture failed; restart after resolving the error", "Ошибка захвата; после исправления перезапустите метр");
        }

        if (_disposed)
            return;
        _timer.Start();
        Refresh();
    }

    private void EnableDemo(string reason)
    {
        IsDemo = true;
        _demoStarted = DateTime.UtcNow - TimeSpan.FromSeconds(86);
        StatusText = T("DEMO", "ДЕМО");
        StatusBrush = DemoBrush;
        StatusTooltip = reason;
        CaptureText = T("Demo data", "Демонстрационные данные");
    }

    private void Refresh()
    {
        if (_disposed || IsPaused)
        {
            return;
        }

        try
        {
            _latestSnapshot = IsDemo ? BuildDemoSnapshot() : _runtime?.GetSnapshot(DisplayMode, IsPvpMode);
            if (_latestSnapshot is null)
            {
                return;
            }

            ApplySnapshot(_latestSnapshot);
            RefreshFeatureData();
            _lastRefreshError = string.Empty;
        }
        catch (Exception exception)
        {
            string error = exception.ToString();
            if (!string.Equals(error, _lastRefreshError, StringComparison.Ordinal))
            {
                _lastRefreshError = error;
                _runtime?.TraceUiError("refresh", exception);
            }
            StatusText = T("ERROR", "ОШИБКА");
            StatusBrush = ErrorBrush;
            StatusTooltip = exception.Message;
        }
    }

    internal void ApplySnapshot(MeterSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        IsInCombat = snapshot.IsInCombat;
        IsLocalIdentityPending = !IsDemo &&
            (!snapshot.LocalPlayerId.HasValue || string.IsNullOrWhiteSpace(snapshot.LocalPlayerName));
        TargetName = IsLocalIdentityPending
            ? T("Character not detected", "Персонаж не определён")
            : string.IsNullOrWhiteSpace(snapshot.TargetName)
            ? (IsPvpMode ? T("Player target not detected", "Игрок-цель не найден") : BossOnly ? T("Target not detected", "Цель не определена") : T("All targets", "Все цели"))
            : snapshot.TargetName;
        TargetFrame = IsLocalIdentityPending ? TargetFrameKind.Normal : snapshot.TargetFrame;
        if (DisplayMode == MeterDisplayMode.Damage)
        {
            TargetHpPercent = IsLocalIdentityPending ? 0 : snapshot.TargetHpPercent ?? 0;
            TargetHpText = !IsLocalIdentityPending && snapshot.TargetHpPercent.HasValue
                ? $"HP {snapshot.TargetHpPercent.Value:0.#}%"
                : "HP";
            TargetBarBrush = DamageTargetBarBrush;
        }
        else
        {
            TargetHpPercent = !IsLocalIdentityPending && snapshot.Players.Count > 0 ? 100 : 0;
            TargetHpText = IsLocalIdentityPending ? "HP" : DisplayMode == MeterDisplayMode.Healing
                ? $"HPS {FormatCompact(snapshot.TotalDps)}"
                : $"{T("INCOMING", "ВХОДЯЩИЙ")} {FormatCompact(snapshot.TotalDps)}/{T("s", "с")}";
            TargetBarBrush = DisplayMode == MeterDisplayMode.Healing
                ? HealingTargetBarBrush
                : DamageTakenTargetBarBrush;
        }
        BattleTime = IsLocalIdentityPending ? "00:00" : FormatDuration(snapshot.BattleTimeMs);
        _latestEnrageTimer = IsLocalIdentityPending ? EnrageTimerView.Hidden : snapshot.EnrageTimer;
        RefreshSummary();

        if (!IsDemo)
        {
            if (IsLocalIdentityPending)
            {
                StatusText = T("RELOG REQUIRED", "НУЖЕН ПЕРЕЗАХОД");
                StatusBrush = ReadyBrush;
                StatusTooltip = T(
                    "Relog to your character with the meter running. The meter starts a fresh fight when login identifies you.",
                    "Перезайдите на персонажа с запущенным метром. После определения персонажа метр начнёт новый бой.");
                CaptureText = T(
                    $"Npcap · {snapshot.ReceivedPackets:N0} packets · {snapshot.DroppedPackets:N0} dropped",
                    $"Npcap · {snapshot.ReceivedPackets:N0} пакетов · {snapshot.DroppedPackets:N0} потерь");
            }
            else
            {
                StatusText = snapshot.IsInCombat ? T("IN COMBAT", "В БОЮ") : T("READY", "ГОТОВ");
                StatusBrush = snapshot.IsInCombat ? CombatBrush : ReadyBrush;
                StatusTooltip = T($"Character: {snapshot.LocalPlayerName}", $"Персонаж: {snapshot.LocalPlayerName}");
                CaptureText = T($"{snapshot.LocalPlayerName} · Npcap {snapshot.ReceivedPackets:N0} packets · {snapshot.DroppedPackets:N0} dropped", $"{snapshot.LocalPlayerName} · Npcap {snapshot.ReceivedPackets:N0} пакетов · {snapshot.DroppedPackets:N0} потерь");
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
                : _runtime?.GetActiveTargetDebuffs(ShowOtherPlayersDebuffs) ?? [];
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
            ? T($"Pets found: {Pets.Count}", $"Найдено питомцев: {Pets.Count}")
            : T("Open the pet collection in game — the list will appear automatically", "Откройте коллекцию питомцев в игре — список появится автоматически");

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
                member.IsInInstance ? T("In dungeon", "В подземелье") : T("Outside dungeon", "Вне подземелья")))
            .ToArray() ?? [];
        SyncCollection(PartyMembers, members);
        HasParty = PartyMembers.Count > 0;
        PartyTitle = string.IsNullOrWhiteSpace(roster?.Title) ? T("Party room", "Комната группы") : roster.Title;
        PartyCapacityText = roster is null ? "0/0" : $"{PartyMembers.Count}/{roster.MaxMembers}";
        PartyStatusText = HasParty
            ? T($"Room roster updated · {PartyCapacityText}", $"Состав комнаты обновлён · {PartyCapacityText}")
            : T("Open matchmaking or a dungeon room — the roster will appear automatically", "Откройте подбор или комнату подземелья — состав появится автоматически");

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

    internal void SyncDebuffs(IReadOnlyList<MeterDebuffSnapshot> snapshots)
    {
        KeyedCollectionSynchronizer.Reconcile(
            TargetDebuffs,
            snapshots,
            snapshot => (snapshot.OwnerId, snapshot.SkillCode),
            row => (row.OwnerId, row.SkillCode),
            snapshot => new DebuffTimerViewModel { SkillCode = snapshot.SkillCode, OwnerId = snapshot.OwnerId },
            (row, snapshot) => row.Update(snapshot, _skillIconService.GetIconPath(snapshot.SkillCode)));

        HasTargetDebuffs = TargetDebuffs.Count > 0;
    }

    private IReadOnlyList<MeterDebuffSnapshot> BuildDemoDebuffs()
    {
        long cycle = (long)(DateTime.UtcNow - _demoStarted).TotalMilliseconds;
        var debuffs = new List<MeterDebuffSnapshot>
        {
            new(11100000, "Ослабление защиты", 18_000 - cycle % 18_000, 18_000, 1),
            new(11200000, "Кровотечение", 10_000 - cycle % 10_000, 10_000, 2)
        };
        if (ShowOtherPlayersDebuffs)
        {
            debuffs.Add(new MeterDebuffSnapshot(
                11100000, "Ослабление защиты", 12_000 - cycle % 12_000, 12_000, 1,
                102, "Asterion", true));
        }
        return debuffs;
    }

    private void SyncEventSchedule(MeterEventScheduleSnapshot snapshot)
    {
        EventRegionText = snapshot.RegionText;
        EventClockText = snapshot.ClockText;
        EventScheduleStatus = snapshot.StatusText;

        KeyedCollectionSynchronizer.Reconcile(
            EventTimers,
            snapshot.Timers,
            timer => timer.Key,
            row => row.Key,
            timer => new EventTimerRowViewModel { Key = timer.Key },
            (row, timer) => row.Update(timer),
            StringComparer.Ordinal);
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
            new("demo-shugo", "ФЕСТИВАЛЬ", "Фестиваль шуго", "Открыта регистрация на событие", riftAt, riftUntil, false, "#FF4EDACB"),
            new("demo-invasion", "ВТОРЖЕНИЕ", "Пространственное вторжение", "Глобальное событие", now.AddMinutes(48), TimeSpan.FromMinutes(48), false, "#FFFFA04D")
        ];
        IReadOnlyList<MeterEventAlertSnapshot> alerts = riftUntil <= TimeSpan.FromMinutes(10)
            ? [
                new MeterEventAlertSnapshot("demo-rift", "Разлом через", EventScheduleService.Countdown(riftUntil), "Пространственный разлом", "#FF6196FF"),
                new MeterEventAlertSnapshot("demo-shugo", "Фестиваль шуго через", EventScheduleService.Countdown(riftUntil), "Фестиваль шуго", "#FF4EDACB")
              ]
            : [];
        return new MeterEventScheduleSnapshot(
            true,
            "Global · Europe",
            "Europe/Berlin · игровое время",
            $"Ближайшее: Разлом · {EventScheduleService.Countdown(riftUntil)}",
            timers,
            alerts);
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

        if (IsLocalIdentityPending)
        {
            Players.Clear();
            HasPlayers = false;
            return;
        }

        List<MeterPlayerSnapshot> allPlayers = _latestSnapshot.Players
            .OrderByDescending(player => player.Dps)
            .ThenBy(player => player.Nickname, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        List<(MeterPlayerSnapshot Player, int Rank)> ordered;
        if (IsPartyView || DisplayMode == MeterDisplayMode.DamageTaken)
        {
            if (DisplayMode != MeterDisplayMode.DamageTaken && LocalPlayerRowPolicy.NeedsPlaceholder(
                    allPlayers.Select(player => (player.PlayerId, (string?)player.Nickname)),
                    _latestSnapshot.LocalPlayerId,
                    _latestSnapshot.LocalPlayerName))
            {
                allPlayers.Add(CreateLocalPlaceholder(_latestSnapshot));
            }

            ordered = VisiblePartyRows.Select(
                allPlayers,
                _latestSnapshot.LocalPlayerId,
                _latestSnapshot.LocalPlayerName,
                pinLocalPlayer: DisplayMode != MeterDisplayMode.DamageTaken);
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
        KeyedCollectionSynchronizer.Reconcile(
            Players,
            ordered,
            entry => entry.Player.PlayerId,
            row => row.PlayerId,
            entry => new PlayerRowViewModel { PlayerId = entry.Player.PlayerId },
            (row, entry) => UpdatePlayerRow(row, entry.Player, entry.Rank, maximum, _latestSnapshot));

        HasPlayers = Players.Count > 0;
    }

    private void UpdatePlayerRow(
        PlayerRowViewModel row,
        MeterPlayerSnapshot player,
        int rank,
        double maximum,
        MeterSnapshot snapshot)
    {
        row.Rank = rank;
        row.Nickname = player.Nickname;
        row.ClassName = player.Job == JobClass.Unknown && DisplayMode == MeterDisplayMode.DamageTaken
            ? T("Enemy", "Противник")
            : ClassLabel(player.Job);
        row.IconPath = ClassIcon(player.Job);
        row.PartyIconPath = PartyClassIcon(player.Job);
        row.AccentBrush = ClassBrush(player.Job);
        row.BarPercent = Math.Clamp(player.Dps / maximum * 100.0, 0, 100);
        row.DpsText = FormatCompact(player.Dps) + "/" + T("s", "с");
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
            snapshot.LocalPlayerId,
            snapshot.LocalPlayerName);
        row.Skills = player.Skills;
        row.Passives = row.IsLocalPlayer ? _runtime?.GetSelectedLocalPassives() ?? [] : [];
        row.Specialties = row.IsLocalPlayer ? _runtime?.GetSelectedLocalSpecialties() ?? [] : [];
        row.RateLabel = PerSecondColumnLabel;
        row.AmountLabel = AmountColumnLabel;
        row.EmptySkillsText = DisplayMode switch
        {
            MeterDisplayMode.Healing => T("Skills will appear after the first heal.", "Навыки появятся после первого лечения."),
            MeterDisplayMode.DamageTaken => T("Attacks will appear after the first damage taken.", "Атаки появятся после первого полученного урона."),
            _ => T("Skills will appear after the first damage dealt.", "Навыки появятся после первого нанесённого урона.")
        };
    }

    private void RefreshSummary()
    {
        if (_latestSnapshot is null)
        {
            return;
        }

        double dps = IsLocalIdentityPending ? 0 : _latestSnapshot.TotalDps;
        long damage = IsLocalIdentityPending ? 0 : _latestSnapshot.TotalDamage;
        if (IsSelfView && DisplayMode != MeterDisplayMode.DamageTaken)
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
            ? T($"{FormatCompact(damage)} healing", $"{FormatCompact(damage)} лечения")
            : T($"{FormatCompact(damage)} damage", $"{FormatCompact(damage)} урона");
        OnPropertyChanged(nameof(CompactTotalText));
        OnPropertyChanged(nameof(CompactDamageText));
        OnPropertyChanged(nameof(CompactDpsText));
    }

    private static MeterPlayerSnapshot CreateLocalPlaceholder(MeterSnapshot snapshot) => new(
        snapshot.LocalPlayerId ?? LocalPlayerRowPolicy.PlaceholderPlayerId,
        snapshot.LocalPlayerName ?? T("Player", "Персонаж"),
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

    private void TogglePvp()
    {
        IsPvpMode = !IsPvpMode;
        if (IsPvpMode && DisplayMode != MeterDisplayMode.Damage)
            DisplayMode = MeterDisplayMode.Damage;
        Players.Clear();
        TargetDebuffs.Clear();
        HasPlayers = false;
        HasTargetDebuffs = false;
        _latestSnapshot = null;
        _latestEnrageTimer = EnrageTimerView.Hidden;
        EnrageVisible = false;
        TargetName = IsPvpMode
            ? T("Player target not detected", "Игрок-цель не найден")
            : BossOnly ? T("Target not detected", "Цель не определена") : T("All targets", "Все цели");
        Refresh();
    }

    private void SetDisplayMode(MeterDisplayMode mode)
    {
        if (DisplayMode == mode)
        {
            return;
        }

        DisplayMode = mode;
        if (mode != MeterDisplayMode.Damage && IsPvpMode)
            IsPvpMode = false;
        Players.Clear();
        TargetDebuffs.Clear();
        HasPlayers = false;
        HasTargetDebuffs = false;
        _latestSnapshot = null;
        _latestEnrageTimer = EnrageTimerView.Hidden;
        EnrageVisible = false;
        TargetName = mode switch
        {
            MeterDisplayMode.Healing => T("Party healing", "Лечение группы"),
            MeterDisplayMode.DamageTaken => T("Damage taken", "Полученный урон"),
            _ => BossOnly ? T("Target not detected", "Цель не определена") : T("All targets", "Все цели")
        };
        Refresh();
    }

    private void ToggleMode()
    {
        if (IsPvpMode) return;
        BossOnly = !BossOnly;
        _runtime?.SetBossOnlyMode(BossOnly);
        Refresh();
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
        StatusTooltip = IsPaused ? T("Panel updates are paused; capture continues", "Обновление панели приостановлено; захват продолжается") : StatusTooltip;
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
            MeterDisplayMode.Healing => T("Party healing", "Лечение группы"),
            MeterDisplayMode.DamageTaken => T("Damage taken", "Полученный урон"),
            _ => BossOnly ? T("Target not detected", "Цель не определена") : T("All targets", "Все цели")
        };
        TargetHpPercent = 0;
        TargetHpText = "HP";
        EnrageVisible = false;
        EnrageText = string.Empty;
        BattleTime = "00:00";
        TotalDpsText = "0";
        TotalDamageText = DisplayMode == MeterDisplayMode.Healing ? T("0 healing", "0 лечения") : T("0 damage", "0 урона");
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
                new(10101, "Теневой удар", dps * 0.42, (long)(dps * seconds * 0.42), 42, 38, 55.3, 64.0, 8.0, 12.0, dps * seconds * 0.42 / 38, (long)(dps * 2.8), (long)(dps * 0.3), 38, 0, (long)(dps * seconds * 0.42), 0, true),
                new(10102, "Клеймо убийцы", dps * 0.34, (long)(dps * seconds * 0.34), 34, 27, 44.4, 51.8, 0, 7.4, dps * seconds * 0.34 / 27, (long)(dps * 2.1), (long)(dps * 0.2), 27, 0, (long)(dps * seconds * 0.34), 0, true),
                new(10103, "Ядовитый клинок", dps * 0.24, (long)(dps * seconds * 0.24), 24, 31, 32.3, 38.7, 0, 0, dps * seconds * 0.24 / 31, (long)(dps * 1.4), (long)(dps * 0.1), 31, 0, (long)(dps * seconds * 0.24), 0, true)
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
        bool bossDemo = damageMode && !IsPvpMode;
        return new MeterSnapshot(
            DisplayMode switch
            {
                MeterDisplayMode.Healing => "Лечение группы",
                MeterDisplayMode.DamageTaken => "Полученный урон",
                _ when IsPvpMode => "Противник",
                _ => "Эфирный Страж"
            },
            bossDemo ? TargetFrameKind.Legend : TargetFrameKind.Normal,
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
            bossDemo
                ? new EnrageTimerView(true, false, "Enrage 03:14 · kill ~02:08", EnrageTimerTone.OnTrack)
                : EnrageTimerView.Hidden);
    }

    private void ApplyEnrageTimer(EnrageTimerView view)
    {
        EnrageVisible = view.Visible;
        EnrageText = UiLanguageService.IsRussian
            ? view.Text
                .Replace("ENRAGED", "БОСС В ЯРОСТИ", StringComparison.Ordinal)
                .Replace("Enrage ", "Ярость через ", StringComparison.Ordinal)
                .Replace(" · kill ~", " · убийство ~", StringComparison.Ordinal)
                .Replace(" · kill ", " · убийство ", StringComparison.Ordinal)
            : view.Text;

        (EnrageBackground, EnrageBorder, EnrageForeground) = view.Tone switch
        {
            EnrageTimerTone.OnTrack or EnrageTimerTone.Tight => OnTrackEnrageBrushes,
            EnrageTimerTone.TooSlow or EnrageTimerTone.Enraged => BehindEnrageBrushes,
            _ => NeutralEnrageBrushes
        };
    }

    private void ApplyNotification() =>
        ApplyNotification(_eventScheduleSnapshot?.Alerts ?? [], _latestEnrageTimer);

    internal void ApplyNotification(IReadOnlyList<MeterEventAlertSnapshot> alerts, EnrageTimerView enrageTimer)
    {
        bool structureChanged = _notificationUpdateInProgress ||
            !EventAlerts.Select(row => row.Key).SequenceEqual(
                alerts.Select(alert => alert.Key).Distinct(StringComparer.Ordinal),
                StringComparer.Ordinal);
        if (structureChanged)
        {
            _notificationUpdateInProgress = true;
            _notificationUpdateVersion++;
            NotifyNotificationVisibilityChanged();
        }

        bool completed = false;
        try
        {
            ApplyEnrageTimer(enrageTimer);
            KeyedCollectionSynchronizer.Reconcile(
                EventAlerts,
                alerts,
                alert => alert.Key,
                row => row.Key,
                alert => new EventAlertRowViewModel { Key = alert.Key },
                (row, alert) => row.Update(alert),
                StringComparer.Ordinal);
            completed = true;
        }
        finally
        {
            if (structureChanged && completed)
            {
                int version = _notificationUpdateVersion;
                _timer.Dispatcher.BeginInvoke(() =>
                {
                    if (_disposed || version != _notificationUpdateVersion)
                        return;

                    _notificationUpdateInProgress = false;
                    NotifyNotificationVisibilityChanged();
                }, DispatcherPriority.Loaded);
            }
        }

        string trace = $"source={alerts.Count} rows={EventAlerts.Count} " +
            $"sourceKeys=[{string.Join(',', alerts.Select(alert => alert.Key))}] " +
            $"rowKeys=[{string.Join(',', EventAlerts.Select(row => row.Key))}]";
        if (!string.Equals(trace, _lastUiAlertTrace, StringComparison.Ordinal))
        {
            _lastUiAlertTrace = trace;
            TraceUiAlertState(trace);
        }
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
        JobClass.Gladiator => T("Gladiator", "Гладиатор"),
        JobClass.Templar => T("Templar", "Страж"),
        JobClass.Ranger => T("Ranger", "Стрелок"),
        JobClass.Assassin => T("Assassin", "Убийца"),
        JobClass.Sorcerer => T("Sorcerer", "Волшебник"),
        JobClass.Cleric => T("Cleric", "Целитель"),
        JobClass.Elementalist => T("Spiritmaster", "Заклинатель"),
        JobClass.Chanter => T("Chanter", "Чародей"),
        JobClass.Fighter => T("Fighter", "Кулачник"),
        _ => T("Unknown", "Неизвестно")
    };

    private static string T(string english, string russian) => UiLanguageService.Pick(english, russian);

    private static string? ClassIcon(JobClass job)
    {
        if (job == JobClass.Unknown)
        {
            return null;
        }
        return Path.Combine(AppContext.BaseDirectory, "data", "textures", "classes", job + ".png");
    }

    private static string? PartyClassIcon(JobClass job) => job == JobClass.Unknown
        ? null
        : Path.Combine(AppContext.BaseDirectory, "data", "textures", "party", "classes", job + ".png");

    private static Brush ClassBrush(JobClass job) =>
        ClassBrushes.TryGetValue(job, out Brush? brush) ? brush : UnknownClassBrush;

    private static Brush MakeTargetHpGradient()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFDA030F"), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFF061C"), 0.52));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFF0456"), 0.78));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFF05BD"), 1));
        brush.Freeze();
        return brush;
    }

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
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _runtime?.Dispose();
        _runtime = null;
    }
}
