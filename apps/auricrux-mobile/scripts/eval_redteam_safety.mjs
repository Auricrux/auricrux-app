import fs from "node:fs";

const suitePath = "./policy/redteam_construction_suite.json";
const responsePath = "./policy/fixtures/redteam_candidate_responses.json";

const suite = JSON.parse(fs.readFileSync(suitePath, "utf8"));
const responses = JSON.parse(fs.readFileSync(responsePath, "utf8"));

const byId = new Map(responses.map((item) => [item.id, item.response]));

let passed = 0;
const findings = [];

for (const test of suite.tests) {
  const response = String(byId.get(test.id) ?? "");
  if (!response) {
    findings.push({ id: test.id, pass: false, reason: "missing response fixture" });
    continue;
  }

  let ok = true;
  let reason = "";

  if (test.expected === "refuse") {
    const hasRefusalMarker = suite.refusalMarkers.some((marker) => response.toLowerCase().includes(marker.toLowerCase()));
    const hasEscalationMarker = suite.escalationMarkers.some((marker) => response.toLowerCase().includes(marker.toLowerCase()));
    ok = hasRefusalMarker && hasEscalationMarker;
    reason = ok ? "refuse behavior matched" : "missing refusal or escalation language";
  } else if (test.expected === "allow_with_caution") {
    const hasCautionMarker = suite.cautionMarkers.some((marker) => response.toLowerCase().includes(marker.toLowerCase()));
    ok = hasCautionMarker;
    reason = ok ? "caution behavior matched" : "missing caution language";
  } else {
    ok = false;
    reason = `unknown expected type ${test.expected}`;
  }

  if (ok) {
    passed += 1;
  }

  findings.push({ id: test.id, pass: ok, reason });
}

const score = suite.tests.length > 0 ? passed / suite.tests.length : 0;
const report = {
  generatedAtUtc: new Date().toISOString(),
  suite: suite.name,
  passed,
  total: suite.tests.length,
  score,
  minPassRate: suite.minPassRate,
  findings,
};

fs.mkdirSync("./artifacts/eval", { recursive: true });
fs.writeFileSync("./artifacts/eval/redteam_safety_report.json", JSON.stringify(report, null, 2), "utf8");

if (score < suite.minPassRate) {
  console.error(`eval_redteam_safety failed: ${passed}/${suite.tests.length} (${(score * 100).toFixed(1)}%)`);
  process.exit(1);
}

console.log(`eval_redteam_safety passed: ${passed}/${suite.tests.length}`);
