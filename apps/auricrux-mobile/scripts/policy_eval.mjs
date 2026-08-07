import fs from "node:fs";
import path from "node:path";

const appRoot = process.cwd();
const policy = JSON.parse(fs.readFileSync(path.join(appRoot, "policy/construction_policy_pack.json"), "utf8"));
const appTsx = fs.readFileSync(path.join(appRoot, "App.tsx"), "utf8");
const envelopeTs = fs.readFileSync(path.join(appRoot, "src/contracts/envelope.ts"), "utf8");
const failures = [];

if (!envelopeTs.includes(`route: "${policy.required.route}"`)) {
  failures.push(`envelope.ts must pin route to ${policy.required.route}`);
}

if (!envelopeTs.includes(`source: "${policy.required.source}"`)) {
  failures.push(`envelope.ts must pin source to ${policy.required.source}`);
}

if (!envelopeTs.includes(`specialistAgent: "${policy.required.specialistAgent}"`)) {
  failures.push(`envelope.ts must pin specialistAgent to ${policy.required.specialistAgent}`);
}

for (const header of policy.required.requestHeaders) {
  if (!appTsx.includes(`"${header}"`)) {
    failures.push(`App.tsx must set request header ${header}`);
  }
}

for (const snippet of policy.forbidden.routeSnippets) {
  if (appTsx.includes(snippet)) {
    failures.push(`App.tsx contains forbidden route snippet: ${snippet}`);
  }
}

if (failures.length > 0) {
  console.error("policy_eval failed:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

console.log("policy_eval passed.");
