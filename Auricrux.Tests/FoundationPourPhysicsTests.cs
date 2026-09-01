using Auricrux.Web.Services.Breakthrough;
using Auricrux.Web.Services.Breakthrough.Physics;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// Guards the calibration bucket parser. The original implementation produced
/// "0.6.0.7" and threw once enough verifications existed to populate calibration.
/// </summary>
public sealed class MetaLearningCalibrationTests
{
    [Theory]
    [InlineData("confidence_0.6_to_0.7", 0.6)]
    [InlineData("confidence_0.9_to_1.0", 0.9)]
    public void ParsesStatedConfidenceFromBucketKey(string key, double expected)
    {
        Assert.True(MetaLearningService.TryParseCalibrationLowerBound(key, out var lowerBound));
        Assert.Equal(expected, lowerBound, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-bucket")]
    public void ReturnsFalseForUnexpectedKeys(string key)
    {
        Assert.False(MetaLearningService.TryParseCalibrationLowerBound(key, out _));
    }
}

/// <summary>
/// Proves pour predictions come from ACI-based physics, not fixed ratios:
/// cold ambient must slow strength gain and push stripping later.
/// </summary>
public sealed class FoundationPourPhysicsTests
{
    [Fact]
    public void ColdAmbient_LowersEarlyStrength_AndDelaysStripping()
    {
        var warm = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 70,
            slabThicknessIn: 8);

        var cold = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 38,
            slabThicknessIn: 8);

        Assert.True(cold.Strength7dPsi < warm.Strength7dPsi);
        Assert.True(cold.CureDaysToStripping > warm.CureDaysToStripping);
        Assert.True(cold.ColdProtectionRequired);
        Assert.False(warm.ColdProtectionRequired);
    }

    [Fact]
    public void ColdWeatherProtection_RecoversCureTemperature()
    {
        var unprotected = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 38,
            slabThicknessIn: 8);

        var protectedPour = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.ColdWeatherProtected,
            targetPsi: 4000,
            ambientTempF: 38,
            slabThicknessIn: 8);

        Assert.True(protectedPour.EffectiveCureTempF > unprotected.EffectiveCureTempF);
        Assert.True(protectedPour.Strength7dPsi > unprotected.Strength7dPsi);
        Assert.True(protectedPour.CureDaysToStripping <= unprotected.CureDaysToStripping);
    }

    [Fact]
    public void AcceleratedMix_StripsEarlierThanStandard_AtSameTemperature()
    {
        var standard = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 68,
            slabThicknessIn: 8);

        var accelerated = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.AcceleratedHighEarly,
            targetPsi: 4000,
            ambientTempF: 68,
            slabThicknessIn: 8);

        Assert.True(accelerated.CureDaysToStripping <= standard.CureDaysToStripping);
        Assert.True(accelerated.ColdJointRiskPercent > standard.ColdJointRiskPercent);
    }

    [Fact]
    public void ColdPour_StillReachesStripping_TemperatureDelaysRatherThanCaps()
    {
        var cold = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 30,
            slabThicknessIn: 8);

        // Temperature scales equivalent age, so a cold pour is delayed but not capped forever.
        Assert.True(cold.CureDaysToStripping < FoundationPourPhysics.MaxCureDaysConsidered);
        Assert.True(cold.CureDaysToStripping > 14);
    }

    [Fact]
    public void MaturityFactor_ScalesWithCureTemperature()
    {
        Assert.Equal(1.0, FoundationPourPhysics.MaturityFactor(FoundationPourPhysics.ReferenceCureTempF), 3);
        Assert.True(FoundationPourPhysics.MaturityFactor(40) < 1.0);
        Assert.True(FoundationPourPhysics.MaturityFactor(20) >= 0.15);
    }

    [Fact]
    public void HighEvaporation_RaisesColdJointRisk()
    {
        var calm = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 75,
            slabThicknessIn: 8,
            relativeHumidity: 0.80,
            windSpeedMph: 2);

        var windyDry = FoundationPourPhysics.Predict(
            FoundationPourPhysics.PourStrategy.StandardAmbient,
            targetPsi: 4000,
            ambientTempF: 95,
            slabThicknessIn: 8,
            relativeHumidity: 0.15,
            windSpeedMph: 20);

        Assert.True(windyDry.EvaporationRateLbPerSqFtPerHour > calm.EvaporationRateLbPerSqFtPerHour);
        Assert.True(windyDry.ColdJointRiskPercent > calm.ColdJointRiskPercent);
    }
}
