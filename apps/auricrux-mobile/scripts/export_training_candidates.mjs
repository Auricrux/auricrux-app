import fs from "node:fs";
import path from "node:path";

const args = process.argv.slice(2);

function getArg(name, fallback = "") {
  const idx = args.indexOf(name);
  if (idx === -1 || idx + 1 >= args.length) {
    return fallback;
  }
  return args[idx + 1];
}

function ensureDirFor(filePath) {
  const dir = path.dirname(filePath);
  fs.mkdirSync(dir, { recursive: true });
}

const inputPath = getArg("--in", "./data/training/conversation_events.sample.jsonl");
const outputPath = getArg("--out", "./artifacts/training/candidates.jsonl");
const summaryPath = getArg("--summary", "./artifacts/training/summary.json");

const lines = fs
  .readFileSync(inputPath, "utf8")
  .split(/\r?\n/)
  .map((line) => line.trim())
  .filter(Boolean);

let parsed = 0;
let selected = 0;
const rejected = {
  missingFields: 0,
  notPositiveFeedback: 0,
  safetyBlocked: 0,
  notConstruction: 0,
};

const candidates = [];

for (const line of lines) {
  parsed += 1;
  let event;
  try {
    event = JSON.parse(line);
  } catch {
    rejected.missingFields += 1;
    continue;
  }

  const prompt = String(event.prompt ?? "").trim();
  const reply = String(event.reply ?? "").trim();
  const feedback = String(event.feedbackRating ?? "").trim().toLowerCase();
  const safetyOutcome = String(event.safetyOutcome ?? "").trim().toLowerCase();
  const constructionDomain = Boolean(event.constructionDomain);

  if (!prompt || !reply) {
    rejected.missingFields += 1;
    continue;
  }

  if (feedback !== "up") {
    rejected.notPositiveFeedback += 1;
    continue;
  }

  if (safetyOutcome !== "safe") {
    rejected.safetyBlocked += 1;
    continue;
  }

  if (!constructionDomain) {
    rejected.notConstruction += 1;
    continue;
  }

  selected += 1;
  candidates.push({
    instruction: "Provide a construction-only expert answer with practical, safety-aware guidance.",
    input: prompt,
    output: reply,
    metadata: {
      traceId: event.traceId ?? "",
      sessionId: event.sessionId ?? "",
      route: "/construction",
      source: "auricrux-mobile",
      seedTags: Array.isArray(event.seedTags) ? event.seedTags : [],
      exportedAtUtc: new Date().toISOString(),
    },
  });
}

ensureDirFor(outputPath);
ensureDirFor(summaryPath);

fs.writeFileSync(outputPath, candidates.map((item) => JSON.stringify(item)).join("\n") + (candidates.length > 0 ? "\n" : ""), "utf8");

const summary = {
  generatedAtUtc: new Date().toISOString(),
  inputPath,
  outputPath,
  parsed,
  selected,
  rejected,
};

fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2), "utf8");

console.log(`export_training_candidates completed: selected ${selected}/${parsed}`);
