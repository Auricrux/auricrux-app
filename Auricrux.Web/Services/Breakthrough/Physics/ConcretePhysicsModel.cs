namespace Auricrux.Web.Services.Breakthrough.Physics;

/// <summary>
/// Concrete Material Physics Model
/// 
/// Implements fundamental concrete behavior for:
/// - Strength gain over time (maturity)
/// - Compressive/tensile strength relationships
/// - Temperature effects on curing
/// - Shrinkage and creep predictions
/// - Mix design validation
/// 
/// Based on: ACI 209R, ACI 211, ACI 318
/// </summary>
public static class ConcretePhysicsModel
{
    /// <summary>
    /// Predict compressive strength at age t based on 28-day strength
    /// Uses modified ACI 209R maturity function
    /// </summary>
    public static double PredictStrengthAtAge(
        double fc28Psi,
        int ageInDays,
        CementType cementType = CementType.TypeI,
        CuringCondition curing = CuringCondition.MoistCured)
    {
        // ACI 209R-92 Equation 2-1
        // f(t) = fc28 × [t / (a + b×t)]

        var (a, b) = cementType switch
        {
            CementType.TypeI => (4.0, 0.85),
            CementType.TypeIII => (2.3, 0.92), // Rapid hardening
            _ => (4.0, 0.85)
        };

        // Curing condition modifier
        var curingFactor = curing switch
        {
            CuringCondition.MoistCured => 1.0,
            CuringCondition.SteamCured => 1.15,
            CuringCondition.AirDried => 0.90,
            _ => 1.0
        };

        var strengthRatio = ageInDays / (a + b * ageInDays);
        return fc28Psi * strengthRatio * curingFactor;
    }

    /// <summary>
    /// Calculate tensile strength from compressive strength
    /// ACI 318-19 Eq. 19.2.3.1
    /// </summary>
    public static double TensileStrength(double fcPsi)
    {
        // fr = 7.5 × √f'c (psi)
        return 7.5 * Math.Sqrt(fcPsi);
    }

    /// <summary>
    /// Calculate modulus of elasticity
    /// ACI 318-19 Eq. 19.2.2.1a (normal weight concrete)
    /// </summary>
    public static double ModulusOfElasticity(double fcPsi, double densityPcf = 145)
    {
        // Ec = w^1.5 × 33 × √f'c (psi)
        var w = densityPcf;
        return Math.Pow(w, 1.5) * 33 * Math.Sqrt(fcPsi);
    }

    /// <summary>
    /// Predict shrinkage strain over time
    /// ACI 209R-92 shrinkage model
    /// </summary>
    public static double ShrinkageStrain(
        int ageInDays,
        int ageCuringEndDays,
        double relativeHumidity,
        double volumeToSurfaceRatio)
    {
        // εsh(t) = εsh,ultimate × [t / (35 + t)]
        var t = Math.Max(0, ageInDays - ageCuringEndDays);

        // Ultimate shrinkage (microstrain)
        // Base: 780 microstrain for reference conditions
        var baseUltimateShrinkage = 780.0;

        // Humidity correction
        var humidityFactor = relativeHumidity <= 0.40 ? 1.4 :
                            relativeHumidity <= 0.80 ? (1.4 - 0.010 * (relativeHumidity - 0.40) * 100) :
                            0.3;

        // Volume/surface correction
        var vsFactor = Math.Min(1.2, 1.2 * Math.Exp(-0.05 * volumeToSurfaceRatio));

        var ultimateShrinkage = baseUltimateShrinkage * humidityFactor * vsFactor;

        // Time development
        var timeFactor = t / (35.0 + t);

        return ultimateShrinkage * timeFactor; // microstrain
    }

    /// <summary>
    /// Temperature-adjusted maturity (Nurse-Saul function)
    /// </summary>
    public static double Maturity(
        List<(double TempF, double HoursAtTemp)> temperatureHistory,
        double datumTempF = 32)
    {
        // M = Σ (T - T0) × Δt
        // where T = concrete temp (°F), T0 = datum temp, Δt = time interval

        return temperatureHistory
            .Where(th => th.TempF > datumTempF)
            .Sum(th => (th.TempF - datumTempF) * th.HoursAtTemp);
    }

    public enum CementType
    {
        TypeI,     // Normal
        TypeII,    // Moderate sulfate resistance
        TypeIII,   // High early strength
        TypeIV,    // Low heat
        TypeV      // High sulfate resistance
    }

    public enum CuringCondition
    {
        MoistCured,
        SteamCured,
        AirDried
    }
}
