using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Auricrux.Web.Services;

/// <summary>
/// Deterministic construction calculator — honest "code interpreter lite"
/// (not a sandboxed Python runtime). Covers volume, rebar, board-feet, percent, unit convert.
/// </summary>
public sealed class ConstructionCalculatorService
{
    private static readonly Dictionary<string, double> RebarLbPerFt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#3"] = 0.376, ["3"] = 0.376,
        ["#4"] = 0.668, ["4"] = 0.668,
        ["#5"] = 1.043, ["5"] = 1.043,
        ["#6"] = 1.502, ["6"] = 1.502,
        ["#7"] = 2.044, ["7"] = 2.044,
        ["#8"] = 2.670, ["8"] = 2.670,
        ["#9"] = 3.400, ["9"] = 3.400,
        ["#10"] = 4.303, ["10"] = 4.303,
        ["#11"] = 5.313, ["11"] = 5.313
    };

    public CalcResult Evaluate(string operation, IReadOnlyDictionary<string, double> args)
    {
        try
        {
            return operation.Trim().ToLowerInvariant() switch
            {
                "concrete_volume_cy" => ConcreteVolume(args),
                "rebar_weight_lb" => RebarWeight(args),
                "board_feet" => BoardFeet(args),
                "percent_of" => PercentOf(args),
                "unit_convert" => UnitConvert(args),
                _ => CalcResult.Fail($"Unknown operation '{operation}'.")
            };
        }
        catch (Exception ex)
        {
            return CalcResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Parse natural-ish calculator intents from a freeform query when the agent planner is weak.
    /// </summary>
    public CalcResult? TryHeuristic(string query)
    {
        var q = query.ToLowerInvariant();
        var nums = Regex.Matches(q, @"\d+(\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();

        if (q.Contains("concrete") && (q.Contains("yard") || q.Contains("cy") || q.Contains("volume")) && nums.Count >= 3)
        {
            // Prefer LxWxD-in if depth looks like inches (<=24) else feet
            var depthIn = nums[2] <= 24 ? nums[2] : nums[2] * 12;
            return Evaluate("concrete_volume_cy", new Dictionary<string, double>
            {
                ["lengthFt"] = nums[0],
                ["widthFt"] = nums[1],
                ["depthIn"] = depthIn
            });
        }

        if ((q.Contains("rebar") || q.Contains("reinforcing")) && nums.Count >= 2)
        {
            var bar = Regex.Match(q, @"#?\s*(3|4|5|6|7|8|9|10|11)\b");
            var size = bar.Success ? bar.Groups[1].Value : "4";
            var pieces = nums.Count >= 3 ? nums[0] : 1;
            var length = nums.Count >= 3 ? nums[1] : nums[0];
            return Evaluate("rebar_weight_lb", new Dictionary<string, double>
            {
                ["pieces"] = pieces,
                ["lengthFt"] = length,
                ["barSize"] = double.Parse(size, CultureInfo.InvariantCulture)
            });
        }

        if ((q.Contains("board feet") || q.Contains("board-feet") || q.Contains("bf")) && nums.Count >= 3)
        {
            return Evaluate("board_feet", new Dictionary<string, double>
            {
                ["thicknessIn"] = nums[0],
                ["widthIn"] = nums[1],
                ["lengthFt"] = nums[2],
                ["pieces"] = nums.Count >= 4 ? nums[3] : 1
            });
        }

        return null;
    }

    private static CalcResult ConcreteVolume(IReadOnlyDictionary<string, double> args)
    {
        var lengthFt = Req(args, "lengthFt");
        var widthFt = Req(args, "widthFt");
        var depthIn = Req(args, "depthIn");
        var cy = lengthFt * widthFt * (depthIn / 12.0) / 27.0;
        return CalcResult.Ok("concrete_volume_cy", cy, "cy",
            $"{lengthFt:g4} ft × {widthFt:g4} ft × {depthIn:g4} in = {cy:F3} cubic yards");
    }

    private static CalcResult RebarWeight(IReadOnlyDictionary<string, double> args)
    {
        var pieces = Req(args, "pieces");
        var lengthFt = Req(args, "lengthFt");
        var barSize = Req(args, "barSize");
        var key = ((int)barSize).ToString(CultureInfo.InvariantCulture);
        if (!RebarLbPerFt.TryGetValue(key, out var lbPerFt) && !RebarLbPerFt.TryGetValue("#" + key, out lbPerFt))
        {
            return CalcResult.Fail($"Unsupported bar size #{key}.");
        }

        var lb = pieces * lengthFt * lbPerFt;
        return CalcResult.Ok("rebar_weight_lb", lb, "lb",
            $"{pieces:g4} pcs × {lengthFt:g4} ft × #{key} ({lbPerFt} lb/ft) = {lb:F1} lb");
    }

    private static CalcResult BoardFeet(IReadOnlyDictionary<string, double> args)
    {
        var t = Req(args, "thicknessIn");
        var w = Req(args, "widthIn");
        var len = Req(args, "lengthFt");
        var pieces = args.TryGetValue("pieces", out var p) ? p : 1;
        var bf = pieces * (t * w * len) / 12.0;
        return CalcResult.Ok("board_feet", bf, "bf",
            $"{pieces:g4} pcs × {t:g4}\" × {w:g4}\" × {len:g4}' / 12 = {bf:F2} board-feet");
    }

    private static CalcResult PercentOf(IReadOnlyDictionary<string, double> args)
    {
        var amount = Req(args, "amount");
        var percent = Req(args, "percent");
        var value = amount * (percent / 100.0);
        return CalcResult.Ok("percent_of", value, "value",
            $"{percent:g4}% of {amount:g6} = {value:g6}");
    }

    private static CalcResult UnitConvert(IReadOnlyDictionary<string, double> args)
    {
        // args: value, fromCode encoded as number map via string keys in JSON path — use From/To via side channel
        throw new InvalidOperationException("Use ConvertUnits(value, from, to) overload.");
    }

    public CalcResult ConvertUnits(double value, string from, string to)
    {
        var f = from.Trim().ToLowerInvariant();
        var t = to.Trim().ToLowerInvariant();
        var table = new Dictionary<(string, string), double>
        {
            [("ft", "m")] = 0.3048,
            [("m", "ft")] = 1 / 0.3048,
            [("in", "mm")] = 25.4,
            [("mm", "in")] = 1 / 25.4,
            [("lb", "kg")] = 0.45359237,
            [("kg", "lb")] = 1 / 0.45359237,
            [("cy", "cf")] = 27,
            [("cf", "cy")] = 1 / 27.0,
            [("sf", "sm")] = 0.09290304,
            [("sm", "sf")] = 1 / 0.09290304
        };

        if (f == t)
        {
            return CalcResult.Ok("unit_convert", value, t, $"{value} {f} = {value} {t}");
        }

        if (!table.TryGetValue((f, t), out var factor))
        {
            return CalcResult.Fail($"Unsupported conversion {f}→{t}.");
        }

        var result = value * factor;
        return CalcResult.Ok("unit_convert", result, t, $"{value} {f} = {result:g6} {t}");
    }

    private static double Req(IReadOnlyDictionary<string, double> args, string key)
    {
        if (!args.TryGetValue(key, out var v))
        {
            throw new ArgumentException($"Missing argument '{key}'.");
        }

        return v;
    }
}

public sealed class CalcRequest
{
    public string Operation { get; set; } = "";
    public Dictionary<string, double>? Args { get; set; }
    public double? Value { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
}

public sealed class CalcResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Operation { get; init; }
    public double? Value { get; init; }
    public string? Unit { get; init; }
    public string? Detail { get; init; }

    public static CalcResult Ok(string op, double value, string unit, string detail) => new()
    {
        Success = true,
        Operation = op,
        Value = value,
        Unit = unit,
        Detail = detail
    };

    public static CalcResult Fail(string error) => new() { Success = false, Error = error };

    public string ToToolText() => Success
        ? JsonSerializer.Serialize(new { success = true, operation = Operation, value = Value, unit = Unit, detail = Detail })
        : JsonSerializer.Serialize(new { success = false, error = Error });
}
