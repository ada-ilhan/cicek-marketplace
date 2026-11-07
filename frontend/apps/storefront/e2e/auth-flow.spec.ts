import { expect, test } from "@playwright/test";

// Full stack: storefront → BFF → ASP.NET Core API → Oracle. Enabled with E2E_FULL_STACK=1.
test.skip(!process.env.E2E_FULL_STACK, "Requires the API and Oracle (set E2E_FULL_STACK=1).");

test("register, view account, sign out", async ({ page, context }) => {
  const email = `e2e.${Date.now()}@example.com`;

  await page.goto("/register");
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^Password/).fill("correct-horse-battery");
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByText(email)).toBeVisible();

  const session = (await context.cookies()).filter(
    (c) => c.name.endsWith("cicek_at") || c.name.endsWith("cicek_rt"),
  );
  expect(session).toHaveLength(2);
  expect(session.every((c) => c.httpOnly && c.sameSite === "Lax")).toBe(true);
  expect(await page.evaluate(() => document.cookie)).not.toContain("cicek_at");

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/$/);
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login/);
});
