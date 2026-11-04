using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PingWidget.Core;
using PingWidget.Core.Metrics;

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

    private static readonly (MetricKind Kind, string Label)[] ThresholdFields =
    [
        (MetricKind.CpuLoad, "CPU нагрузка, %"),
        (MetricKind.CpuTemp, "CPU темп., °C"),
        (MetricKind.RamUsed, "RAM, %"),
        (MetricKind.GpuLoad, "GPU нагрузка, %"),
        (MetricKind.VramUsed, "VRAM, %"),
        (MetricKind.GpuTemp, "GPU темп., °C"),
    ];

    public SettingsWindow(IReadOnlyList<HostConfig> current, MetricThresholds thresholds)
    {
        InitializeComponent();
        _rows = new ObservableCollection<EditableHost>(
            current.Select(h => new EditableHost { Address = h.Address, Threshold = h.ThresholdMs.ToString() }));
        Grid.ItemsSource = _rows;

        foreach (var (kind, label) in ThresholdFields)
        {
            ThresholdTexts[kind] = thresholds.Of(kind).ToString();
            var box = new TextBox { Width = 60, Text = ThresholdTexts[kind], VerticalContentAlignment = VerticalAlignment.Center };
            box.TextChanged += (_, _) => ThresholdTexts[kind] = box.Text;
            var cell = new DockPanel { Margin = new Thickness(0, 2, 12, 2) };
            DockPanel.SetDock(box, Dock.Right);
            cell.Children.Add(box);
            cell.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            ThresholdGrid.Children.Add(cell);
        }
    }

    /// <summary>Current text of each hardware threshold field (kept as text so bad input gets our own message).</summary>
    internal Dictionary<MetricKind, string> ThresholdTexts { get; } = [];

    /// <summary>The saved hardware thresholds, or null if the window was cancelled.</summary>
    public MetricThresholds? ResultThresholds { get; private set; }

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

        var thresholds = new MetricThresholds();
        foreach (var (kind, _) in ThresholdFields)
            thresholds.Set(kind, int.TryParse(ThresholdTexts[kind]?.Trim(), out var v) ? v : 0);

        var errors = HostListValidator.Validate(hosts).Concat(HostListValidator.ValidateThresholds(thresholds)).ToList();
        if (errors.Count > 0)
        {
            Errors.Text = string.Join(Environment.NewLine, errors);
            Errors.Visibility = Visibility.Visible;
            return false;
        }

        Result = hosts;
        ResultThresholds = thresholds;
        return true;
    }
}
