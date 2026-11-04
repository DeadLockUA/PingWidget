using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PingWidget.Core;

namespace PingWidget.Settings;

public partial class SettingsWindow : Window
{
    /// <summary>Editable row; threshold kept as text so bad input gets our own error message.</summary>
    public sealed class EditableHost
    {
        public string Address { get; set; } = "";
        public string Threshold { get; set; } = "";
    }

    private readonly ObservableCollection<EditableHost> _rows;

    public SettingsWindow(IReadOnlyList<HostConfig> current)
    {
        InitializeComponent();
        _rows = new ObservableCollection<EditableHost>(
            current.Select(h => new EditableHost { Address = h.Address, Threshold = h.ThresholdMs.ToString() }));
        Grid.ItemsSource = _rows;
    }

    /// <summary>The saved host list, or null if the window was cancelled.</summary>
    public IReadOnlyList<HostConfig>? Result { get; private set; }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var row = new EditableHost { Threshold = "50" };
        _rows.Add(row);
        Grid.SelectedItem = row;
        Grid.ScrollIntoView(row);
        Grid.CurrentCell = new DataGridCellInfo(row, Grid.Columns[0]);
        Grid.Focus();
        Grid.BeginEdit();
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        Grid.CancelEdit();
        if (Grid.SelectedItem is EditableHost row) _rows.Remove(row);
    }

    internal ObservableCollection<EditableHost> Rows => _rows;

    internal string ErrorText => Errors.Text;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
        if (TryAccept()) DialogResult = true;
    }

    /// <summary>Validates the rows; on success sets <see cref="Result"/>, otherwise shows the errors.</summary>
    internal bool TryAccept()
    {
        var hosts = _rows
            .Select(r => new HostConfig((r.Address ?? "").Trim(), int.TryParse(r.Threshold?.Trim(), out var t) ? t : 0))
            .ToList();

        var errors = HostListValidator.Validate(hosts);
        if (errors.Count > 0)
        {
            Errors.Text = string.Join(Environment.NewLine, errors);
            Errors.Visibility = Visibility.Visible;
            return false;
        }

        Result = hosts;
        return true;
    }
}
