// Readiness gate for every Playwright run. "Container healthy" only proves each service answers inside its own container; the
// tests need the whole path to work through the PUBLISHED port: browser -> storefront -> BFF -> API -> Oracle. Probing it up
// front turns "the first tests failed because the stack was still settling" into one clear, bounded failure (or a pass).
// It waits for consecutive successes rather than a single one, because a freshly started stack can answer once and then stall.
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";
// The API leg is only required for full-stack runs; the storefront-only smoke run has no API behind it.
const FULL_STACK = Boolean(process.env.E2E_FULL_STACK);
const PROBES = [
  { path: "/login", expect: "storefront renders" },
  ...(FULL_STACK ? [{ path: "/api/bff/v1/categories", expect: "storefront -> API -> Oracle answers" }] : []),
];
const REQUIRED_CONSECUTIVE = 5;
const DEADLINE_MS = 180_000;

async function probe(path: string): Promise<boolean> {
  try {
    const response = await fetch(new URL(path, baseURL), { signal: AbortSignal.timeout(5_000) });
    await response.arrayBuffer();
    return response.ok;
  } catch {
    return false;
  }
}

export default async function globalSetup(): Promise<void> {
  const started = Date.now();
  let consecutive = 0;
  let failures = 0;
  while (consecutive < REQUIRED_CONSECUTIVE) {
    if (Date.now() - started > DEADLINE_MS) {
      throw new Error(
        `The stack at ${baseURL} was not ready after ${DEADLINE_MS / 1000}s ` +
          `(${consecutive}/${REQUIRED_CONSECUTIVE} consecutive probes, ${failures} failed probes). ` +
          `Probed: ${PROBES.map((p) => `${p.path} (${p.expect})`).join(", ")}.`,
      );
    }
    const results = await Promise.all(PROBES.map((p) => probe(p.path)));
    if (results.every(Boolean)) {
      consecutive += 1;
    } else {
      consecutive = 0;
      failures += 1;
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  console.log(`Stack ready after ${((Date.now() - started) / 1000).toFixed(1)}s (${failures} failed probes)`);
}
