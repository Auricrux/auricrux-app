import fs from "node:fs";
import path from "node:path";

const appRoot = process.cwd();
const schema = JSON.parse(fs.readFileSync(path.join(appRoot, "policy/feedback_event.schema.json"), "utf8"));
const fixture = JSON.parse(fs.readFileSync(path.join(appRoot, "policy/fixtures/feedback_event.valid.json"), "utf8"));

const failures = [];

function requireKey(obj, key, label) {
  if (!(key in obj)) {
    failures.push(`Missing ${label}.${key}`);
  }
}

for (const key of schema.required) {
  requireKey(fixture, key, "root");
}

if (fixture.rating !== "up" && fixture.rating !== "down") {
  failures.push("rating must be up or down");
}

if (fixture.route !== "/construction") {
  failures.push("route must be /construction");
}

if (typeof fixture.message !== "string" || fixture.message.trim().length === 0) {
  failures.push("message must be a non-empty string");
}

if (typeof fixture.reply !== "string" || fixture.reply.trim().length === 0) {
  failures.push("reply must be a non-empty string");
}

if (typeof fixture.context !== "object" || fixture.context === null) {
  failures.push("context must be an object");
} else {
  const requiredContext = schema.properties.context.required;
  for (const key of requiredContext) {
    requireKey(fixture.context, key, "context");
  }

  const allowedThinking = new Set(["quick", "deep", "auto"]);
  if (!allowedThinking.has(fixture.context.thinkingMode)) {
    failures.push("context.thinkingMode must be quick, deep, or auto");
  }

  const allowedSearch = new Set(["internal", "public", "both"]);
  if (!allowedSearch.has(fixture.context.searchScope)) {
    failures.push("context.searchScope must be internal, public, or both");
  }

  if (fixture.context.source !== "auricrux-mobile") {
    failures.push("context.source must be auricrux-mobile");
  }

  if (fixture.context.route !== "/construction") {
    failures.push("context.route must be /construction");
  }

  if (fixture.context.specialistAgent !== "construction-expert") {
    failures.push("context.specialistAgent must be construction-expert");
  }
}

if (failures.length > 0) {
  console.error("feedback_schema_eval failed:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

console.log("feedback_schema_eval passed.");
