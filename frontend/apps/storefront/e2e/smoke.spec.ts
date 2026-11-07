import { expect, test } from "@playwright/test";

// Runs against the storefront alone (no API needed for navigation, validation and guards).
test("home page renders the shell and navigates to sign in", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.getByRole("navigation", { name: "Main menu" }).getByRole("link", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("private pages redirect anonymous visitors to sign in", async ({ page }) => {
  for (const path of ["/account", "/cart", "/seller", "/admin"]) {
    await page.goto(path);
    await expect(page).toHaveURL(new RegExp(`/login\\?next=${encodeURIComponent(path)}$`));
  }
});

test("registration form validates on the client", async ({ page }) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByLabel(/Email/)).toHaveAttribute("aria-invalid", "true");
});

test("the market default language is Azerbaijani and users can switch language", async ({ browser }) => {
  const context = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const page = await context.newPage();
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("lang", "az");
  await expect(
    page.getByRole("navigation", { name: "Əsas menyu" }).getByRole("link", { name: "Daxil ol" }),
  ).toBeVisible();

  await page.getByLabel("Dil").selectOption("en");
  await expect(
    page.getByRole("navigation", { name: "Main menu" }).getByRole("link", { name: "Sign in" }),
  ).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await context.close();
});
