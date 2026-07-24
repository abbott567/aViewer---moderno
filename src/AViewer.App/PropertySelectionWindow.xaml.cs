using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace AViewer.App;

public partial class PropertySelectionWindow : Window
{
    private readonly List<PropertyChoice> _choices;
    private readonly ICollectionView _view;

    public PropertySelectionWindow(IEnumerable<PropertyChoice> choices)
    {
        InitializeComponent();
        _choices = choices
            .Select(choice => new PropertyChoice
            {
                Group = choice.Group,
                Name = choice.Name,
                IsSelected = choice.IsSelected
            })
            .ToList();

        _view = CollectionViewSource.GetDefaultView(_choices);
        _view.Filter = MatchesSearch;
        ChoiceGrid.ItemsSource = _view;
    }

    public IReadOnlyList<PropertyChoice> Choices => _choices;

    private bool MatchesSearch(object item)
    {
        if (item is not PropertyChoice choice)
        {
            return false;
        }

        var search = SearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        return choice.Group.Contains(search, StringComparison.OrdinalIgnoreCase)
            || choice.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _view.Refresh();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        SetVisibleChoices(true);
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        SetVisibleChoices(false);
    }

    private void SetVisibleChoices(bool selected)
    {
        foreach (var item in _view.Cast<PropertyChoice>())
        {
            item.IsSelected = selected;
        }

        ChoiceGrid.Items.Refresh();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ChoiceGrid.CommitEdit();
        ChoiceGrid.CommitEdit();
        DialogResult = true;
    }
}
