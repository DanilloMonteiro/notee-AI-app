// Executa todas as suítes *.test.mjs em sequência e falha se alguma falhar.
import { readdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = dirname(fileURLToPath(import.meta.url));
let failed = 0;
for (const file of readdirSync(dir).filter(f => f.endsWith('.test.mjs')).sort()) {
  console.log(`\n=== ${file}`);
  const r = spawnSync(process.execPath, [join(dir, file)], { stdio: 'inherit', env: process.env });
  if (r.status !== 0) failed++;
}
console.log(failed ? `\n${failed} suíte(s) com falha` : '\nTodas as suítes passaram');
process.exit(failed ? 1 : 0);
