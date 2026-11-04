using PingWidget.Core;

namespace PingWidget.Tests;

public class HealthEvaluatorTests
{
    private static List<PingSample> Of(params long?[] rtts) =>
        rtts.Select(r => new PingSample(DateTime.UnixEpoch, r)).ToList();

    [Fact] public void TwoBadOfFive_False() => Assert.False(HealthEvaluator.IsUnhealthy(Of(5, 50, 5, 50, 5), 10));
    [Fact] public void ThreeBadOfFive_True() => Assert.True(HealthEvaluator.IsUnhealthy(Of(50, 5, 50, 5, 50), 10));
    [Fact] public void LostCountsAsBad() => Assert.True(HealthEvaluator.IsUnhealthy(Of(null, null, 5, null, 5), 10));
    [Fact] public void OnlyLastFiveCount() => Assert.False(HealthEvaluator.IsUnhealthy(Of(50, 50, 50, 5, 5, 5, 5, 5), 10));
    [Fact] public void EqualToThreshold_NotBad() => Assert.False(HealthEvaluator.IsUnhealthy(Of(10, 10, 10, 10, 10), 10));
    [Fact] public void FewerThanFive_ThreeBad_True() => Assert.True(HealthEvaluator.IsUnhealthy(Of(50, 50, 50), 10));
    [Fact] public void FewerThanFive_TwoBad_False() => Assert.False(HealthEvaluator.IsUnhealthy(Of(50, 50), 10));
    [Fact] public void Empty_False() => Assert.False(HealthEvaluator.IsUnhealthy(Of(), 10));
}
