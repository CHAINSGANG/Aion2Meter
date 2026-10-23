using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Aion2Meter.Services;

internal static class UiLanguageService
{
    private static readonly IReadOnlyDictionary<string, (string English, string Russian)> Texts =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["CombatAnalysis"] = ("Aion 2 DPS", "Aion 2 DPS"),
            ["Close"] = ("Close", "Закрыть"),
            ["Settings"] = ("Settings and tools", "Настройки и инструменты"),
            ["Opacity"] = ("Three opacity levels", "Три уровня прозрачности"),
            ["Compact"] = ("Compact mode", "Компактный режим"),
            ["Expand"] = ("Expand", "Развернуть"),
            ["MinimizeTray"] = ("Hide to tray", "Свернуть в трей"),
            ["Show"] = ("Show meter", "Показать метр"),
            ["Hide"] = ("Hide meter", "Скрыть метр"),
            ["Exit"] = ("Exit", "Выход"),
            ["TrayText"] = ("Aion II Meter", "Aion II Meter"),
            ["UpdateAvailable"] = ("Aion 2 DPS {0} is available. Open updates to install it.", "Доступна версия Aion 2 DPS {0}. Откройте обновления для установки."),
            ["UpdateAvailableManual"] = ("Aion 2 DPS {0} is available. Click to open the release.", "Доступна версия Aion 2 DPS {0}. Нажмите, чтобы открыть релиз."),
            ["UpdateTitle"] = ("Update available", "Доступно обновление"),
            ["UpdateCurrent"] = ("You already have the latest version ({0}).", "У вас уже последняя версия ({0})."),
            ["UpdateFailed"] = ("Could not check for updates: {0}", "Не удалось проверить обновления: {0}"),
            ["CheckingUpdates"] = ("Checking for updates…", "Проверяем обновления…"),
            ["English"] = ("English", "English"),
            ["Russian"] = ("Russian", "Русский"),
            ["Language"] = ("LANGUAGE", "ЯЗЫК"),
            ["LanguageHint"] = ("Interface language. English is used by default.", "Язык интерфейса. По умолчанию используется английский."),
            ["Tools"] = ("Tools", "Инструменты"),
            ["ToolsTitle"] = ("Aion II Meter · Tools", "Aion II Meter · Управление"),
            ["ToolsHeading"] = ("OVERLAY CONTROLS", "УПРАВЛЕНИЕ"),
            ["WorkingFeatures"] = ("Overlay controls", "Рабочие функции оверлея"),
            ["WorkingFeaturesHint"] = ("Only features already connected to the meter are shown here.", "Здесь находятся только действия, которые уже подключены к нашему метру."),
            ["Display"] = ("DISPLAY", "ОТОБРАЖЕНИЕ"),
            ["PartyRowStyle"] = ("Party-style damage rows", "Строки урона в стиле группы"),
            ["PartyRowStyleHint"] = ("Class icon, rank, name, DPS, total damage and share — without bars.", "Иконка класса, место, имя, DPS, общий урон и доля — без полосок."),
            ["StatisticsType"] = ("STATISTICS TYPE", "ТИП СТАТИСТИКИ"),
            ["CharacterData"] = ("CHARACTER DATA", "ДАННЫЕ ПЕРСОНАЖА"),
            ["Combat"] = ("COMBAT", "БОЙ"),
            ["ShowOtherPlayersDebuffs"] = ("Show debuffs from other players", "Показывать дебафы других игроков"),
            ["ShowOtherPlayersDebuffsHint"] = ("Adds effects applied to the current target by known players. Off: show only your own effects.", "Добавляет на текущей цели эффекты известных игроков. Выкл.: только ваши эффекты."),
            ["Notifications"] = ("NOTIFICATIONS", "УВЕДОМЛЕНИЯ"),
            ["KeepNotificationsWhenHidden"] = ("Show alerts while the meter is hidden", "Показывать уведомления, когда метр скрыт"),
            ["KeepNotificationsWhenHiddenHint"] = ("Off: alerts follow the meter into the tray or when the game is minimized. On: alerts stay on screen.", "Выкл.: уведомления скрываются вместе с метром или игрой. Вкл.: остаются на экране."),
            ["Updates"] = ("UPDATES", "ОБНОВЛЕНИЯ"),
            ["CheckUpdates"] = ("Check for updates", "Проверить обновления"),
            ["CheckUpdatesHint"] = ("Compare this build with the latest GitHub release.", "Сравнить эту сборку с последним релизом на GitHub."),
            ["AutoCheckUpdates"] = ("Check for updates when the meter starts", "Проверять обновления при запуске метра"),
            ["AutoCheckUpdatesHint"] = ("Enabled by default. Updates are never installed without your confirmation.", "Включено по умолчанию. Обновления не устанавливаются без вашего подтверждения."),
            ["InstallUpdate"] = ("Download and install update", "Скачать и установить обновление"),
            ["InstallUpdateHint"] = ("Verify the GitHub release and update this installation.", "Проверить релиз GitHub и обновить эту установку."),
            ["UpdateInstallConfirm"] = ("Download Aion 2 DPS {0} from GitHub and install it to:\n{1}\n\nThe meter will close after you approve the Windows installer. Continue?", "Скачать Aion 2 DPS {0} с GitHub и установить в:\n{1}\n\nПосле подтверждения установщика Windows метр закроется. Продолжить?"),
            ["UpdateDownloading"] = ("Downloading update: {0}%", "Скачивание обновления: {0}%"),
            ["UpdateVerifying"] = ("Verifying update…", "Проверяем обновление…"),
            ["UpdateStarting"] = ("Starting the installer…", "Запускаем установщик…"),
            ["UpdateCancelled"] = ("Update cancelled; the meter is still running.", "Обновление отменено; метр продолжает работать."),
            ["UpdateInstallFailed"] = ("Could not install update: {0}", "Не удалось установить обновление: {0}"),
            ["NextModules"] = ("NEXT MODULES", "СЛЕДУЮЩИЕ МОДУЛИ"),
            ["HistoryTraining"] = ("History / training", "История / тренировка"),
            ["HistoryTrainingHint"] = ("Save encounters and compare training-dummy attempts.", "Сохранение боёв и сравнение попыток на манекене."),
            ["LayoutChanged"] = ("Overlay size changed", "Размер оверлея переключён"),
            ["OpacityChanged"] = ("Opacity level changed", "Уровень прозрачности переключён"),
            ["SummaryCopied"] = ("Combat summary copied", "Сводка боя скопирована в буфер обмена"),
            ["ScreenshotCopied"] = ("Meter screenshot copied", "Снимок DPS-окна скопирован в буфер обмена"),
            ["PetsOpened"] = ("Pets opened", "Открыта страница питомцев"),
            ["PartyOpened"] = ("Party check opened", "Открыта проверка состава группы"),
            ["TimersOpened"] = ("Event calendar opened", "Открыт календарь игровых событий"),
            ["LanguageChanged"] = ("Language changed", "Язык переключён"),
            ["BossAll"] = ("Boss / all targets", "Босс / все цели"),
            ["Now"] = ("Now: ", "Сейчас: "),
            ["SelfOnly"] = ("Only me", "Только я"),
            ["SelfOnlyHint"] = ("Keep only the active character row.", "Оставить строку текущего персонажа."),
            ["WholeParty"] = ("Whole party", "Вся группа"),
            ["WholePartyHint"] = ("Show all detected party members.", "Показать всех найденных участников."),
            ["WindowSize"] = ("Window size", "Размер окна"),
            ["WindowSizeHint"] = ("Compact / detailed mode.", "Компактный / подробный режим."),
            ["OpacityTitle"] = ("Opacity", "Прозрачность"),
            ["OpacityHint"] = ("Cycle through three opacity levels.", "Переключить один из трёх уровней."),
            ["WindowOpacity"] = ("Window opacity", "Прозрачность окна"),
            ["TextOpacity"] = ("Text opacity", "Прозрачность текста"),
            ["DamageDealt"] = ("Damage dealt", "Нанесённый урон"),
            ["DamageDealtHint"] = ("Regular DPS against the boss or all targets.", "Обычный DPS по боссу или всем целям."),
            ["Healing"] = ("Healing", "Лечение"),
            ["HealingHint"] = ("HPS, healing amount, share and healer skills.", "HPS, объём лечения, доля и навыки лекарей."),
            ["DamageTaken"] = ("Damage taken", "Полученный урон"),
            ["DamageTakenHint"] = ("Who attacks the character and with which skills.", "Кто атакует персонажа и какими умениями."),
            ["Pets"] = ("Pets", "Питомцы"),
            ["PetsHint"] = ("Collection, families, levels and permanent bonuses.", "Коллекция, семейства, уровни и постоянные бонусы."),
            ["PartyCheck"] = ("Party check", "Проверка группы"),
            ["PartyCheckHint"] = ("Room members, classes, level, GS and combat power.", "Состав комнаты, классы, уровень, GS и боевая мощь."),
            ["EventTimers"] = ("Event timers", "Таймеры событий"),
            ["EventTimersHint"] = ("Rifts, sieges, Abyss bosses, Shugo Festival and invasions.", "Рифты, осады, боссы Бездны, фестиваль шуго и вторжения."),
            ["PauseHint"] = ("Pauses window updates without losing captured packets.", "Пауза касается обновления окна; захват не теряется."),
            ["ResetCombat"] = ("Reset combat", "Сбросить бой"),
            ["ResetCombatTooltip"] = ("Reset damage and the current encounter", "Сбросить урон и текущий бой"),
            ["ResetCombatHint"] = ("Clear the current calculation and target HP.", "Очистить текущий расчёт и HP цели."),
            ["CopySummary"] = ("Copy summary", "Копировать сводку"),
            ["CopySummaryHint"] = ("Text with each player's DPS, damage and share.", "Текст с DPS, уроном и долей каждого игрока."),
            ["CopyScreenshot"] = ("Party screenshot", "Снимок группы"),
            ["CopyScreenshotHint"] = ("Copy the current meter window as an image.", "Копировать текущее DPS-окно как изображение."),
            ["Ready"] = ("READY", "ГОТОВ"),
            ["Connecting"] = ("CONNECTING", "ПОДКЛЮЧЕНИЕ"),
            ["InCombat"] = ("IN COMBAT", "В БОЮ"),
            ["Error"] = ("ERROR", "ОШИБКА"),
            ["Demo"] = ("DEMO", "ДЕМО"),
            ["WaitingCombat"] = ("Waiting for combat", "Ожидание боя"),
            ["TargetUnknown"] = ("Target not detected", "Цель не определена"),
            ["AllTargets"] = ("All targets", "Все цели"),
            ["Boss"] = ("BOSS", "БОСС"),
            ["All"] = ("ALL", "ВСЕ"),
            ["Pause"] = ("PAUSE", "ПАУЗА"),
            ["Resume"] = ("RESUME", "ПРОДОЛЖИТЬ"),
            ["Player"] = ("Player", "Персонаж"),
            ["Enemy"] = ("Enemy", "Противник"),
            ["Unknown"] = ("Unknown", "Неизвестно"),
            ["Damage"] = ("DAMAGE", "УРОН"),
            ["TotalDamage"] = ("TOTAL DAMAGE", "ОБЩИЙ УРОН"),
            ["TotalHealing"] = ("TOTAL HEALING", "ОБЩЕЕ ЛЕЧЕНИЕ"),
            ["Group"] = ("PARTY", "ГРУППА"),
            ["Source"] = ("SOURCE", "ИСТОЧНИК"),
            ["CurrentTarget"] = ("CURRENT TARGET", "ТЕКУЩАЯ ЦЕЛЬ"),
            ["Mode"] = ("MODE", "РЕЖИМ"),
            ["Share"] = ("SHARE", "ДОЛЯ"),
            ["MitigationHint"] = ("Share of incoming attacks mitigated by defence", "Доля входящих атак, на которых сработала защита"),
            ["TargetHealth"] = ("Current target health", "Здоровье текущей цели"),
            ["NoCombatData"] = ("NO COMBAT DATA", "НЕТ ДАННЫХ БОЯ"),
            ["NoCombatHint"] = ("Attack a target — the party will appear automatically", "Атакуйте цель — группа появится автоматически"),
            ["Gladiator"] = ("Gladiator", "Гладиатор"),
            ["Templar"] = ("Templar", "Страж"),
            ["Ranger"] = ("Ranger", "Стрелок"),
            ["Assassin"] = ("Assassin", "Убийца"),
            ["Sorcerer"] = ("Sorcerer", "Волшебник"),
            ["Cleric"] = ("Cleric", "Целитель"),
            ["Elementalist"] = ("Spiritmaster", "Заклинатель"),
            ["Chanter"] = ("Chanter", "Чародей"),
            ["Fighter"] = ("Fighter", "Кулачник")
        };

    private static string _currentLanguage = "en";

    public static string CurrentLanguage => _currentLanguage;
    public static bool IsRussian => string.Equals(_currentLanguage, "ru", StringComparison.OrdinalIgnoreCase);
    public static event EventHandler? LanguageChanged;

    public static void Apply(string? language)
    {
        string normalized = string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        if (string.Equals(_currentLanguage, normalized, StringComparison.Ordinal))
        {
            ApplyCulture(normalized);
            return;
        }

        _currentLanguage = normalized;
        ApplyCulture(normalized);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Text(string key, params object[] arguments)
    {
        string value = Texts.TryGetValue(key, out var pair)
            ? IsRussian ? pair.Russian : pair.English
            : key;
        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    public static string Pick(string english, string russian) => IsRussian ? russian : english;

    private static void ApplyCulture(string language)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(language == "ru" ? "ru-RU" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
