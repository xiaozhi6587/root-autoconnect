using System.Windows;
using System.Windows.Controls;

namespace UsbAutoConnect;

public partial class ExclusionsWindow : MacWindow
{
    private List<DeviceExclusion> _rules;
    private readonly Func<List<DeviceExclusion>, bool> _save;
    public ExclusionsWindow(IEnumerable<DeviceExclusion> rules, Func<List<DeviceExclusion>, bool> save)
    {
        InitializeComponent();
        _rules = rules.ToList(); _save = save;
        Refresh();
    }
    private void Refresh()
    {
        RulesGrid.ItemsSource = _rules.ToArray();
        NoRulesText.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"共 {_rules.Count} 个排除设备";
    }
    private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RestoreButton != null) RestoreButton.IsEnabled = RulesGrid.SelectedItems.Count > 0;
    }
    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var selected = RulesGrid.SelectedItems.Cast<DeviceExclusion>().ToHashSet();
        var remaining = _rules.Where(rule => !selected.Contains(rule)).ToList();
        if (selected.Count == 0 || !_save(remaining)) return;
        _rules = remaining; Refresh();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
