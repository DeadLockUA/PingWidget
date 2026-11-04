using System.Globalization;
using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

public class MetricFormatterTests
{
    [Fact] public void CpuLoad() => Assert.Equal("46%", MetricFormatter.Format(MetricKind.CpuLoad, new(46.27)));
    [Fact] public void Temp() => Assert.Equal("54°C", MetricFormatter.Format(MetricKind.CpuTemp, new(54.4)));
    [Fact] public void Ram() => Assert.Equal("18.5/31.9 GB 58%", MetricFormatter.Format(MetricKind.RamUsed, new(57.95, 18.50, 31.92)));
    [Fact] public void Vram() => Assert.Equal("3.5/16 GB 22%", MetricFormatter.Format(MetricKind.VramUsed, new(21.58, 3.45, 15.98)));

    [Theory]
    [InlineData(MetricKind.CpuLoad)]
    [InlineData(MetricKind.CpuTemp)]
    [InlineData(MetricKind.RamUsed)]
    [InlineData(MetricKind.VramUsed)]
    public void Missing_Dash(MetricKind k) => Assert.Equal("—", MetricFormatter.Format(k, MetricReading.Missing));

    [Fact]
    public void RamWithoutAmounts_ShowsPercentOnly() => Assert.Equal("58%", MetricFormatter.Format(MetricKind.RamUsed, new(57.95)));

    [Fact]
    public void RussianCulture_UsesDot()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            Assert.Equal("18.5/31.9 GB 58%", MetricFormatter.Format(MetricKind.RamUsed, new(57.95, 18.50, 31.92)));
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}
