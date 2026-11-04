using PingWidget.Core;
using PingWidget.Core.Metrics;
using PingWidget.Settings;

namespace PingWidget.Tests;

public class HostListValidatorTests
{
    [Fact]
    public void Defaults_Valid() => Assert.Empty(HostListValidator.Validate(AppSettings.DefaultHosts()));

    [Fact]
    public void BlankAddress() =>
        Assert.Equal(["Адрес не может быть пустым"], HostListValidator.Validate([new HostConfig("  ", 10)]));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10001)]
    public void ThresholdOutOfRange(int t) =>
        Assert.Equal(["Порог должен быть от 1 до 10000 мс"], HostListValidator.Validate([new HostConfig("8.8.8.8", t)]));

    [Theory]
    [InlineData(1)]
    [InlineData(10000)]
    public void ThresholdBoundsValid(int t) => Assert.Empty(HostListValidator.Validate([new HostConfig("8.8.8.8", t)]));

    [Fact]
    public void DuplicateAddress_CaseInsensitiveTrimmed() =>
        Assert.Equal(["Адрес router.lan указан дважды"],
            HostListValidator.Validate([new HostConfig("router.lan", 10), new HostConfig(" ROUTER.lan ", 20)]));

    [Fact]
    public void EmptyList() =>
        Assert.Equal(["Добавьте хотя бы один адрес"], HostListValidator.Validate([]));

    [Fact]
    public void ValidateThresholds_Defaults_Valid() => Assert.Empty(HostListValidator.ValidateThresholds(new MetricThresholds()));

    [Fact]
    public void ValidateThresholds_PercentOutOfRange() =>
        Assert.Equal(["Порог нагрузки и памяти должен быть от 1 до 100 %"],
            HostListValidator.ValidateThresholds(new MetricThresholds { GpuLoad = 101, RamUsed = 0 }));

    [Fact]
    public void ValidateThresholds_TempOutOfRange() =>
        Assert.Equal(["Порог температуры должен быть от 1 до 150 °C"],
            HostListValidator.ValidateThresholds(new MetricThresholds { GpuTemp = 0 }));
}
