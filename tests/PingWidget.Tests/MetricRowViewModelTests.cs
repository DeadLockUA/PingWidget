using PingWidget.Core.Metrics;
using PingWidget.Widget;

namespace PingWidget.Tests;

public class MetricRowViewModelTests
{
    [Fact]
    public void FormatsLastReading()
    {
        var vm = new MetricRowViewModel(MetricKind.RamUsed, "Used", 90) { Readings = [new(57.95, 18.5, 31.92)] };
        Assert.Equal("18.5/31.9 GB 58%", vm.CurrentText);
        Assert.False(vm.IsAboveThreshold);
    }

    [Fact]
    public void AboveThreshold()
    {
        var vm = new MetricRowViewModel(MetricKind.CpuTemp, "Temp", 80) { Readings = [new(70), new(85)] };
        Assert.Equal("85°C", vm.CurrentText);
        Assert.True(vm.IsAboveThreshold);
    }

    [Fact]
    public void MissingLast()
    {
        var vm = new MetricRowViewModel(MetricKind.CpuTemp, "Temp", 80) { Readings = [new(85), MetricReading.Missing] };
        Assert.Equal("—", vm.CurrentText);
        Assert.False(vm.IsAboveThreshold);
    }

    [Fact]
    public void NoReadings() => Assert.Equal("—", new MetricRowViewModel(MetricKind.GpuLoad, "Load", 90).CurrentText);

    [Fact]
    public void ValuesProjection_AndPropertyChanged()
    {
        var vm = new MetricRowViewModel(MetricKind.GpuLoad, "Load", 90);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Readings = [new(5), MetricReading.Missing];
        Assert.Equal(new double?[] { 5, null }, vm.Values);
        Assert.Contains(nameof(MetricRowViewModel.Values), changed);
        Assert.Contains(nameof(MetricRowViewModel.CurrentText), changed);
        Assert.Contains(nameof(MetricRowViewModel.IsAboveThreshold), changed);
    }
}
