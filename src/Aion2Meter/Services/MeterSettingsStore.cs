using System;
using System.IO;
using System.Text.Json;
using AionFlex.Core.Overlay;

namespace Aion2Meter.Services;

internal sealed class MeterSettings
{
    public int Version { get; set; } = 4;
    public string Language { get; set; } = "en";
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 560;
    public double Height { get; set; } = 510;
    public bool IsCompact { get; set; } = true;
    public double CompactWidth { get; set; } = 560;
    public double CompactHeight { get; set; } = 248;
    public double ExpandedWidth { get; set; } = 600;
    public double ExpandedHeight { get; set; } = 510;
    public bool HasCustomCompactSize { get; set; }
    public bool HasCustomExpandedSize { get; set; }
    public int OpacityLevel { get; set; }
    public double WindowOpacity { get; set; } = 1;
    public double TextOpacity { get; set; } = 1;
    public bool BossOnly { get; set; } = true;
    public bool ShowPartyMembers { get; set; }
    public bool ShowTargetDebuffs { get; set; } = true;
    public MeterDisplayMode DisplayMode { get; set; } = MeterDisplayMode.Damage;
}

internal sealed class MeterSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public MeterSettingsStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aion2Meter");
        FilePath = Path.Combine(directory, "config.json");
    }

    public string FilePath { get; }

    public MeterSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new MeterSettings();
            }

            string json = File.ReadAllText(FilePath);
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

            settings.Language = string.Equals(settings.Language, "ru", StringComparison.OrdinalIgnoreCase)
                ? "ru"
                : "en";
            settings.WindowOpacity = Math.Clamp(settings.WindowOpacity, 0.25, 1);
            settings.TextOpacity = Math.Clamp(settings.TextOpacity, 0.25, 1);

            if (!Enum.IsDefined(settings.DisplayMode))
            {
                settings.DisplayMode = MeterDisplayMode.Damage;
            }

            return settings;
        }
        catch
        {
            return new MeterSettings();
        }
    }

    public void Save(MeterSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch
        {
            // A read-only profile must not prevent the meter from closing.
        }
    }
}
