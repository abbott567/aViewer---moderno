using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace AViewer.App;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("AViewer.App.Resources.Strings", typeof(LocalizationManager).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    private LocalizationManager()
    {
    }

    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture => _culture;

    public FlowDirection FlowDirection =>
        _culture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        return Resources.GetString(key, _culture) ?? $"[{key}]";
    }

    public string Format(string key, params object?[] arguments)
    {
        return string.Format(_culture, Get(key), arguments);
    }

    public void SetCulture(string? cultureName)
    {
        var culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.InstalledUICulture
            : CultureInfo.GetCultureInfo(cultureName);

        if (string.Equals(_culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _culture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
    }
}

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
