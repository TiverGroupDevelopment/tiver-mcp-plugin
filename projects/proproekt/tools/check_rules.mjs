// Проверка готовых семейств ПроПроекта командой плагина check_family_rules с проверками профиля proproekt
// (те же, что отдаёт инструмент сервера check_family_rules profile: "proproekt").
//   node check_rules.mjs <jobs.json> <rules.jsonl> [port]
import net from "node:net";
import { readFileSync, appendFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const rules = await import(join(here, "../../../server/build/utils/familyRules.js").replace(/\\/g, "/").replace(/^([A-Z]):/, "file:///$1:"));

const [jobsPath, outPath, portArg] = process.argv.slice(2);
const port = Number(portArg || 8080);
const { resolved } = rules.resolveRequestedProfile("proproekt");
const { checks, missingInFop } = rules.checksWithGuids(resolved);
if (missingInFop.length) console.error("Нет в ФОП:", missingInFop.join(", "));

function call(method, params, timeoutMs = 120000) {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket();
    let buffer = "";
    const timer = setTimeout(() => { socket.destroy(); reject(new Error("timeout")); }, timeoutMs);
    socket.on("data", (d) => {
      buffer += d.toString("utf8");
      try {
        const r = JSON.parse(buffer);
        clearTimeout(timer);
        socket.destroy();
        resolve(r);
      } catch { /* ждём остаток */ }
    });
    socket.on("error", (e) => { clearTimeout(timer); reject(e); });
    socket.connect(port, "127.0.0.1", () => socket.write(JSON.stringify({ jsonrpc: "2.0", method, params, id: String(Date.now()) })));
  });
}

const jobs = JSON.parse(readFileSync(jobsPath, "utf8"));
writeFileSync(outPath, "");
let n = 0;
for (const job of jobs) {
  let row;
  try {
    const r = await call("check_family_rules", { checks, path: job.output });
    row = { family: job.family, response: r.error ? { error: r.error } : r.result };
  } catch (e) {
    row = { family: job.family, response: { error: String(e) } };
  }
  appendFileSync(outPath, JSON.stringify(row) + "\n");
  n++;
}
console.log(`проверено ${n}`);
