namespace Auricrux.Web.Services.Breakthrough.Physics;

/// <summary>
/// Structural Engineering Physics Model
/// 
/// Implements structural mechanics for:
/// - Beam bending (stress, deflection)
/// - Column stability (Euler buckling, slenderness)
/// - Steel member capacity (AISC LRFD)
/// - Connection design (bolts, welds)
/// - Load combinations (ASCE 7)
/// 
/// Based on: AISC 360, AISC Manual 15th Ed, Timoshenko
/// </summary>
public static class StructuralPhysicsModel
{
    /// <summary>
    /// Calculate maximum bending stress in a beam
    /// σ = M×y / I (flexure formula)
    /// </summary>
    public static double BeamBendingStress(
        double momentInchLbs,
        double distanceFromNeutralAxisInches,
        double momentOfInertiaIn4)
    {
        // σ = M×c / I
        return (momentInchLbs * distanceFromNeutralAxisInches) / momentOfInertiaIn4;
    }

    /// <summary>
    /// Calculate maximum deflection for simply supported beam with uniform load
    /// δ = (5×w×L⁴) / (384×E×I)
    /// </summary>
    public static double BeamDeflectionUniformLoad(
        double uniformLoadPlf,
        double spanFt,
        double elasticModulusPsi,
        double momentOfInertiaIn4)
    {
        var spanInches = spanFt * 12;
        var wPli = uniformLoadPlf / 12; // Convert to lbs/inch

        // δ = (5×w×L⁴) / (384×E×I)
        var deflectionInches = (5 * wPli * Math.Pow(spanInches, 4))
                              / (384 * elasticModulusPsi * momentOfInertiaIn4);

        return deflectionInches;
    }

    /// <summary>
    /// Check beam deflection against span/360 (typical live load limit)
    /// </summary>
    public static bool IsDeflectionAcceptable(
        double deflectionInches,
        double spanFt,
        double allowableRatio = 360)
    {
        var allowableDeflectionInches = (spanFt * 12) / allowableRatio;
        return deflectionInches <= allowableDeflectionInches;
    }

    /// <summary>
    /// Euler critical buckling load for column
    /// Pcr = (π²×E×I) / (K×L)²
    /// </summary>
    public static double EulerBucklingLoad(
        double elasticModulusPsi,
        double momentOfInertiaIn4,
        double lengthInches,
        double effectiveLengthFactor = 1.0)
    {
        var kl = effectiveLengthFactor * lengthInches;

        // Pcr = (π²×E×I) / (K×L)²
        var pCritical = (Math.Pow(Math.PI, 2) * elasticModulusPsi * momentOfInertiaIn4)
                       / Math.Pow(kl, 2);

        return pCritical;
    }

    /// <summary>
    /// Column slenderness ratio check
    /// </summary>
    public static (double SlendernessRatio, bool IsShortColumn) ColumnSlenderness(
        double effectiveLengthInches,
        double radiusOfGyrationInches,
        double shortColumnLimit = 22)
    {
        var slenderness = effectiveLengthInches / radiusOfGyrationInches;
        var isShort = slenderness <= shortColumnLimit;

        return (slenderness, isShort);
    }

    /// <summary>
    /// Steel beam nominal flexural strength (AISC 360 Chapter F)
    /// Simplified for compact sections
    /// </summary>
    public static double SteelBeamFlexuralStrength(
        double yieldStressPsi,
        double plasticSectionModulusIn3,
        bool isCompactSection = true,
        double lateralTorsionalBucklingModifierCb = 1.0)
    {
        if (isCompactSection)
        {
            // Mn = Mp = Fy × Zx (plastic moment)
            return yieldStressPsi * plasticSectionModulusIn3;
        }
        else
        {
            // Simplified: reduce by Cb factor for LTB
            return yieldStressPsi * plasticSectionModulusIn3 * lateralTorsionalBucklingModifierCb;
        }
    }

    /// <summary>
    /// Design flexural strength with resistance factor
    /// φMn (AISC 360)
    /// </summary>
    public static double DesignFlexuralStrength(
        double nominalStrengthInchLbs,
        double phiFactor = 0.90)
    {
        return phiFactor * nominalStrengthInchLbs;
    }

    /// <summary>
    /// Steel column nominal compressive strength (AISC 360 Chapter E)
    /// Simplified for non-slender sections
    /// </summary>
    public static double SteelColumnCompressiveStrength(
        double yieldStressPsi,
        double elasticModulusPsi,
        double grossAreaIn2,
        double effectiveLengthInches,
        double radiusOfGyrationInches)
    {
        // Slenderness parameter
        var kl_r = effectiveLengthInches / radiusOfGyrationInches;
        var fe = (Math.Pow(Math.PI, 2) * elasticModulusPsi) / Math.Pow(kl_r, 2);

        double fcr; // Critical stress

        if (kl_r <= 4.71 * Math.Sqrt(elasticModulusPsi / yieldStressPsi))
        {
            // Inelastic buckling
            var ratio = yieldStressPsi / fe;
            fcr = Math.Pow(0.658, ratio) * yieldStressPsi;
        }
        else
        {
            // Elastic buckling
            fcr = 0.877 * fe;
        }

        // Pn = Fcr × Ag
        return fcr * grossAreaIn2;
    }

    /// <summary>
    /// Design compressive strength with resistance factor
    /// φPn (AISC 360)
    /// </summary>
    public static double DesignCompressiveStrength(
        double nominalStrengthLbs,
        double phiFactor = 0.90)
    {
        return phiFactor * nominalStrengthLbs;
    }

    /// <summary>
    /// Single bolt shear capacity (AISC 360 Chapter J)
    /// </summary>
    public static double BoltShearCapacity(
        double boltDiameterInches,
        double ultimateTensileStressPsi,
        int numberOfShearPlanes = 1,
        double phiFactor = 0.75)
    {
        var nominalAreaIn2 = Math.PI * Math.Pow(boltDiameterInches / 2.0, 2);

        // Rn = Fnv × Ab × ns
        // Fnv = 0.45 × Fub (for threads included in shear plane)
        var fnv = 0.45 * ultimateTensileStressPsi;
        var rn = fnv * nominalAreaIn2 * numberOfShearPlanes;

        // φRn
        return phiFactor * rn;
    }

    /// <summary>
    /// Load combination factors (ASCE 7-22 Section 2.3)
    /// </summary>
    public static double LoadCombination(
        double deadLoad,
        double liveLoad,
        double snowLoad = 0,
        double windLoad = 0,
        double earthquakeLoad = 0,
        LoadCombinationCase loadCase = LoadCombinationCase.LRFD_1_4D)
    {
        return loadCase switch
        {
            LoadCombinationCase.LRFD_1_4D => 1.4 * deadLoad,
            LoadCombinationCase.LRFD_1_2D_1_6L => 1.2 * deadLoad + 1.6 * liveLoad,
            LoadCombinationCase.LRFD_1_2D_1_6L_0_5S => 1.2 * deadLoad + 1.6 * liveLoad + 0.5 * snowLoad,
            LoadCombinationCase.LRFD_1_2D_1_0L_1_0W => 1.2 * deadLoad + 1.0 * liveLoad + 1.0 * windLoad,
            LoadCombinationCase.LRFD_1_2D_1_0E_0_5L => 1.2 * deadLoad + 1.0 * earthquakeLoad + 0.5 * liveLoad,
            LoadCombinationCase.LRFD_0_9D_1_0W => 0.9 * deadLoad + 1.0 * windLoad,
            _ => deadLoad + liveLoad
        };
    }

    public enum LoadCombinationCase
    {
        LRFD_1_4D,
        LRFD_1_2D_1_6L,
        LRFD_1_2D_1_6L_0_5S,
        LRFD_1_2D_1_0L_1_0W,
        LRFD_1_2D_1_0E_0_5L,
        LRFD_0_9D_1_0W
    }
}
