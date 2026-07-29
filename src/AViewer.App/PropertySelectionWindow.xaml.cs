using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace AViewer.App;

public partial class PropertySelectionWindow : Window
{
    private readonly List<PropertyChoice> _all;
    public ObservableCollection<PropertyChoice> Choices { get; }
    public IReadOnlyList<PropertyChoice> AllChoices => _all;
    public bool ShowUnavailableProperties => ShowUnavailableCheckBox.IsChecked == true;

    public PropertySelectionWindow(
        IReadOnlyList<PropertyChoice> choices,
        bool showUnavailableProperties)
    {
        InitializeComponent();
        _all = choices.ToList();
        Choices = new ObservableCollection<PropertyChoice>(_all);
        ShowUnavailableCheckBox.IsChecked = showUnavailableProperties;
        DataContext = this;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        Choices.Clear();
        foreach (var choice in _all.Where(item =>
                     query.Length == 0 || item.Label.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            Choices.Add(choice);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var choice in _all) choice.IsSelected = true;
        ChoiceList.Items.Refresh();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var choice in _all) choice.IsSelected = false;
        ChoiceList.Items.Refresh();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
