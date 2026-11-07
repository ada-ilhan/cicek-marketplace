import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e",
  globalSetup: "./e2e/global-setup.ts",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL,
    trace: "retain-on-failure",
    // Tests run in English so selectors do not depend on the market default (Azerbaijani). Locale behaviour has its own test.
    storageState: {
      cookies: [
        {
          name: "cicek_locale",
          value: "en",
          domain: new URL(baseURL).hostname,
          path: "/",
          expires: -1,
          httpOnly: false,
          secure: false,
          sameSite: "Lax",
        },
      ],
      origins: [],
    },
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] }, grepInvert: /@real-browser/ },
    // Playwright's bundled Chromium ships without Azerbaijani ICU data. Formatting for the market locale is therefore
    // verified in the installed Google Chrome (npx playwright test --project=chrome).
    { name: "chrome", use: { ...devices["Desktop Chrome"], channel: "chrome" }, grep: /@real-browser/ },
  ],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : { command: "npm run start", url: baseURL, reuseExistingServer: true, timeout: 120_000 },
});
