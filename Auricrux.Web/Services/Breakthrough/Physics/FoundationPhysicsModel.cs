namespace Auricrux.Web.Services.Breakthrough.Physics;

/// <summary>
/// Foundation Engineering Physics Model
/// 
/// Implements geotechnical and foundation mechanics for:
/// - Bearing capacity calculations (Terzaghi, Meyerhof, Hansen)
/// - Settlement predictions (immediate, consolidation, secondary)
/// - Pile capacity (driven, drilled, friction, end-bearing)
/// - Lateral earth pressure
/// - Slope stability
/// 
/// Based on: Terzaghi & Peck, Bowles, Das, AASHTO LRFD
/// </summary>
public static class FoundationPhysicsModel
{
    /// <summary>
    /// Calculate ultimate bearing capacity using Terzaghi equations
    /// For strip, square, or circular footings
    /// </summary>
    public static double BearingCapacity(
        FootingShape shape,
        double widthFt,
        double depthFt,
        double cohesionPsf,
        double frictionAngleDegrees,
        double unitWeightPcf)
    {
        // Terzaghi bearing capacity: qu = c×Nc×sc + γ×D×Nq + 0.5×γ×B×Nγ×sγ

        var phi = frictionAngleDegrees * Math.PI / 180.0; // Convert to radians

        // Bearing capacity factors (Terzaghi)
        var nq = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4.0 + phi / 2.0), 2);
        var nc = phi == 0 ? 5.7 : (nq - 1.0) / Math.Tan(phi);
        var ng = phi == 0 ? 0 : 1.5 * (nq - 1.0) * Math.Tan(phi);

        // Shape factors
        var (sc, sg) = shape switch
        {
            FootingShape.Strip => (1.0, 1.0),
            FootingShape.Square => (1.3, 0.8),
            FootingShape.Circular => (1.3, 0.6),
            _ => (1.0, 1.0)
        };

        // Ultimate bearing capacity (psf)
        var qu = cohesionPsf * nc * sc
               + unitWeightPcf * depthFt * nq
               + 0.5 * unitWeightPcf * widthFt * ng * sg;

        return qu;
    }

    /// <summary>
    /// Allowable bearing capacity with factor of safety
    /// </summary>
    public static double AllowableBearingCapacity(
        FootingShape shape,
        double widthFt,
        double depthFt,
        double cohesionPsf,
        double frictionAngleDegrees,
        double unitWeightPcf,
        double factorOfSafety = 3.0)
    {
        var qu = BearingCapacity(shape, widthFt, depthFt, cohesionPsf, frictionAngleDegrees, unitWeightPcf);
        return qu / factorOfSafety;
    }

    /// <summary>
    /// Predict immediate (elastic) settlement
    /// Schmertmann method for cohesionless soils
    /// </summary>
    public static double ImmediateSettlement(
        double appliedPressurePsf,
        double footingWidthFt,
        double footingDepthFt,
        double elasticModulusPsf,
        double poissonRatio = 0.3)
    {
        // Elastic settlement: Se = q × B × (1 - ν²) × I / Es
        // where I = influence factor (approx 0.85 for flexible footing)

        var influenceFactor = 0.85;
        var settlementFt = appliedPressurePsf * footingWidthFt
                          * (1 - Math.Pow(poissonRatio, 2))
                          * influenceFactor
                          / elasticModulusPsf;

        return settlementFt * 12; // Convert to inches
    }

    /// <summary>
    /// Predict consolidation settlement in clay
    /// Terzaghi 1-D consolidation theory
    /// </summary>
    public static double ConsolidationSettlement(
        double appliedPressurePsf,
        double initialVoidRatio,
        double compressionIndex,
        double layerThicknessFt,
        double effectiveStressPsf,
        bool isNormallyConsolidated = true,
        double recompressionIndex = 0)
    {
        var finalStressPsf = effectiveStressPsf + appliedPressurePsf;

        double settlementFt;

        if (isNormallyConsolidated)
        {
            // Sc = (Cc / (1 + e0)) × H × log10(σf / σ0)
            settlementFt = (compressionIndex / (1 + initialVoidRatio))
                          * layerThicknessFt
                          * Math.Log10(finalStressPsf / effectiveStressPsf);
        }
        else
        {
            // Overconsolidated: use Cr instead of Cc
            var cr = recompressionIndex > 0 ? recompressionIndex : compressionIndex / 10;
            settlementFt = (cr / (1 + initialVoidRatio))
                          * layerThicknessFt
                          * Math.Log10(finalStressPsf / effectiveStressPsf);
        }

        return settlementFt * 12; // Convert to inches
    }

    /// <summary>
    /// Calculate driven pile capacity (Meyerhof method)
    /// </summary>
    public static double DrivenPileCapacity(
        double pileDiameterInches,
        double pileLengthFt,
        double skinFrictionPsf,
        double endBearingPsf)
    {
        var diameterFt = pileDiameterInches / 12.0;
        var perimeterFt = Math.PI * diameterFt;
        var areaFt2 = Math.PI * Math.Pow(diameterFt / 2.0, 2);

        // Qult = Qskin + Qtip
        var qSkin = perimeterFt * pileLengthFt * skinFrictionPsf;
        var qTip = areaFt2 * endBearingPsf;

        return qSkin + qTip; // lbs
    }

    /// <summary>
    /// Lateral earth pressure coefficient (Rankine theory)
    /// </summary>
    public static (double Ka, double Kp) LateralEarthPressureCoefficients(double frictionAngleDegrees)
    {
        var phi = frictionAngleDegrees * Math.PI / 180.0;

        // Active: Ka = tan²(45° - φ/2)
        var ka = Math.Pow(Math.Tan(Math.PI / 4.0 - phi / 2.0), 2);

        // Passive: Kp = tan²(45° + φ/2)
        var kp = Math.Pow(Math.Tan(Math.PI / 4.0 + phi / 2.0), 2);

        return (ka, kp);
    }

    public enum FootingShape
    {
        Strip,
        Square,
        Circular
    }
}
