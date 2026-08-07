import fs from "node:fs";
import path from "node:path";

const appRoot = process.cwd();
const failures = [];

function fileExists(relativePath) {
  return fs.existsSync(path.join(appRoot, relativePath));
}

function readJson(relativePath) {
  const fullPath = path.join(appRoot, relativePath);
  return JSON.parse(fs.readFileSync(fullPath, "utf8"));
}

function readText(relativePath) {
  const fullPath = path.join(appRoot, relativePath);
  return fs.readFileSync(fullPath, "utf8");
}

function requireFile(relativePath, reason) {
  if (!fileExists(relativePath)) {
    failures.push(`Missing required file: ${relativePath} (${reason})`);
  }
}

function checkAppJson() {
  const appJson = readJson("app.json");
  const expo = appJson?.expo ?? {};
  const extra = expo?.extra ?? {};
  const apiBaseUrl = String(extra?.API_BASE_URL ?? "").trim();

  if (!apiBaseUrl.startsWith("https://")) {
    failures.push("app.json expo.extra.API_BASE_URL must use https://");
  }

  if (/localhost|127\.0\.0\.1|10\.0\.2\.2/i.test(apiBaseUrl)) {
    failures.push("app.json expo.extra.API_BASE_URL must not point to local development hosts");
  }

  const releaseTag = String(extra?.RELEASE_TAG ?? "").trim();
  if (!releaseTag) {
    failures.push("app.json expo.extra.RELEASE_TAG must be set");
  }

  const androidPackage = String(expo?.android?.package ?? "").trim();
  if (!androidPackage) {
    failures.push("app.json expo.android.package must be set");
  }

  const iosBundle = String(expo?.ios?.bundleIdentifier ?? "").trim();
  if (!iosBundle) {
    failures.push("app.json expo.ios.bundleIdentifier must be set");
  }
}

function checkAppContracts() {
  const appTsx = readText("App.tsx");
  const envelopeTs = readText("src/contracts/envelope.ts");

  if (!envelopeTs.includes('route: "/construction"')) {
    failures.push("envelope.ts must pin route to /construction for construction-only behavior");
  }

  if (!envelopeTs.includes('source: "auricrux-mobile"')) {
    failures.push("envelope.ts must stamp source=auricrux-mobile for traceability");
  }

  if (!envelopeTs.includes("thinkingMode") || !envelopeTs.includes("searchScope")) {
    failures.push("envelope.ts must include thinkingMode and searchScope in request context");
  }

  if (!appTsx.includes('"x-trace-id"') || !appTsx.includes('"x-auricrux-session-id"')) {
    failures.push("App.tsx must send trace and session headers for auditable requests");
  }
}

function checkPackageScripts() {
  const pkg = readJson("package.json");
  const scripts = pkg?.scripts ?? {};

  const requiredScripts = [
    "typecheck",
    "build:android:ci",
    "quality:gate",
    "eval:policy",
    "eval:feedback-schema",
    "eval:redteam-safety",
    "eval:promotion-gate",
    "export:training-candidates",
    "ci:quality"
  ];
  for (const name of requiredScripts) {
    if (!scripts[name]) {
      failures.push(`package.json is missing required script: ${name}`);
    }
  }
}

function checkRequiredFiles() {
  requireFile("README.md", "app operating guide");
  requireFile("app.json", "runtime identity and release metadata");
  requireFile("eas.json", "release profile configuration");
  requireFile("App.tsx", "application entry point");
  requireFile("scripts/release-now.ps1", "deterministic release entrypoint");
  requireFile("src/contracts/envelope.ts", "typed app envelope and trace schema");
  requireFile("policy/construction_policy_pack.json", "construction-only policy pack");
  requireFile("policy/feedback_event.schema.json", "feedback event schema");
  requireFile("policy/fixtures/feedback_event.valid.json", "schema validation fixture");
  requireFile("policy/redteam_construction_suite.json", "construction safety red-team suite");
  requireFile("policy/fixtures/redteam_candidate_responses.json", "red-team response fixture set");
  requireFile("scripts/export_training_candidates.mjs", "training candidate export pipeline");
  requireFile("scripts/eval_redteam_safety.mjs", "red-team safety evaluator");
  requireFile("scripts/eval_promotion_gate.mjs", "release promotion evaluator");
  requireFile("eval/baseline_metrics.json", "release baseline metrics");
  requireFile("eval/candidate_metrics.json", "release candidate metrics");
  requireFile("data/training/conversation_events.sample.jsonl", "training candidate input sample");
}

function run() {
  checkRequiredFiles();
  checkPackageScripts();
  checkAppJson();
  checkAppContracts();

  if (failures.length > 0) {
    console.error("auricrux-mobile quality gate failed:");
    for (const item of failures) {
      console.error(`- ${item}`);
    }
    process.exit(1);
  }

  console.log("auricrux-mobile quality gate passed.");
}

run();