using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace Aion2Meter.Services;

[MarkupExtensionReturnType(typeof(string))]
internal sealed class LocalizedTextExtension : MarkupExtension
{
    public LocalizedTextExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = LocalizedTextSource.Instance,
            Mode = BindingMode.OneWay
        }.ProvideValue(serviceProvider);
}

internal sealed class LocalizedTextSource : INotifyPropertyChanged
{
    public static LocalizedTextSource Instance { get; } = new();

    private LocalizedTextSource() => UiLanguageService.LanguageChanged += (_, _) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public string this[string key] => UiLanguageService.Text(key);
    public event PropertyChangedEventHandler? PropertyChanged;
}

[MarkupExtensionReturnType(typeof(string))]
internal sealed class BilingualTextExtension : MarkupExtension
{
    public string English { get; set; } = string.Empty;
    public string Russian { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(BilingualTextSource.Value))
        {
            Source = new BilingualTextSource(English, Russian),
            Mode = BindingMode.OneWay
        }.ProvideValue(serviceProvider);
}

internal sealed class BilingualTextSource : INotifyPropertyChanged
{
    private readonly string _english;
    private readonly string _russian;

    public BilingualTextSource(string english, string russian)
    {
        _english = english;
        _russian = russian;
        UiLanguageService.LanguageChanged += LanguageChanged;
    }

    public string Value => UiLanguageService.Pick(_english, _russian);
    public event PropertyChangedEventHandler? PropertyChanged;

    private void LanguageChanged(object? sender, EventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
