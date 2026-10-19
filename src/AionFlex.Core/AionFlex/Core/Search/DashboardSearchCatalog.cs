using System.Collections.Generic;

namespace AionFlex.Core.Search;

public static class DashboardSearchCatalog
{
	public static IReadOnlyList<SearchEntry> All { get; } = new List<SearchEntry>
	{
		Area("Start", "The start screen", "start", "home", "dashboard", "main"),
		Area("Fights", "Your saved boss fights", "sessions", "sessions", "pve", "history", "boss", "kills", "report", "dps", "saved"),
		new SearchEntry("PvP fights", "Your saved PvP fights", SearchGroup.Areas, "pvp", new string[6] { "pvp", "open world", "kills", "deaths", "opponents", "abyss" }, DevOnly: false, NeedsPvp: true),
		Area("Training", "Compare skill rotations on the training dummy", "training", "dummy", "scarecrow", "rotation", "compare"),
		Area("Timers", "Rifts, sieges, Abyss bosses, Shugo Festival, Dimensional Invasion", "events", "events", "schedule", "countdown", "spawn"),
		Area("Checklist", "Daily and weekly tasks", "checklist", "daily", "weekly", "tasks", "todo", "entries", "reward count", "keys", "attempts", "odyle", "counters"),
		Area("Roster", "Your characters and which one to farm next", "roster", "characters", "alts", "odyle", "energy", "loot", "farm", "change character", "kina", "money", "gold", "wallet", "abyss points", "nightmare", "ascension trial", "trial", "entries", "tickets"),
		Area("Pets", "Your pets by type and what they add", "pets", "pet", "pets", "mount", "companion", "genus", "cogni", "fera", "natura", "varian"),
		Area("Mechanics", "Boss mechanics and position sheets", "mechanics", "boss", "positions", "sheets", "wipe", "stagger"),
		Area("Settings", "Every setting of the app", "settings", "preferences", "options", "config"),
		Area("Account", "License, Pro, devices", "license", "license", "pro", "subscription", "key", "sign out", "device"),
		new SearchEntry("Logs", "Log files and captures (dev builds)", SearchGroup.Areas, "logs", new string[3] { "debug", "captures", "runs" }, DevOnly: true),
		Setting("Overlay", "overlay", "Theme, row size, transparency, colours", "appearance", "look"),
		Setting("Alerts & sounds", "alerts", "Rift, siege, Abyss bosses, field bosses, sounds", "notifications", "warnings"),
		Setting("On-screen windows", "windows", "Overlay, proc bar, skill timers, Mechanics panel, Party Check, field bosses", "panels", "windows", "show", "hide", "position", "lock", "move"),
		Setting("General", "general", "Language and region", "language", "region"),
		Setting("Fights & sessions", "fights", "Saving boss kills, PvP", "sessions", "save"),
		Setting("Hotkeys", "hotkeys", "Change or turn off the shortcuts", "keyboard", "shortcuts", "keys"),
		Setting("Data & privacy", "data", "Character profile upload", "upload", "privacy", "profile"),
		Setting("About", "about", "Version and what is new", "version", "update", "changelog"),
		Setting("Overlay row size", "overlay", "Normal, Compact or Micro rows", "density", "compact", "micro", "rows", "size"),
		Setting("Overlay transparency", "overlay", "How see-through the overlay is", "opacity", "transparent", "alpha"),
		Setting("Scale bars to the top player", "overlay", "Bar length mode", "bars", "normalize", "leader"),
		Switch("Overlay lock button", "overlay", "overlayLockButton", "Click-through lock on the overlay", "lock", "click through", "padlock"),
		Switch("Hide the overlay out of combat", "overlay", "overlayAutoHide", "Auto-hide after a quiet time", "auto hide", "hide", "combat"),
		Setting("Overlay theme", "overlay", "Legion Banners, Classic, Ghost Prism and the Pro themes", "theme", "skin", "legion", "classic", "prism"),
		Setting("Overlay colours", "overlay", "Class colours, your own palette (Pro)", "colors", "colours", "palette", "class colors"),
		Setting("Overlay font and corners", "overlay", "Corner radius, bar style, font (Pro)", "font", "radius", "corners", "bar style"),
		Setting("What each row shows", "overlay", "Class icon, bar, contribution, DPS, damage, Mit", "class icon", "contribution", "mitigation", "damage", "dps value"),
		AlertSwitch("Spacetime Rift alert", "rift", "riftAlert", "Warning before each rift", "rift", "portal"),
		AlertSwitch("Artifact Siege alert", "siege", "siegeAlert", "Warning before the siege", "siege", "artifact", "corridor"),
		AlertSwitch("Abyss boss alert", "abyssBoss", "abyssBossAlert", "Executors, Watcher Kaira, Guardian Lord Nahma", "abyss", "boss", "executor", "kaira", "nahma"),
		AlertSwitch("Shugo Festival alert", "shugoFesta", "shugoAlert", "Warning before the festival opens", "shugo", "festival", "festa"),
		AlertSwitch("Dimensional Invasion alert", "invasion", "invasionAlert", "Warning before the invasion opens", "invasion", "dimensional", "beritra", "air raid"),
		AlertSwitch("Field boss alerts", "fieldBoss", "fieldBossAlert", "Pick the field bosses to be warned about", "field boss", "respawn", "world boss", "panel"),
		AlertSwitch("Mechanics alerts", "mechanics", "mechanicsAlert", "Stagger gauge, Krao pillars", "stagger", "pillars", "mechanics", "krao"),
		new SearchEntry("Boss enrage alert", "Enrage countdown and its sound", SearchGroup.Settings, DashboardTargets.AlertPage("bossEnrage"), new string[4] { "enrage", "berserk", "frenzy", "timer" }),
		Setting("Alert sounds", "alerts", "A sound for each alert, your own sound files, volume", "sound", "audio", "volume", "chime", "beep", "custom sound", "mp3", "wav"),
		Switch("Proc bar", "windows", "procBar", "Your active proc buffs in a small window", "procs", "buffs", "passives"),
		Switch("Skill timers", "windows", "skillTimers", "Buff and debuff timers of your skills", "skills", "cooldown", "buffs", "debuffs", "dot"),
		Setting("Party Check panel", "windows", "Your Party Dungeon room's members before you go in", "party check", "room", "roster", "members"),
		Setting("Mechanics panel", "windows", "Pin a boss's positions diagram on screen (Pro)", "mechanics panel", "diagram", "positions", "sheet"),
		Setting("Field boss panel", "windows", "Field boss respawn times on screen (Pro, Global)", "field boss panel", "respawn", "timers"),
		Setting("Show or hide the overlay", "windows", "Bring the overlay back or put it away", "overlay", "show overlay", "hide overlay", "lost"),
		Setting("Language", "general", "English, Korean, Chinese", "english", "korean", "chinese", "translation"),
		Setting("Game region", "general", "Taiwan, Korea, Global datacenters", "region", "server", "europe", "taiwan", "korea", "global"),
		Switch("Save boss kills", "fights", "saveBossKills", "Saving fights after a boss kill", "auto save", "sessions", "save"),
		Switch("PvP auto-switch", "fights", "pvpAutoSwitch", "Switch the overlay to PvP in a PvP fight", "pvp", "auto switch"),
		Switch("Upload character profile", "data", "profileUpload", "Your profile on aionflex.gg", "upload", "aionflex.gg", "profile", "website"),
		Setting("Version", "about", "Which version is running", "version", "build", "update"),
		Timer("Spacetime Rift", "Every 3 hours, open 10 minutes", "rift", "portal"),
		Timer("Artifact Siege", "Chaotic Lower Reshanta", "siege", "artifact", "corridor"),
		Timer("Abyss bosses", "Executors, Watcher Kaira, Guardian Lord Nahma", "executor", "argo", "kaira", "nahma", "tamasa", "abyss"),
		Timer("Shugo Festival", "Every hour, entry open 3 minutes", "shugo", "festival", "festa"),
		Timer("Dimensional Invasion", "Every hour at :30, entry open 3 minutes", "invasion", "dimensional", "beritra")
	};

	private static SearchEntry Area(string title, string subtitle, string target, params string[] keywords)
	{
		return new SearchEntry(title, subtitle, SearchGroup.Areas, target, keywords);
	}

	private static SearchEntry Setting(string title, string group, string subtitle, params string[] keywords)
	{
		return new SearchEntry(title, subtitle, SearchGroup.Settings, DashboardTargets.SettingsGroup(group), keywords);
	}

	private static SearchEntry Switch(string title, string group, string toggle, string subtitle, params string[] keywords)
	{
		return new SearchEntry(title, subtitle, SearchGroup.Settings, DashboardTargets.SettingsGroup(group), keywords, DevOnly: false, NeedsPvp: false, toggle);
	}

	private static SearchEntry AlertSwitch(string title, string key, string toggle, string subtitle, params string[] keywords)
	{
		return new SearchEntry(title, subtitle, SearchGroup.Settings, DashboardTargets.AlertPage(key), keywords, DevOnly: false, NeedsPvp: false, toggle);
	}

	private static SearchEntry Timer(string title, string subtitle, params string[] keywords)
	{
		return new SearchEntry(title, subtitle, SearchGroup.Timers, "events", keywords);
	}
}
