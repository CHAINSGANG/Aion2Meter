using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AionFlex.Core.Overlay;

namespace Aion2Meter.Services;

internal sealed class MeterSettings
{
    public int Version { get; set; } = 6;
    public string Language { get; set; } = "en";
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 560;
    public double Height { get; set; } = 510;
    public bool IsCompact { get; set; } = true;
    public double CompactWidth { get; set; } = 560;
    public double CompactHeight { get; set; } = 218;
    public double ExpandedWidth { get; set; } = 600;
    public double ExpandedHeight { get; set; } = 476;
    public bool HasCustomCompactSize { get; set; }
    public bool HasCustomExpandedSize { get; set; }
    public int OpacityLevel { get; set; }
    public double WindowOpacity { get; set; } = 1;
    public double TextOpacity { get; set; } = 1;
    public bool BossOnly { get; set; } = true;
    public bool ShowPartyMembers { get; set; }
    public bool ShowTargetDebuffs { get; set; } = true;
    public bool PvpMode { get; set; }
    public bool KeepNotificationsWhenHidden { get; set; }
    public MeterDisplayMode DisplayMode { get; set; } = MeterDisplayMode.Damage;
}

internal sealed class MeterSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true
    };

    public MeterSettingsStore() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aion2Meter",
            "config.json"))
    { }

    internal MeterSettingsStore(string filePath) => FilePath = filePath;

    public string FilePath { get; }
    public Exception? LastSaveError { get; private set; }
    public Exception? LastLoadError { get; private set; }

    public MeterSettings Load()
    {
        LastLoadError = null;
        if (!File.Exists(FilePath))
        {
            string backupPath = FilePath + ".bak";
            if (File.Exists(backupPath))
            {
                try
                {
                    MeterSettings backup = LoadFile(backupPath);
                    LastLoadError = new FileNotFoundException("Primary settings file is missing; backup was loaded.", FilePath);
                    return backup;
                }
                catch (Exception exception)
                {
                    LastLoadError = exception;
                }
            }
            return new MeterSettings();
        }

        try
        {
            return LoadFile(FilePath);
        }
        catch (Exception exception)
        {
            LastLoadError = exception;
            string backupPath = FilePath + ".bak";
            if (File.Exists(backupPath))
            {
                try
                {
                    return LoadFile(backupPath);
                }
                catch (Exception backupException)
                {
                    LastLoadError = new AggregateException(exception, backupException);
                }
            }
            return new MeterSettings();
        }
    }

    private static MeterSettings LoadFile(string path)
    {
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        bool hasVersion = document.RootElement.TryGetProperty(nameof(MeterSettings.Version), out _);
        MeterSettings settings = JsonSerializer.Deserialize<MeterSettings>(
            json,
            JsonOptions) ?? new MeterSettings();

        // Migrate the old overlay file, which only had Width/Height.
        if (!hasVersion || settings.Version < 2)
        {
            settings.CompactWidth = settings.Width;
            settings.CompactHeight = settings.Height;
            settings.HasCustomCompactSize = true;
            settings.Version = 2;
        }

        if (settings.Version < 3)
        {
            settings.Language = "en";
            settings.Version = 3;
        }

        if (settings.Version < 4)
        {
            double[] oldLevels = [1.0, 0.72, 0.46];
            settings.WindowOpacity = oldLevels[Math.Clamp(settings.OpacityLevel, 0, oldLevels.Length - 1)];
            settings.TextOpacity = 1;
            settings.Version = 4;
        }

        if (settings.Version < 5)
        {
            if (!settings.HasCustomCompactSize) settings.CompactHeight = 218;
            if (!settings.HasCustomExpandedSize) settings.ExpandedHeight = 476;
            settings.Version = 5;
        }

        if (settings.Version < 6)
        {
            // Older builds accidentally saved auto-expanded combat height as a
            // user preference, preventing the window from shrinking later.
            // Keep the chosen width, but restore the one-row height baseline.
            settings.CompactHeight = Math.Min(settings.CompactHeight, 218);
            settings.Version = 6;
        }

        settings.Language = string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";
        settings.WindowOpacity = double.IsFinite(settings.WindowOpacity)
            ? Math.Clamp(settings.WindowOpacity, 0.25, 1)
            : 1;
        settings.TextOpacity = double.IsFinite(settings.TextOpacity)
            ? Math.Clamp(settings.TextOpacity, 0.25, 1)
            : 1;

        if (!Enum.IsDefined(settings.DisplayMode))
        {
            settings.DisplayMode = MeterDisplayMode.Damage;
        }

        return settings;
    }

    public bool Save(MeterSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IOException("Settings path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            if (File.Exists(FilePath) && LastLoadError is null)
                File.Replace(temporaryPath, FilePath, FilePath + ".bak");
            else
                File.Move(temporaryPath, FilePath, overwrite: true);

            LastSaveError = null;
            LastLoadError = null;
            return true;
        }
        catch (Exception exception)
        {
            // A read-only profile must not prevent the meter from closing, but
            // callers can now report the failure instead of silently losing settings.
            LastSaveError = exception;
            return false;
        }
    }
}
