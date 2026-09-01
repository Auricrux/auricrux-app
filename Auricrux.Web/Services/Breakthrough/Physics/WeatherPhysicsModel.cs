namespace Auricrux.Web.Services.Breakthrough.Physics;

/// <summary>
/// Weather Impact Physics Model
/// 
/// Implements weather effects on construction for:
/// - Concrete curing (temperature, humidity)
/// - Cold weather protection requirements
/// - Hot weather concrete precautions
/// - Wind load effects on operations
/// - Precipitation impact on productivity
/// - Heat stress on workers (WBGT)
/// 
/// Based on: ACI 306R (Cold), ACI 305R (Hot), OSHA heat stress, ASCE 37
/// </summary>
public static class WeatherPhysicsModel
{
    /// <summary>
    /// Determine if cold weather protection is required for concrete
    /// ACI 306R definition: Mean daily temp < 40°F and temp < 50°F for >12 hrs
    /// </summary>
    public static bool RequiresColdWeatherProtection(
        double meanDailyTempF,
        double minTempF,
        double hoursBelow50F)
    {
        return meanDailyTempF < 40 || (minTempF < 50 && hoursBelow50F > 12);
    }

    /// <summary>
    /// Calculate required concrete placement temperature for cold weather
    /// ACI 306R recommendations
    /// </summary>
    public static (double MinTempF, double MaxTempF) ColdWeatherPlacementTemp(
        double sectionThicknessInches,
        double ambientTempF)
    {
        // Minimum placement temp
        double minTemp;
        if (sectionThicknessInches < 12)
        {
            minTemp = 55; // Thin sections need warmer concrete
        }
        else if (sectionThicknessInches < 36)
        {
            minTemp = 50;
        }
        else
        {
            minTemp = 45; // Mass concrete can be cooler
        }

        // Adjust for extreme cold
        if (ambientTempF < 0)
        {
            minTemp += 10;
        }

        // Maximum to avoid thermal shock
        var maxTemp = 90.0;

        return (minTemp, maxTemp);
    }

    /// <summary>
    /// Calculate concrete temperature after mixing
    /// Weighted average of component temperatures
    /// </summary>
    public static double ConcreteMixtureTemp(
        double cementWeightLbs,
        double cementTempF,
        double aggregateWeightLbs,
        double aggregateTempF,
        double waterWeightLbs,
        double waterTempF)
    {
        // T = (0.22(Wa×Ta + Wc×Tc) + Ww×Tw + Wi×Ti) / (0.22(Wa + Wc) + Ww + Wi)
        // Simplified (no ice): T = (0.22×Wa×Ta + 0.22×Wc×Tc + Ww×Tw) / (0.22×Wa + 0.22×Wc + Ww)

        var specificHeatConcrete = 0.22; // BTU/(lb·°F)

        var numerator = specificHeatConcrete * aggregateWeightLbs * aggregateTempF
                      + specificHeatConcrete * cementWeightLbs * cementTempF
                      + waterWeightLbs * waterTempF;

        var denominator = specificHeatConcrete * aggregateWeightLbs
                        + specificHeatConcrete * cementWeightLbs
                        + waterWeightLbs;

        return numerator / denominator;
    }

    /// <summary>
    /// Determine if hot weather precautions are required
    /// ACI 305R definition: Any combination producing high concrete temp or rapid moisture loss
    /// </summary>
    public static bool RequiresHotWeatherPrecautions(
        double airTempF,
        double relativeHumidity,
        double windSpeedMph)
    {
        // Hot weather if air temp > 85°F
        if (airTempF > 85) return true;

        // High evaporation conditions (ACI 305R Figure 4.1)
        var evaporationRate = EstimateEvaporationRate(airTempF, relativeHumidity, windSpeedMph);
        if (evaporationRate > 0.2) return true; // lb/ft²/hr

        return false;
    }

    /// <summary>
    /// Estimate evaporation rate from concrete surface
    /// ACI 305R Figure 4.1 (simplified)
    /// </summary>
    public static double EstimateEvaporationRate(
        double airTempF,
        double relativeHumidity,
        double windSpeedMph,
        double concreteTempF = 0)
    {
        if (concreteTempF == 0) concreteTempF = airTempF + 10; // Assume concrete ~10°F warmer

        // Simplified empirical formula (lb/ft²/hr)
        // E = [(2.5 + 1.5×V) × (Tcs - Tcd)] / 100
        // where V = wind speed (mph), Tcs = concrete surface temp, Tcd = dewpoint

        var dewpointF = ApproximateDewpoint(airTempF, relativeHumidity);
        var evapRate = ((2.5 + 1.5 * windSpeedMph) * (concreteTempF - dewpointF)) / 100.0;

        return Math.Max(0, evapRate);
    }

    /// <summary>
    /// Calculate Wet Bulb Globe Temperature (WBGT) for heat stress assessment
    /// OSHA heat stress guidance
    /// </summary>
    public static double WetBulbGlobeTemperature(
        double dryBulbTempF,
        double relativeHumidity,
        double windSpeedMph = 5,
        bool inDirectSunlight = true)
    {
        // Simplified WBGT estimation (outdoors with sun exposure)
        // WBGT ≈ 0.7×Twb + 0.2×Tg + 0.1×Tdb
        // Approximation: WBGT ≈ Tdb - ((100 - RH) / 5) for humid conditions

        var wetBulbEffect = (100 - relativeHumidity * 100) / 5.0;
        var wbgt = dryBulbTempF - wetBulbEffect;

        // Adjust for wind
        wbgt -= (windSpeedMph - 5) * 0.5;

        // Adjust for sun
        if (inDirectSunlight)
        {
            wbgt += 5;
        }

        return wbgt;
    }

    /// <summary>
    /// Determine work/rest schedule based on WBGT
    /// OSHA Technical Manual Section III Chapter 4
    /// </summary>
    public static (string WorkLevel, string RestSchedule, bool ExtremeRisk) HeatStressWorkSchedule(
        double wbgtF,
        WorkloadIntensity workload = WorkloadIntensity.Moderate)
    {
        // Thresholds vary by workload
        var (cautionThreshold, dangerThreshold, extremeThreshold) = workload switch
        {
            WorkloadIntensity.Light => (86, 89, 91),
            WorkloadIntensity.Moderate => (80, 85, 88),
            WorkloadIntensity.Heavy => (77, 82, 86),
            _ => (80, 85, 88)
        };

        if (wbgtF < cautionThreshold)
        {
            return ("Normal", "Normal breaks sufficient", false);
        }
        else if (wbgtF < dangerThreshold)
        {
            return ("Caution", "Every 30 min: 15 min work, 15 min rest in shade", false);
        }
        else if (wbgtF < extremeThreshold)
        {
            return ("Danger", "Every 30 min: 10 min work, 20 min rest with cooling", false);
        }
        else
        {
            return ("Extreme", "STOP WORK - Extreme heat risk", true);
        }
    }

    /// <summary>
    /// Calculate wind pressure on structure (simplified)
    /// ASCE 7-22 Chapter 26
    /// </summary>
    public static double WindPressure(
        double windSpeedMph,
        double exposureCategoryFactor = 1.0,
        double gustFactor = 1.14)
    {
        // q = 0.00256 × V² × Kz × Kzt × Kd (psf)
        // Simplified for basic wind pressure

        var velocityPressure = 0.00256 * Math.Pow(windSpeedMph, 2);
        return velocityPressure * exposureCategoryFactor * gustFactor;
    }

    /// <summary>
    /// Determine if wind conditions are safe for crane operations
    /// Typical crane manufacturer limits
    /// </summary>
    public static (bool IsSafe, string Guidance) CraneOperationWindCheck(
        double windSpeedMph,
        bool isLoadSuspended,
        double boomLengthFt)
    {
        // Typical limits:
        // - 20 mph: exercise caution
        // - 25 mph: stop lifting operations with load in air
        // - 30 mph: shut down and secure crane

        if (windSpeedMph < 20)
        {
            return (true, "Wind conditions acceptable for crane operations");
        }
        else if (windSpeedMph < 25)
        {
            return (!isLoadSuspended, "CAUTION: Wind approaching limits. Complete current lift, then pause.");
        }
        else if (windSpeedMph < 30)
        {
            return (false, "STOP: Wind exceeds safe lifting limits. Secure load and crane.");
        }
        else
        {
            return (false, "DANGER: Shut down crane immediately and secure all equipment.");
        }
    }

    /// <summary>
    /// Estimate productivity impact from precipitation
    /// </summary>
    public static double ProductivityFactor(
        PrecipitationType precip,
        double intensityInchesPerHour = 0)
    {
        return precip switch
        {
            PrecipitationType.None => 1.0,
            PrecipitationType.LightRain => intensityInchesPerHour < 0.1 ? 0.85 : 0.70,
            PrecipitationType.ModerateRain => 0.50,
            PrecipitationType.HeavyRain => 0.20, // Likely shutdown
            PrecipitationType.Snow => intensityInchesPerHour < 0.5 ? 0.60 : 0.30,
            _ => 1.0
        };
    }

    // ── Helper Methods ──────────────────────────────────────────────────────────

    private static double ApproximateDewpoint(double tempF, double relativeHumidity)
    {
        // Magnus formula approximation
        var tempC = (tempF - 32) * 5 / 9;
        var rh = relativeHumidity;

        var a = 17.27;
        var b = 237.7;

        var alpha = ((a * tempC) / (b + tempC)) + Math.Log(rh);
        var dewpointC = (b * alpha) / (a - alpha);

        return dewpointC * 9 / 5 + 32; // Convert back to F
    }

    public enum WorkloadIntensity
    {
        Light,
        Moderate,
        Heavy
    }

    public enum PrecipitationType
    {
        None,
        LightRain,
        ModerateRain,
        HeavyRain,
        Snow
    }
}
