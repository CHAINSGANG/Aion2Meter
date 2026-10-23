using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AionFlex.Application.Data;
using AionFlex.Application.DpsCalculation;
using AionFlex.Application.Mobs;
using AionFlex.Application.Skills;
using AionFlex.Core.Combat;
using AionFlex.Core.Models;
using AionFlex.Infrastructure.PacketCapture;
using AionFlex.Infrastructure.PacketParsing.Dps;
using Aion2Meter;
using Aion2Meter.Controls;
using Aion2Meter.Services;
using Aion2Meter.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

int passed = 0;
var rows = new ObservableCollection<Row>();
int collectionChanges = 0;
rows.CollectionChanged += (_, _) => collectionChanges++;

Sync([new Snapshot("one", 1), new Snapshot("two", 2)]);
Check(rows.Count == 2 && rows[0].Key == "one" && rows[1].Key == "two", "initial order");
Row first = rows[0];
Row second = rows[1];

int changesBeforeRefresh = collectionChanges;
Sync([new Snapshot("one", 10), new Snapshot("two", 20)]);
Check(collectionChanges == changesBeforeRefresh, "unchanged keys must not rebuild UI rows");
Check(ReferenceEquals(first, rows[0]) && first.Value == 10, "first row is reused and updated");

Sync([new Snapshot("two", 21), new Snapshot("one", 11)]);
Check(ReferenceEquals(second, rows[0]) && ReferenceEquals(first, rows[1]), "rows are moved, not recreated");

Sync([new Snapshot("two", 22), new Snapshot("two", 999)]);
Check(rows.Count == 1 && ReferenceEquals(second, rows[0]) && second.Value == 22,
    "duplicate source keys create only one row");

rows.Add(new Row("two"));
Sync([new Snapshot("two", 23)]);
Check(rows.Count == 1 && ReferenceEquals(second, rows[0]), "duplicate existing rows are removed");

Sync([]);
Check(rows.Count == 0, "stale rows are removed");

Check(MeterWindowHeightPolicy.MinimumHeight(compact: true, rowCount: 10) == 574,
    "compact window reserves space for ten rows without scrolling");
Check(MeterWindowHeightPolicy.MinimumHeight(compact: true, rowCount: 12) == 574,
    "compact window never grows beyond ten visible rows");
Check(MeterWindowHeightPolicy.DesiredHeight(compact: true, rowCount: 5, preferredHeight: 343, maximumHeight: 920) == 374,
    "a saved compact size still expands when a fifth player appears");
Check(MeterWindowHeightPolicy.DesiredHeight(compact: true, rowCount: 1, preferredHeight: 218, maximumHeight: 920) == 218,
    "compact window returns to one-row height when the party shrinks");
Check(MeterWindowHeightPolicy.MinimumHeight(compact: false, rowCount: 10) == 730,
    "expanded window reserves space for ten rows without scrolling");
var unknownClassRow = new PlayerRowViewModel { PlayerId = 1 };
Check(!unknownClassRow.HasClassIcon, "an unidentified class does not display a substitute person icon");
unknownClassRow.IconPath = "Cleric.png";
Check(unknownClassRow.HasClassIcon, "a discovered class replaces the neutral icon");
Check(!unknownClassRow.HasPartyIcon, "an unidentified class has no party-style icon");
unknownClassRow.PartyIconPath = "Cleric.png";
Check(unknownClassRow.HasPartyIcon, "party-style rows use a dedicated class icon");
string? skillIconPath = new SkillIconService().GetIconPath(1);
Check(skillIconPath is not null && File.Exists(skillIconPath),
    "a mapped game skill resolves to an existing skill texture");
var sampleSkill = new MeterSkillSnapshot(1, "Sample skill", 100, 1_000, 50, 5,
    25, 33, 0, 0, 200, 450, 40, 4, 1, 800, 200, true);
var sampleSkillRow = new SkillRowViewModel(sampleSkill, skillIconPath);
Check(sampleSkillRow.IconPath == skillIconPath && sampleSkillRow.HitsText == "4" && sampleSkillRow.DotHitsText == "1" &&
      sampleSkillRow.MinimumText == "40" && sampleSkillRow.BreakdownTooltip.Contains("200"),
    "skill breakdown shows its icon, real minimum, and direct/DoT split");
var dotOnlyRow = new SkillRowViewModel(sampleSkill with
{
    DirectHits = 0, DotHits = 5, HasHitQualityData = false
}, skillIconPath);
Check(dotOnlyRow.CritText == "—" && dotOnlyRow.BackText == "—" &&
      dotOnlyRow.DoubleText == "—" && dotOnlyRow.PerfectText == "—",
    "unmeasured hit-quality rates are not displayed as zero percent");
var loadout = new SkillLoadoutSnapshot(
    [new SkillRosterEntry(11720000, 3)],
    [
        new SkillEquippedEntry(11710000, 5, [1]),
        new SkillEquippedEntry(11710000, 4, [1]),
        new SkillEquippedEntry(11010000, 2, [2])
    ]);
IReadOnlyList<SkillEquippedEntry> mergedEquipped = SkillLoadoutAccumulator.MergeEquipped(
    loadout.Equipped,
    [new SkillEquippedEntry(11010000, 4, [1, 3])]);
Check(mergedEquipped.Count == 2 && mergedEquipped.Single(entry => entry.BaseCode == 11010000).Specs.SequenceEqual([1, 3]) &&
      mergedEquipped.Any(entry => entry.BaseCode == 11710000),
    "a partial skill update replaces its selected options without erasing other loadout skills");
string testDataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
var skillRepository = new SkillRepository(
    NullLogger<SkillRepository>.Instance,
    skillsFilePath: Path.Combine(testDataDirectory, "skills.json"),
    npcSkillsFilePath: Path.Combine(testDataDirectory, "npc_skills.json"),
    theostonesFilePath: Path.Combine(testDataDirectory, "theostones.json"),
    skillEffectTypesFilePath: Path.Combine(testDataDirectory, "skill_effect_types.json"),
    summonSkillsFilePath: Path.Combine(testDataDirectory, "summon_skills.json"));
skillRepository.InitializeAsync().GetAwaiter().GetResult();
var mobRepository = new MobRepository(
    NullLogger<MobRepository>.Instance,
    mobsFilePath: Path.Combine(testDataDirectory, "mobs.json"),
    npcSummonsFilePath: Path.Combine(testDataDirectory, "npc_summons.json"));
mobRepository.ReloadAsync().GetAwaiter().GetResult();
Check(mobRepository.GetMobDungeon(2345107) == "world" && mobRepository.IsBoss(2345107),
    "Grandios is a named world boss, not an ordinary world mob");
var bossDamageRepository = new DataRepository(NullLogger<DataRepository>.Instance);
bossDamageRepository.SetLocalPlayerName("Lunari");
bossDamageRepository.SetNickname(1540, "Lunari", isConfirmedPlayer: true, isAuthoritativeSource: true);
bossDamageRepository.SetEntityNpcId(50759, 2345107);
bossDamageRepository.SetMobName(50759, "Grandios");
long bossHitTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
bossDamageRepository.AppendDamageEvent(new DamageEvent
{
    Timestamp = bossHitTime, ActorId = 1540, TargetId = 50759, Damage = 2500, SkillCode = 14380007
});
bossDamageRepository.AppendDamageEvent(new DamageEvent
{
    Timestamp = bossHitTime + 1000, ActorId = 1540, TargetId = 50759, Damage = 3200, SkillCode = 14380007
});
var worldBossCalculator = new DpsCalculator(
    NullLogger<DpsCalculator>.Instance, bossDamageRepository, skillRepository, mobRepository);
worldBossCalculator.SetBossOnlyMode(true);
Check(worldBossCalculator.CalculateDps().GetPlayersByDps().Any(player =>
        player.PlayerId == 1540 && player.TotalDamage >= 5700),
    "boss-only mode counts Lunari's damage to the named world boss Grandios");
IReadOnlyList<MeterPassiveSkillSnapshot> selectedPassives = PassiveLoadoutSkills.FromLoadout(
    loadout,
    skillRepository.GetSkillMetadata,
    skillRepository.GetSkillName);
Check(selectedPassives.Count == 2 && selectedPassives[0].SkillCode == 11710000 &&
      selectedPassives[0].Level == 5 && selectedPassives[0].IsEquipped &&
      selectedPassives[1].SkillCode == 11720000 && !selectedPassives[1].IsEquipped &&
      selectedPassives[0].Name == "Survival Stance",
    "the loadout distinguishes selected passives from merely learned passives");
Check(new SkillIconService().GetIconPath(11710000) is { } passiveIcon && File.Exists(passiveIcon),
    "an equipped passive resolves to its own game icon");
IReadOnlyList<MeterSkillSpecialtySnapshot> selectedSpecialties = SkillSpecialtySelection.FromLoadout(
    loadout,
    skillRepository.GetSkillMetadata,
    skillRepository.GetSkillName);
Check(selectedSpecialties.Count == 1 && selectedSpecialties[0].SkillCode == 11010000 &&
      selectedSpecialties[0].SelectedOptions.SequenceEqual([2]),
    "mastery specialties are selected from equipped options without mixing in passive skills");
string specialtyDescription = new SkillSpecialtyCatalog().GetDescription(11010000, 2);
Check(specialtyDescription.Contains("Block") || specialtyDescription.Contains("Блок"),
    "selected specialty resolves to a localized effect description");
Check(new SkillSpecialtyCatalog().GetOptionIconPath(11010000, 2) is { } specialtyIcon &&
      File.Exists(specialtyIcon),
    "selected specialty resolves to the matching game icon");
string unknownSpecialty = new SkillSpecialtyCatalog().GetDescription(19010000, 3);
Check(unknownSpecialty.Contains("Option") || unknownSpecialty.Contains("Вариант"),
    "an unmapped specialty is identified by option number instead of a fabricated effect");
Check(new SkillSpecialtyCatalog().GetOptionIconPath(19010000, 3) is { } brawlerSpecialtyIcon &&
      File.Exists(brawlerSpecialtyIcon),
    "a newly introduced class still receives its matching specialty icon");
var localBuildRow = new PlayerRowViewModel
{
    IsLocalPlayer = true,
    Passives = selectedPassives,
    Specialties = selectedSpecialties
};
var localBuildView = new PlayerBreakdownViewModel(localBuildRow);
Check(localBuildView.HasPassives && localBuildView.Passives[0].IconPath is not null &&
      localBuildView.Passives[0].StatusText != localBuildView.Passives[1].StatusText,
    "the local player's breakdown exposes passive icons and distinct selection states");
localBuildView.UpdatePassives([]);
Check(!localBuildView.HasPassives, "an open breakdown can refresh when the local loadout changes");
Check(localBuildView.HasSpecialties && localBuildView.Specialties[0].SelectedOptions.Count == 1 &&
      localBuildView.Specialties[0].Slots.Count == 3,
    "the local player's breakdown exposes selected effects and three specialty slots");
localBuildView.UpdateSpecialties([]);
Check(!localBuildView.HasSpecialties, "an open breakdown can refresh selected specialties");
Check(JobClassExtensions.FromSkillCode(17410041) == JobClass.Cleric,
    "a non-damage skill can identify the class of an otherwise idle player");
Check(!MeterRuntime.HasLivingBossHealth(null) && !MeterRuntime.HasLivingBossHealth(0) &&
      MeterRuntime.HasLivingBossHealth(1),
    "enrage cannot continue once boss HP is zero or no longer available");
var targetEffects = new List<ActiveDebuffWindow>
{
    new(900, 1, 101, 1, 11100000, 1_000, 20_000, 19_000),
    new(900, 2, 102, 2, 11100000, 1_000, 19_000, 18_000),
    new(900, 3, 103, 3, 11100000, 1_000, 18_000, 17_000),
    new(900, 4, 104, 2, 11200000, 1_000, 17_000, 16_000),
    new(900, 5, 105, 2, 11100000, 9_000, 10_000, 500)
};
IReadOnlyList<MeterDebuffSnapshot> ProjectTargetEffects(bool showOthers) => TargetDebuffSelection.Project(
    targetEffects, 1, showOthers, 10_000,
    _ => null,
    owner => owner == 2,
    skill => new SkillMetadata { Type = "Active", Categories = skill == 11100000 ? ["Debuff"] : ["Buff"] },
    _ => "Defense down",
    owner => owner == 2 ? "Ally" : null);
IReadOnlyList<MeterDebuffSnapshot> ownEffects = ProjectTargetEffects(false);
IReadOnlyList<MeterDebuffSnapshot> partyEffects = ProjectTargetEffects(true);
Check(ownEffects.Count == 1 && ownEffects[0].OwnerId == 1,
    "target debuffs show only local casts while the new option is off");
Check(partyEffects.Count == 2 && partyEffects.Any(effect => effect.OwnerId == 2 && effect.IsOtherPlayer) &&
      partyEffects.Any(effect => effect.OwnerId == 1 && !effect.IsOtherPlayer),
    "the option adds known players' casts and keeps duplicate skills distinct by owner");
Check(partyEffects.All(effect => effect.OwnerId != 3),
    "unknown NPC or unconfirmed casters are not presented as other players");
var foreignDebuffRow = new DebuffTimerViewModel { SkillCode = 11100000, OwnerId = 2 };
foreignDebuffRow.Update(partyEffects.Single(effect => effect.OwnerId == 2), null);
Check(foreignDebuffRow.IsOtherPlayer && foreignDebuffRow.Name.Contains("Ally"),
    "a foreign debuff is marked and names its caster in the tooltip");
var defeatedHealth = MeterRuntime.ResolveTargetHealth(null, defeated: true, defeatedMaxHp: 15_504_000);
Check(defeatedHealth.CurrentHp == 0 && defeatedHealth.MaxHp == 15_504_000 && defeatedHealth.Percent == 0,
    "removed defeated target retains HP 0% instead of becoming unknown HP");
var bossPullClock = new BossPullClock();
bossPullClock.OnDamage(730, 101, 100, isHeal: false, timestampMs: 10_000);
Check(bossPullClock.Snapshot(730, 11_000).PullFirstHitMs == 10_000,
    "boss pull records its first damaging hit");
bossPullClock.OnRemoved(730);
Check(bossPullClock.Snapshot(730, 11_100).PullFirstHitMs is null,
    "removed bosses no longer retain an active enrage pull");

List<MeterPlayerSnapshot> rankedPlayers = Enumerable.Range(1, 12)
    .Select(rank => new MeterPlayerSnapshot(
        rank, rank == 12 ? "Me" : $"Player {rank}", JobClass.Unknown,
        12000 - rank, 120000 - rank, 0, 0, 0, 0, []))
    .ToList();
List<(MeterPlayerSnapshot Player, int Rank)> visiblePlayers = VisiblePartyRows.Select(
    rankedPlayers, localPlayerId: 12, localPlayerName: "Me", pinLocalPlayer: true);
Check(visiblePlayers.Count == 10 && visiblePlayers[8].Rank == 9,
    "party view displays at most nine higher-ranked players before the local slot");
Check(visiblePlayers[9].Player.PlayerId == 12 && visiblePlayers[9].Rank == 12,
    "the tenth slot pins the local player with their real rank");
Check(VisiblePartyRows.Select(rankedPlayers, 12, "Me", pinLocalPlayer: false)[9].Rank == 10,
    "modes without a local-player slot retain the normal top ten");

TcpPayloadSequenceDecision firstPacket = TcpPayloadSequencePolicy.Evaluate(null, sequence: 100, payloadLength: 20);
Check(!firstPacket.Drop && firstPacket.NextExpected == 120, "first TCP payload establishes the sequence");
TcpPayloadSequenceDecision inOrder = TcpPayloadSequencePolicy.Evaluate(120, sequence: 120, payloadLength: 10);
Check(!inOrder.Drop && inOrder.SkipBytes == 0 && inOrder.NextExpected == 130,
    "in-order TCP payload is delivered completely");
TcpPayloadSequenceDecision overlap = TcpPayloadSequencePolicy.Evaluate(120, sequence: 110, payloadLength: 20);
Check(!overlap.Drop && overlap.SkipBytes == 10 && overlap.NextExpected == 130,
    "overlapping retransmission contributes only new TCP bytes");
Check(TcpPayloadSequencePolicy.Evaluate(120, sequence: 100, payloadLength: 20).Drop,
    "complete retransmission is discarded");
TcpPayloadSequenceDecision gap = TcpPayloadSequencePolicy.Evaluate(120, sequence: 125, payloadLength: 10);
Check(!gap.Drop && gap.GapBytes == 5 && gap.NextExpected == 135,
    "TCP gap is measured before advancing the sequence");
TcpPayloadSequenceDecision wrapped = TcpPayloadSequencePolicy.Evaluate(uint.MaxValue - 1, sequence: 1, payloadLength: 4);
Check(!wrapped.Drop && wrapped.GapBytes == 3 && wrapped.NextExpected == 5,
    "TCP sequence wraparound preserves a small forward gap");
Check(UpdateCheckService.IsTrustedReleaseUrl("https://github.com/CHAINSGANG/Aion2Meter/releases/tag/v0.2.8"),
    "update notification accepts this repository's release page");
Check(!UpdateCheckService.IsTrustedReleaseUrl("file:///C:/Windows/System32/calc.exe") &&
      !UpdateCheckService.IsTrustedReleaseUrl("https://example.com/CHAINSGANG/Aion2Meter/releases/tag/v0.2.8") &&
      !UpdateCheckService.IsTrustedReleaseUrl("https://github.com.evil.test/CHAINSGANG/Aion2Meter/releases/tag/v0.2.8"),
    "update notification rejects non-release and non-HTTPS launch targets");
const string installerName = "Aion2Meter-Setup-v0.3.0.exe";
const string assetRoot = "https://github.com/CHAINSGANG/Aion2Meter/releases/download/v0.3.0/";
using (JsonDocument release = JsonDocument.Parse("""
    {"assets":[
      {"name":"Aion2Meter-Setup-v0.3.0.exe","size":12345,"browser_download_url":"https://github.com/CHAINSGANG/Aion2Meter/releases/download/v0.3.0/Aion2Meter-Setup-v0.3.0.exe"},
      {"name":"Aion2Meter-Setup-v0.3.0.exe.sha256","size":95,"browser_download_url":"https://github.com/CHAINSGANG/Aion2Meter/releases/download/v0.3.0/Aion2Meter-Setup-v0.3.0.exe.sha256"}
    ]}
    """))
{
    UpdatePackage? package = UpdateCheckService.TryReadPackage(release.RootElement, "v0.3.0", new Version(0, 3, 0));
    Check(package?.InstallerName == installerName && package.InstallerSize == 12345,
        "updater selects matching installer and checksum assets");
    Check(UpdateCheckService.TryReadPackage(release.RootElement, "v0.3.1", new Version(0, 3, 1)) is null,
        "updater rejects assets from a different version");
}
Check(UpdateCheckService.IsTrustedAssetUrl(assetRoot + installerName, "v0.3.0", installerName) &&
      !UpdateCheckService.IsTrustedAssetUrl("https://github.com.evil.test/" + installerName, "v0.3.0", installerName) &&
      !UpdateCheckService.IsTrustedAssetUrl(assetRoot + installerName + "?redirect=evil", "v0.3.0", installerName),
    "updater accepts only this repository's exact HTTPS asset URL");
string checksum = new string('a', 64);
Check(UpdateInstallService.TryParseChecksum($"{checksum}  {installerName}\n", installerName, out string? parsedHash) &&
      parsedHash == checksum &&
      !UpdateInstallService.TryParseChecksum($"{checksum}  other.exe", installerName, out _) &&
      !UpdateInstallService.TryParseChecksum($"{checksum[..63]}  {installerName}", installerName, out _),
    "updater verifies the SHA-256 manifest format and installer filename");
string installerTestDirectory = Path.Combine(Path.GetTempPath(), $"Aion2Meter.Updater.Smoke.{Guid.NewGuid():N}");
Directory.CreateDirectory(installerTestDirectory);
try
{
    string installedExe = Path.Combine(installerTestDirectory, "Aion2Meter.exe");
    string setupExe = Path.Combine(installerTestDirectory, installerName);
    File.WriteAllBytes(installedExe, [0]);
    File.WriteAllBytes(setupExe, [0]);
    var startInfo = UpdateInstallService.CreateInstallerStartInfo(setupExe, installerTestDirectory);
    Check(startInfo.UseShellExecute && startInfo.Verb == "runas" &&
          startInfo.ArgumentList.Count == 1 &&
          startInfo.ArgumentList[0] == $"/DIR={installerTestDirectory}",
        "installer targets the current meter directory and requests Windows elevation");
}
finally
{
    File.Delete(Path.Combine(installerTestDirectory, "Aion2Meter.exe"));
    File.Delete(Path.Combine(installerTestDirectory, installerName));
    Directory.Delete(installerTestDirectory);
}
Check(NpcapServiceLifecycle.ParseServiceState("STATE              : 3  STOP_PENDING") == 3 &&
      NpcapServiceLifecycle.ParseServiceState("STATE              : 4  RUNNING") == 4,
    "Npcap lifecycle detects pending shutdown and an already-running driver");
Check(NpcapServiceLifecycle.IsKnownConcurrentCaptureProcess("AionFlex") &&
      NpcapServiceLifecycle.IsKnownConcurrentCaptureProcess("DUMPCAP") &&
      !NpcapServiceLifecycle.IsKnownConcurrentCaptureProcess("Aion2Meter"),
    "Npcap shutdown recognizes other packet-capture applications");

Exception? notificationFailure = null;
var uiThread = new Thread(() =>
{
    try
    {
        using var viewModel = new MeterViewModel();
        viewModel.SyncDebuffs(partyEffects);
        Check(viewModel.TargetDebuffs.Count == 2 &&
              viewModel.TargetDebuffs.Select(row => row.OwnerId).Distinct().Count() == 2,
            "same-skill debuffs from two players remain separate overlay icons");
        viewModel.SyncDebuffs(ownEffects);
        Check(viewModel.TargetDebuffs.Count == 1 && viewModel.TargetDebuffs[0].OwnerId == 1,
            "turning off the option removes only foreign debuff icons");
        Check(viewModel.TargetBarBrush is LinearGradientBrush hpGradient &&
              hpGradient.GradientStops[0].Color.R > hpGradient.GradientStops[0].Color.B &&
              hpGradient.GradientStops[^1].Color.B > hpGradient.GradientStops[0].Color.B,
            "target HP uses the game's red-to-magenta fill");
        var countsAtOpen = new List<int>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MeterViewModel.ShowNotificationPopup) && viewModel.ShowNotificationPopup)
                countsAtOpen.Add(viewModel.EventAlerts.Count);
        };

        viewModel.ApplyNotification(
        [
            new MeterEventAlertSnapshot("boss", "Watcher Kaira", "09:54", "", "#FFB28AFF"),
            new MeterEventAlertSnapshot("festival", "Shugo Festival", "09:54", "", "#FF4EDACB")
        ], EnrageTimerView.Hidden);
        Check(viewModel.EventAlerts.Count == 2 && !viewModel.ShowNotificationPopup,
            "popup stays closed while notification rows are built");

        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.Background);
        Dispatcher.PushFrame(frame);
        Check(viewModel.ShowNotificationPopup && countsAtOpen.SequenceEqual([2]),
            "popup first opens with the complete notification list");
        Check(viewModel.EventAlerts[0].TickerBackground is LinearGradientBrush bossColor &&
              viewModel.EventAlerts[1].TickerBackground is LinearGradientBrush festivalColor &&
              bossColor.GradientStops[0].Color != festivalColor.GradientStops[0].Color,
            "each ticker event retains its own category color");
        Check(viewModel.EventAlerts.All(row => row.TickerBackground is LinearGradientBrush background &&
              background.GradientStops.All(stop => stop.Color.A > 0)),
            "adjacent ticker items never fade to a dark empty seam");

        EventAlertRowViewModel firstAlert = viewModel.EventAlerts[0];
        int alertCollectionChanges = 0;
        viewModel.EventAlerts.CollectionChanged += (_, _) => alertCollectionChanges++;
        viewModel.ApplyNotification(
        [
            new MeterEventAlertSnapshot("boss", "Watcher Kaira", "NOW", "", "#FFB28AFF"),
            new MeterEventAlertSnapshot("festival", "Shugo Festival", "09:53", "", "#FF4EDACB")
        ], EnrageTimerView.Hidden);
        Check(viewModel.ShowNotificationPopup && alertCollectionChanges == 0,
            "countdown changes do not reopen or rebuild notification rows");
        Check(ReferenceEquals(firstAlert, viewModel.EventAlerts[0]) && firstAlert.Countdown == "NOW",
            "countdown changes update the existing notification row");
        viewModel.KeepNotificationsWhenHidden = true;
        viewModel.SetOverlayVisible(false);
        Check(!viewModel.ShowNotificationPopup && viewModel.ShowDetachedNotifications,
            "alerts switch to a detached surface when the meter is hidden and the option is on");
        viewModel.KeepNotificationsWhenHidden = false;
        Check(!viewModel.ShowNotificationPopup && !viewModel.ShowDetachedNotifications,
            "alerts hide with the meter when the option is off");
        viewModel.SetOverlayVisible(true);
        Check(viewModel.ShowNotificationPopup && !viewModel.ShowDetachedNotifications,
            "alerts return to the attached popup when the meter reappears");
        var detachedWindow = new NotificationWindow(viewModel);
        Check(detachedWindow.Owner is null && detachedWindow.Topmost && !detachedWindow.ShowInTaskbar,
            "hidden-meter alerts have a separate ownerless topmost window");
        var ticker = new NotificationTicker { DataContext = viewModel, Width = 560 };
        ticker.Measure(new Size(560, 26));
        ticker.Arrange(new Rect(0, 0, 560, 26));
        ticker.UpdateLayout();
        var primaryCycle = (ContentPresenter?)ticker.FindName("PrimaryCycle");
        double initialCycleWidth = primaryCycle?.ActualWidth ?? 0;
        viewModel.ApplyNotification(
        [
            new MeterEventAlertSnapshot("boss", "Watcher Kaira", "СЕЙЧАС", "", "#FFB28AFF"),
            new MeterEventAlertSnapshot("festival", "Shugo Festival", "NOW", "", "#FF4EDACB")
        ], EnrageTimerView.Hidden);
        ticker.UpdateLayout();
        Check(initialCycleWidth > 0 && primaryCycle is not null &&
              Math.Abs(primaryCycle.ActualWidth - initialCycleWidth) < 0.1,
            "ticker cycle width stays fixed when countdown text changes language or length");
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_PREVIEW") is { Length: > 0 } previewPath)
        {
            var bitmap = new RenderTargetBitmap(560, 26, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(ticker);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream output = File.Create(previewPath);
            encoder.Save(output);
        }
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_ICON_PREVIEW") is { Length: > 0 } iconPreviewPath)
        {
            var icon = new Border
            {
                Width = 120,
                Height = 120,
                Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/Aion2Meter;component/Assets/Textures/HudAtlas.png")),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(916, 149, 40, 40),
                    Stretch = Stretch.Uniform
                }
            };
            icon.Measure(new Size(120, 120));
            icon.Arrange(new Rect(0, 0, 120, 120));
            var bitmap = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(icon);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream output = File.Create(iconPreviewPath);
            encoder.Save(output);
        }
        viewModel.ApplyNotification([], new EnrageTimerView(true, false, "Enrage 03:14 · kill ~02:08", EnrageTimerTone.OnTrack));
        Check(viewModel.EnrageVisible && !viewModel.HasNotifications && !viewModel.ShowNotificationPopup &&
              !viewModel.ShowDetachedNotifications,
            "boss enrage stays by HP and never creates an event ticker");
        Color onTrackColor = ((SolidColorBrush)viewModel.EnrageForeground).Color;
        viewModel.ApplyNotification([], new EnrageTimerView(true, false, "Enrage 03:14 · kill ~03:40", EnrageTimerTone.TooSlow));
        Color tooSlowColor = ((SolidColorBrush)viewModel.EnrageForeground).Color;
        Check(onTrackColor.G > onTrackColor.R && tooSlowColor.R > tooSlowColor.G,
            "enrage highlight is green within the limit and red when the estimate misses it");
        var app = new App();
        app.InitializeComponent();
        var toolsWindow = new ToolsWindow(viewModel);
        var toolsSurface = (FrameworkElement)toolsWindow.Content;
        toolsSurface.Measure(new Size(720, 570));
        toolsSurface.Arrange(new Rect(0, 0, 720, 570));
        toolsSurface.UpdateLayout();
        var otherDebuffsCheckbox = (System.Windows.Controls.CheckBox)toolsWindow.FindName("OtherPlayersDebuffsCheckBox")!;
        otherDebuffsCheckbox.IsChecked = true;
        Check(viewModel.ShowOtherPlayersDebuffs,
            "the tools checkbox enables other players' target debuffs");
        otherDebuffsCheckbox.IsChecked = false;
        Check(!viewModel.ShowOtherPlayersDebuffs,
            "the tools checkbox returns to own-only target debuffs");
        var partyRowStyleCheckbox = (System.Windows.Controls.CheckBox)toolsWindow.FindName("PartyRowStyleCheckBox")!;
        partyRowStyleCheckbox.IsChecked = true;
        Check(viewModel.UsePartyRowStyle, "the tools checkbox enables party-style damage rows");
        partyRowStyleCheckbox.IsChecked = false;
        Check(!viewModel.UsePartyRowStyle, "the tools checkbox restores standard damage rows");
        string? skillPreviewPath = Environment.GetEnvironmentVariable("AION2METER_RENDER_SKILL_PREVIEW");
        string? passivePreviewPath = Environment.GetEnvironmentVariable("AION2METER_RENDER_PASSIVES_PREVIEW");
        if (!string.IsNullOrWhiteSpace(skillPreviewPath) || !string.IsNullOrWhiteSpace(passivePreviewPath))
        {
            var previewPlayer = new PlayerRowViewModel
            {
                Nickname = "Sample player",
                ClassName = "Gladiator",
                IsLocalPlayer = true,
                DpsText = "88.2k/s",
                DamageText = "4.23m",
                ShareText = "42.1%",
                Hits = 129,
                CritRate = 38.6,
                Passives = [
                    new MeterPassiveSkillSnapshot(11710000, "Survival Stance", 5, true),
                    new MeterPassiveSkillSnapshot(11720000, "Protection Armor", 4, false),
                    new MeterPassiveSkillSnapshot(11730000, "Blood Absorption", 5, true),
                    new MeterPassiveSkillSnapshot(11740000, "Identify Weakness", 3, false)
                ],
                Skills = [
                    sampleSkill with { SkillCode = 1, Name = "Explosive Strike", Share = 41.2 },
                    sampleSkill with { SkillCode = 301, Name = "Flame Burst", Share = 27.4 },
                    sampleSkill with { SkillCode = 1001, Name = "Arcane Wave", Share = 18.3 },
                    sampleSkill with { SkillCode = 1002, Name = "Burning Mark", Share = 13.1 }
                ]
            };
            var breakdownWindow = new PlayerBreakdownWindow(previewPlayer);
            IReadOnlyList<MeterPassiveSkillSnapshot> previewPassives = previewPlayer.Passives;
            previewPlayer.Passives = [];
            Check(!((PlayerBreakdownViewModel)breakdownWindow.DataContext).HasPassives,
                "an already-open breakdown clears passives when the local loadout changes");
            previewPlayer.Passives = previewPassives;
            Check(((PlayerBreakdownViewModel)breakdownWindow.DataContext).HasPassives,
                "an already-open breakdown receives newly observed passives");
            var breakdownSurface = (FrameworkElement)breakdownWindow.Content;
            breakdownSurface.Measure(new Size(1020, 560));
            breakdownSurface.Arrange(new Rect(0, 0, 1020, 560));
            breakdownSurface.UpdateLayout();
            if (!string.IsNullOrWhiteSpace(skillPreviewPath))
            {
                var skillBitmap = new RenderTargetBitmap(1020, 560, 96, 96, PixelFormats.Pbgra32);
                skillBitmap.Render(breakdownSurface);
                var skillEncoder = new PngBitmapEncoder();
                skillEncoder.Frames.Add(BitmapFrame.Create(skillBitmap));
                using FileStream skillOutput = File.Create(skillPreviewPath);
                skillEncoder.Save(skillOutput);
            }
            if (!string.IsNullOrWhiteSpace(passivePreviewPath))
            {
                var passivesTab = (System.Windows.Controls.RadioButton)breakdownWindow.FindName("PassivesTab")!;
                passivesTab.IsChecked = true;
                var passivesPanel = (FrameworkElement)breakdownWindow.FindName("PassivesPanel")!;
                Check(passivesPanel.Visibility == Visibility.Visible,
                    "the passive tab opens its own selected-skill pane");
                breakdownSurface.UpdateLayout();
                var passiveBitmap = new RenderTargetBitmap(1020, 560, 96, 96, PixelFormats.Pbgra32);
                passiveBitmap.Render(breakdownSurface);
                var passiveEncoder = new PngBitmapEncoder();
                passiveEncoder.Frames.Add(BitmapFrame.Create(passiveBitmap));
                using FileStream passiveOutput = File.Create(passivePreviewPath);
                passiveEncoder.Save(passiveOutput);
            }
            breakdownWindow.Close();
        }
        var window = new MainWindow { Width = 560, Height = 218 };
        MethodInfo applyWindowFillOpacity = typeof(MainWindow).GetMethod(
            "ApplyWindowFillOpacity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Brush)window.Resources["PanelBackground"]).Freeze();
        applyWindowFillOpacity.Invoke(window, [window]);
        applyWindowFillOpacity.Invoke(window, [window]);
        Check(window.Resources["PanelBackground"] is Brush panelBrush &&
              Math.Abs(panelBrush.Opacity - window.WindowOpacityPercent / 100) < 0.001 &&
              window.Resources["PartyPanelBackground"] is Brush partyBrush &&
              Math.Abs(partyBrush.Opacity - window.WindowOpacityPercent / 100) < 0.001,
            "startup opacity updates sealed WPF style brushes on repeated calls");
        var meter = (MeterViewModel)window.DataContext;
        meter.ShowSelfCommand.Execute(null);
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_DEBUFF_PREVIEW") is { Length: > 0 } debuffPreviewPath)
        {
            meter.SyncDebuffs(partyEffects);
            var debuffIcons = new StackPanel
            {
                Width = 40,
                Orientation = Orientation.Vertical
            };
            foreach (DebuffTimerViewModel row in meter.TargetDebuffs)
            {
                debuffIcons.Children.Add(new ContentPresenter
                {
                    Content = row,
                    ContentTemplate = (DataTemplate)window.FindResource("DebuffIconTemplate")
                });
            }
            debuffIcons.Measure(new Size(40, 80));
            debuffIcons.Arrange(new Rect(0, 0, 40, 80));
            debuffIcons.UpdateLayout();
            var debuffBitmap = new RenderTargetBitmap(40, 80, 96, 96, PixelFormats.Pbgra32);
            debuffBitmap.Render(debuffIcons);
            var debuffEncoder = new PngBitmapEncoder();
            debuffEncoder.Frames.Add(BitmapFrame.Create(debuffBitmap));
            using FileStream debuffOutput = File.Create(debuffPreviewPath);
            debuffEncoder.Save(debuffOutput);
            meter.SyncDebuffs([]);
        }
        for (int i = 1; i <= 6; i++)
            meter.Players.Add(new PlayerRowViewModel { PlayerId = i });
        window.UpdateMeterHeight();
        double expandedHeight = window.Height;
        meter.Players.Clear();
        window.UpdateMeterHeight();
        Check(expandedHeight > window.Height && window.Height <= 218,
            "the actual meter window grows for six rows and shrinks after they leave");
        var bossPreview = new MeterSnapshot(
            "Kromede's Desire", TargetFrameKind.Legend, 72, 7_200_000, 10_000_000,
            90_000, 0, 0, [], false, 100, 0, null, null, JobClass.Unknown,
            new EnrageTimerView(true, false, "Enrage 03:05", EnrageTimerTone.OnTrack));
        meter.ApplySnapshot(bossPreview);
        Check(meter.IsLocalIdentityPending && meter.IsSelfView &&
              (meter.StatusText.Contains("RELOG", StringComparison.OrdinalIgnoreCase) ||
               meter.StatusText.Contains("ПЕРЕЗАХОД", StringComparison.OrdinalIgnoreCase)) &&
              (meter.EmptyStateText.Contains("RELOG", StringComparison.OrdinalIgnoreCase) ||
               meter.EmptyStateText.Contains("ПЕРЕЗАЙДИТЕ", StringComparison.OrdinalIgnoreCase)) &&
              meter.TargetHpPercent == 0 && !meter.EnrageVisible,
            "missing identity requests a fresh character login and hides unbound target data");
        meter.ApplySnapshot(bossPreview with
        {
            TotalDps = 3000,
            TotalDamage = 30_000,
            Players =
            [
                new MeterPlayerSnapshot(123, "Lunari", JobClass.Sorcerer, 1200, 12_000, 40, 0, 10, 0, []),
                new MeterPlayerSnapshot(456, "Ally", JobClass.Gladiator, 1800, 18_000, 60, 0, 12, 0, [])
            ]
        });
        Check(meter.IsLocalIdentityPending && meter.Players.Count == 0 &&
              !meter.HasPlayers && meter.TotalDpsText == "0" && meter.BattleTime == "00:00" &&
              meter.TargetName != "Kromede's Desire",
            "unbound combat participants, totals, and target stay hidden until relog");
        meter.ApplySnapshot(bossPreview with
        {
            LocalPlayerId = 123,
            LocalPlayerName = "Lunari",
            Players =
            [
                new MeterPlayerSnapshot(123, "Lunari", JobClass.Sorcerer, 1200, 12_000, 40, 0, 10, 0, []),
                new MeterPlayerSnapshot(456, "Ally", JobClass.Gladiator, 1800, 18_000, 60, 0, 12, 0, [])
            ]
        });
        Check(!meter.IsLocalIdentityPending && meter.IsSelfView &&
              meter.Players.Count == 1 && meter.Players[0].IsLocalPlayer,
            "authoritative local identity restores the saved self-only view automatically");
        Check(meter.TargetBarBrush is LinearGradientBrush bossHpBrush &&
              bossHpBrush.GradientStops[0].Color.R > bossHpBrush.GradientStops[0].Color.G,
            "boss HP remains red; poison status is not a frame color");
        meter.ApplySnapshot(bossPreview with { TargetFrame = TargetFrameKind.Normal });
        Check(meter.TargetBarBrush is LinearGradientBrush normalHpBrush &&
              normalHpBrush.GradientStops[0].Color.R > normalHpBrush.GradientStops[0].Color.G,
            "ordinary target HP retains the red-magenta fill");
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_HP_PREVIEW") is { Length: > 0 } hpPreviewPrefix)
        {
            foreach ((TargetFrameKind frameKind, string suffix, double hp) in new[]
                     {
                         (TargetFrameKind.Legend, "boss", 72d),
                         (TargetFrameKind.Normal, "mob", 72d),
                         (TargetFrameKind.Legend, "boss-full", 100d),
                         (TargetFrameKind.Normal, "mob-full", 100d),
                         (TargetFrameKind.Legend, "boss-empty", 0d),
                         (TargetFrameKind.Normal, "mob-empty", 0d)
                     })
            {
                meter.ApplySnapshot(bossPreview with { TargetFrame = frameKind, TargetHpPercent = hp });
                meter.ApplyNotification([], bossPreview.EnrageTimer);
                var surface = (FrameworkElement)window.Content;
                surface.Measure(new Size(560, 218));
                surface.Arrange(new Rect(0, 0, 560, 218));
                surface.UpdateLayout();
                var bitmap = new RenderTargetBitmap(560, 218, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(surface);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream output = File.Create($"{hpPreviewPrefix}-{suffix}.png");
                encoder.Save(output);

                ((FrameworkElement)window.FindName("CompactLayout")).Visibility = Visibility.Collapsed;
                ((FrameworkElement)window.FindName("DetailedLayout")).Visibility = Visibility.Visible;
                surface.Measure(new Size(600, 476));
                surface.Arrange(new Rect(0, 0, 600, 476));
                surface.UpdateLayout();
                var detailedBitmap = new RenderTargetBitmap(600, 476, 96, 96, PixelFormats.Pbgra32);
                detailedBitmap.Render(surface);
                var detailedEncoder = new PngBitmapEncoder();
                detailedEncoder.Frames.Add(BitmapFrame.Create(detailedBitmap));
                using FileStream detailedOutput = File.Create($"{hpPreviewPrefix}-{suffix}-detailed.png");
                detailedEncoder.Save(detailedOutput);
                ((FrameworkElement)window.FindName("DetailedLayout")).Visibility = Visibility.Collapsed;
                ((FrameworkElement)window.FindName("CompactLayout")).Visibility = Visibility.Visible;
            }
        }
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_CLASS_PREVIEW") is { Length: > 0 } classPreviewPath)
        {
            meter.Players.Clear();
            string[] classNames = ["Templar", "Gladiator", "Ranger", "Sorcerer", "Cleric"];
            Brush[] classColors =
            [
                new SolidColorBrush(Color.FromRgb(52, 110, 166)),
                new SolidColorBrush(Color.FromRgb(204, 121, 62)),
                new SolidColorBrush(Color.FromRgb(70, 151, 116)),
                new SolidColorBrush(Color.FromRgb(145, 80, 155)),
                new SolidColorBrush(Color.FromRgb(172, 158, 73))
            ];
            for (int index = 0; index < classNames.Length; index++)
            {
                meter.Players.Add(new PlayerRowViewModel
                {
                    Rank = index + 1,
                    Nickname = classNames[index],
                    IconPath = Path.Combine(AppContext.BaseDirectory, "data", "textures", "classes", classNames[index] + ".png"),
                    AccentBrush = classColors[index],
                    BarPercent = 100 - index * 15,
                    DpsText = "12,4k/c",
                    DamageText = "1,24m",
                    ShareText = "20%"
                });
            }
            window.UpdateMeterHeight();
            var surface = (FrameworkElement)window.Content;
            surface.Measure(new Size(560, 440));
            surface.Arrange(new Rect(0, 0, 560, 440));
            surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(560, 440, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream output = File.Create(classPreviewPath);
            encoder.Save(output);
            meter.Players.Clear();
            window.UpdateMeterHeight();
        }
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_PARTY_PREVIEW") is { Length: > 0 } partyPreviewPath)
        {
            meter.UsePartyRowStyle = true;
            meter.ShowPartyCommand.Execute(null);
            meter.ApplySnapshot(bossPreview with
            {
                LocalPlayerId = 123,
                LocalPlayerName = "Lunari",
                TotalDps = 349_000,
                TotalDamage = 4_200_000,
                Players =
                [
                    new MeterPlayerSnapshot(123, "Lunari", JobClass.Templar, 100_000, 1_400_000, 33.3, 0, 100, 0, []),
                    new MeterPlayerSnapshot(456, "Piska", JobClass.Gladiator, 85_000, 1_200_000, 28.6, 0, 80, 0, []),
                    new MeterPlayerSnapshot(789, "purppl", JobClass.Ranger, 70_000, 1_000_000, 23.8, 0, 70, 0, []),
                    new MeterPlayerSnapshot(987, "Heal", JobClass.Sorcerer, 55_000, 400_000, 9.5, 0, 50, 0, []),
                    new MeterPlayerSnapshot(654, "skurwincyn", JobClass.Cleric, 39_000, 200_000, 4.8, 0, 40, 0, [])
                ]
            });
            Check(meter.Players.Count == 5 &&
                  meter.Players.All(player => player.PartyIconPath is { } path && File.Exists(path)),
                "party-style rows show all participants with their class icons");
            var surface = (FrameworkElement)window.Content;
            surface.Measure(new Size(560, 374));
            surface.Arrange(new Rect(0, 0, 560, 374));
            surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(560, 374, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream output = File.Create(partyPreviewPath)) encoder.Save(output);

            ((FrameworkElement)window.FindName("CompactLayout")).Visibility = Visibility.Collapsed;
            ((FrameworkElement)window.FindName("DetailedLayout")).Visibility = Visibility.Visible;
            surface.Measure(new Size(600, 476));
            surface.Arrange(new Rect(0, 0, 600, 476));
            surface.UpdateLayout();
            var detailedBitmap = new RenderTargetBitmap(600, 476, 96, 96, PixelFormats.Pbgra32);
            detailedBitmap.Render(surface);
            var detailedEncoder = new PngBitmapEncoder();
            detailedEncoder.Frames.Add(BitmapFrame.Create(detailedBitmap));
            using (FileStream output = File.Create(Path.ChangeExtension(partyPreviewPath, ".detailed.png")))
                detailedEncoder.Save(output);
            meter.Players.Clear();
            meter.UsePartyRowStyle = false;
            meter.ShowSelfCommand.Execute(null);
            ((FrameworkElement)window.FindName("DetailedLayout")).Visibility = Visibility.Collapsed;
            ((FrameworkElement)window.FindName("CompactLayout")).Visibility = Visibility.Visible;
        }
        if (Environment.GetEnvironmentVariable("AION2METER_RENDER_LAYOUT_PREVIEW") is { Length: > 0 } layoutPreviewPath)
        {
            meter.ApplyNotification(
                [], new EnrageTimerView(true, false, "Enrage 03:14 · kill ~02:08", EnrageTimerTone.OnTrack));
            var surface = (FrameworkElement)window.Content;
            surface.Measure(new Size(560, 218));
            surface.Arrange(new Rect(0, 0, 560, 218));
            surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(560, 218, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream output = File.Create(layoutPreviewPath);
            encoder.Save(output);
            if (Environment.GetEnvironmentVariable("AION2METER_RENDER_EXPANDED_PREVIEW") is { Length: > 0 } expandedPreviewPath)
            {
                ((FrameworkElement)window.FindName("CompactLayout")).Visibility = Visibility.Collapsed;
                ((FrameworkElement)window.FindName("DetailedLayout")).Visibility = Visibility.Visible;
                surface.Measure(new Size(600, 476));
                surface.Arrange(new Rect(0, 0, 600, 476));
                surface.UpdateLayout();
                var expandedBitmap = new RenderTargetBitmap(600, 476, 96, 96, PixelFormats.Pbgra32);
                expandedBitmap.Render(surface);
                var expandedEncoder = new PngBitmapEncoder();
                expandedEncoder.Frames.Add(BitmapFrame.Create(expandedBitmap));
                using FileStream expandedOutput = File.Create(expandedPreviewPath);
                expandedEncoder.Save(expandedOutput);
            }
        }
    }
    catch (Exception exception)
    {
        notificationFailure = exception;
    }
});
uiThread.SetApartmentState(ApartmentState.STA);
uiThread.Start();
uiThread.Join();
if (notificationFailure is not null)
    throw notificationFailure;

string mutexName = $@"Local\Aion2Meter.Smoke.{Guid.NewGuid():N}";
using (var primaryGate = new SingleInstanceGate(mutexName))
{
    Check(primaryGate.IsPrimary, "first process owns the instance gate");
    using var duplicateGate = new SingleInstanceGate(mutexName);
    Check(!duplicateGate.IsPrimary, "second process cannot start another meter");
}
using (var restarted = new SingleInstanceGate(mutexName))
    Check(restarted.IsPrimary, "instance gate is released on exit");

var repository = new DataRepository(NullLogger<DataRepository>.Instance);
var deviceLockParser = new DpsPacketParser(NullLogger<DpsPacketParser>.Instance, repository);
MethodInfo parseDamage = typeof(DpsPacketParser).GetMethod("TryParseDamagePacket", BindingFlags.Instance | BindingFlags.NonPublic)!;
FieldInfo currentTargetField = typeof(DpsPacketParser).GetField("_currentTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
Check((bool)parseDamage.Invoke(deviceLockParser, [new byte[] { 8, 4, 56, 0xC1, 0x84, 0x3D }])! &&
      (int)currentTargetField.GetValue(deviceLockParser)! == 0,
    "an impossible combat target ID cannot replace the current target");
FieldInfo lockedDeviceField = typeof(DpsPacketParser).GetField("_lockedDeviceName", BindingFlags.Instance | BindingFlags.NonPublic)!;
FieldInfo lockTickField = typeof(DpsPacketParser).GetField("_lastLockedStreamPacketMs", BindingFlags.Instance | BindingFlags.NonPublic)!;
lockedDeviceField.SetValue(deviceLockParser, "old-adapter");
lockTickField.SetValue(deviceLockParser, Environment.TickCount64);
deviceLockParser.ProcessPayload(new CapturedPayload(13328, 50123, [0], "new-adapter"));
Check(deviceLockParser.LockedDeviceName == "old-adapter",
    "a recent game-stream lock ignores another adapter");
lockTickField.SetValue(deviceLockParser, Environment.TickCount64 - 31_000);
deviceLockParser.ProcessPayload(new CapturedPayload(13328, 50123, [0], "new-adapter"));
Check(deviceLockParser.LockedDeviceName is null,
    "an idle game-stream lock permits capture after an adapter switch");
var schedule = new EventScheduleService(
    repository,
    Path.Combine(AppContext.BaseDirectory, "data", "event_schedules.json"));
MeterEventScheduleSnapshot beforeInvasion = schedule.GetSnapshotAt(
    new DateTimeOffset(2026, 10, 20, 16, 22, 0, TimeSpan.Zero));
Check(beforeInvasion.Alerts.Count == 1, "one imminent event produces one alert");
Check(beforeInvasion.Alerts.Select(alert => alert.Key).Distinct(StringComparer.Ordinal).Count() == 1,
    "event alert keys are unique");
MeterEventScheduleSnapshot duringInvasion = schedule.GetSnapshotAt(
    new DateTimeOffset(2026, 10, 20, 16, 31, 0, TimeSpan.Zero));
Check(duringInvasion.Alerts.Count == 1 && duringInvasion.Alerts[0].Countdown is "NOW" or "СЕЙЧАС",
    "the active event remains a single alert");

string bossCachePath = Path.Combine(Path.GetTempPath(), $"Aion2Meter.Boss.{Guid.NewGuid():N}.json");
try
{
    var bossTimer = new FieldBossTimerService(bossCachePath);
    DateTimeOffset bossNow = DateTimeOffset.UtcNow;
    long bossDue = bossNow.AddHours(2).ToUnixTimeMilliseconds();
    var bossPacket = new FieldBossListEvent(
        FieldBossTimerService.GartuaMapId,
        [new FieldBossEntry(FieldBossTimerService.GartuaRowId, false, null, bossDue)],
        bossNow.ToUnixTimeMilliseconds());
    Check(!bossTimer.Record(bossPacket, null), "map timer without a server cannot leak to another server");
    Check(bossTimer.Record(bossPacket, 2301), "Gartua map packet is accepted");
    Check(bossTimer.GetSnapshotAt(bossNow, 2301) is { Alert: null },
        "Gartua appears in the event list but not the ticker two hours early");
    Check(bossTimer.GetSnapshotAt(bossNow, 2302) is null,
        "Gartua timer is isolated to the current game server");
    Check(bossTimer.GetSnapshotAt(bossNow.AddHours(1).AddMinutes(55), 2301) is { Alert: not null },
        "Gartua enters the ticker ten minutes before spawn");
    Check(bossTimer.GetSnapshotAt(bossNow.AddHours(2).AddMinutes(1), 2301) is { Alert: not null },
        "expected spawn remains visible briefly without claiming a confirmed sighting");
    Check(bossTimer.GetSnapshotAt(bossNow.AddHours(2).AddMinutes(4), 2301) is null,
        "an old map timer expires instead of claiming the boss is still present");
    Check(new FieldBossTimerService(bossCachePath).GetSnapshotAt(bossNow, 2301) is not null,
        "Gartua map timer survives a meter restart");
}
finally
{
    if (File.Exists(bossCachePath)) File.Delete(bossCachePath);
    if (File.Exists(bossCachePath + ".tmp")) File.Delete(bossCachePath + ".tmp");
}

string settingsDirectory = Path.Combine(Path.GetTempPath(), $"Aion2Meter.Smoke.{Guid.NewGuid():N}");
Directory.CreateDirectory(settingsDirectory);
string settingsPath = Path.Combine(settingsDirectory, "config.json");
try
{
    var store = new MeterSettingsStore(settingsPath);
    Check(store.Save(new MeterSettings
    {
        Language = "ru",
        CompactWidth = 444,
        CompactHeight = 333,
        HasCustomCompactSize = true,
        ShowPartyMembers = true,
        ShowOtherPlayersDebuffs = true,
        UsePartyRowStyle = true,
        KeepNotificationsWhenHidden = true,
        CheckUpdatesOnStartup = false
    }), "settings file can be written");
    MeterSettings restored = store.Load();
    Check(restored.Language == "ru" && restored.CompactWidth == 444 && restored.CompactHeight == 333,
        $"window preferences survive restart (actual {restored.Language}, {restored.CompactWidth}, {restored.CompactHeight})");
    Check(restored.ShowPartyMembers && restored.KeepNotificationsWhenHidden && restored.ShowOtherPlayersDebuffs &&
          restored.UsePartyRowStyle,
        "toggle preferences survive restart");
    Check(!restored.CheckUpdatesOnStartup, "disabled startup update checks survive restart");
    Check(store.Save(new MeterSettings { Language = "en", CompactWidth = 500 }),
        "a second settings save succeeds atomically");
    Check(File.Exists(settingsPath + ".bak"), "a previous valid settings file is retained as backup");
    File.WriteAllText(settingsPath, "{broken json");
    MeterSettings recovered = store.Load();
    Check(store.LastLoadError is not null && recovered.Language == "ru" && recovered.CompactWidth == 444,
        "damaged settings recover the last valid backup");
    Check(store.Save(recovered) && File.Exists(settingsPath + ".bak"),
        "saving after recovery preserves the valid backup");
    File.Delete(settingsPath);
    MeterSettings recoveredFromMissingPrimary = store.Load();
    Check(store.LastLoadError is FileNotFoundException &&
          recoveredFromMissingPrimary.Language == "ru" && recoveredFromMissingPrimary.CompactWidth == 444,
        "a missing primary settings file also recovers the valid backup");
    File.WriteAllText(settingsPath, """{"Version":5,"CompactWidth":444,"CompactHeight":438,"HasCustomCompactSize":true}""");
    MeterSettings migrated = store.Load();
    Check(migrated.Version == 7 && migrated.CompactWidth == 444 && migrated.CompactHeight == 218 &&
          migrated.CheckUpdatesOnStartup,
        "old auto-expanded height is migrated back to the one-row baseline");
    File.Delete(settingsPath);
    File.Delete(settingsPath + ".bak");
    Check(store.Load().CheckUpdatesOnStartup,
        "first installation enables startup update checks by default");
}
finally
{
    if (File.Exists(settingsPath)) File.Delete(settingsPath);
    if (File.Exists(settingsPath + ".bak")) File.Delete(settingsPath + ".bak");
    if (File.Exists(settingsPath + ".tmp")) File.Delete(settingsPath + ".tmp");
    Directory.Delete(settingsDirectory);
}

string diagnosticsDirectory = Path.Combine(Path.GetTempPath(), $"Aion2Meter.Diagnostics.Smoke.{Guid.NewGuid():N}");
string? diagnosticPath = null;
try
{
    using (var diagnostics = new DiagnosticLog(diagnosticsDirectory, maximumFileBytes: 512))
    {
        diagnosticPath = diagnostics.FilePath;
        var logger = new DiagnosticLogger<NpcapCaptureService>(diagnostics, LogLevel.Warning);
        logger.LogInformation("ordinary capture status");
        logger.LogWarning("capture queue dropped {Count} packets", 2);
        diagnostics.Write("TEST", new string('x', 1024));
        diagnostics.Write("TEST", "after limit");
        diagnostics.Write("TEST", "SECOND_AFTER_LIMIT");
    }

    string contents = File.ReadAllText(diagnosticPath!);
    string[] continuationFiles = Directory.GetFiles(diagnosticsDirectory, "hp-debug-*-part02.log");
    string continuation = continuationFiles.Length == 1 ? File.ReadAllText(continuationFiles[0]) : string.Empty;
    Check(!contents.Contains("ordinary capture status", StringComparison.Ordinal) &&
          contents.Contains("capture queue dropped 2 packets", StringComparison.Ordinal),
        "capture warnings reach diagnostics without verbose per-packet logs");
    Check(contents.Contains("continuing in", StringComparison.Ordinal) &&
          continuation.Contains("after limit", StringComparison.Ordinal) &&
          continuation.Contains("SECOND_AFTER_LIMIT", StringComparison.Ordinal),
        "diagnostic journal continues in a bounded new file after its size limit");
}
finally
{
    if (Directory.Exists(diagnosticsDirectory))
        foreach (string file in Directory.GetFiles(diagnosticsDirectory)) File.Delete(file);
    if (Directory.Exists(diagnosticsDirectory)) Directory.Delete(diagnosticsDirectory);
}

Console.WriteLine($"Aion 2 DPS smoke checks: {passed} passed.");

void Sync(IReadOnlyList<Snapshot> snapshots) => KeyedCollectionSynchronizer.Reconcile(
    rows,
    snapshots,
    snapshot => snapshot.Key,
    row => row.Key,
    snapshot => new Row(snapshot.Key),
    (row, snapshot) => row.Value = snapshot.Value,
    StringComparer.Ordinal);

void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException($"Failed: {name}");

    Interlocked.Increment(ref passed);
}

internal sealed record Snapshot(string Key, int Value);

internal sealed class Row(string key)
{
    public string Key { get; } = key;
    public int Value { get; set; }
}
