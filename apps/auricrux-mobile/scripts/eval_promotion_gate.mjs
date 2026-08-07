import fs from "node:fs";

const args = process.argv.slice(2);

function getArg(name, fallback = "") {
  const idx = args.indexOf(name);
  if (idx === -1 || idx + 1 >= args.length) {
    return fallback;
  }
  return args[idx + 1];
}

const baselinePath = getArg("--baseline", "./eval/baseline_metrics.json");
const candidatePath = getArg("--candidate", "./eval/candidate_metrics.json");

const baseline = JSON.parse(fs.readFileSync(baselinePath, "utf8"));
const candidate = JSON.parse(fs.readFileSync(candidatePath, "utf8"));

const checks = [];

function check(name, pass, details) {
  checks.push({ name, pass, details });
}

const overallDelta = candidate.overallQuality - baseline.overallQuality;
check(
  "overall_quality_delta",
  overallDelta >= baseline.thresholds.minOverallDelta,
  { baseline: baseline.overallQuality, candidate: candidate.overallQuality, delta: overallDelta, min: baseline.thresholds.minOverallDelta }
);

check(
  "safety_floor",
  candidate.safetyPassRate >= baseline.thresholds.minSafetyPassRate,
  { baseline: baseline.safetyPassRate, candidate: candidate.safetyPassRate, min: baseline.thresholds.minSafetyPassRate }
);

check(
  "grounding_non_regression",
  candidate.groundingScore >= baseline.groundingScore,
  { baseline: baseline.groundingScore, candidate: candidate.groundingScore }
);

const maxLatency = Math.round(baseline.latencyP95Ms * baseline.thresholds.maxLatencyMultiplier);
check(
  "latency_guardrail",
  candidate.latencyP95Ms <= maxLatency,
  { baseline: baseline.latencyP95Ms, candidate: candidate.latencyP95Ms, maxAllowed: maxLatency }
);

const failed = checks.filter((item) => !item.pass);
const report = {
  generatedAtUtc: new Date().toISOString(),
  baselinePath,
  candidatePath,
  checks,
  passed: failed.length === 0,
};

fs.mkdirSync("./artifacts/eval", { recursive: true });
fs.writeFileSync("./artifacts/eval/promotion_gate_report.json", JSON.stringify(report, null, 2), "utf8");

if (failed.length > 0) {
  console.error("eval_promotion_gate failed:");
  for (const item of failed) {
    console.error(`- ${item.name}`);
  }
  process.exit(1);
}

console.log("eval_promotion_gate passed.");
