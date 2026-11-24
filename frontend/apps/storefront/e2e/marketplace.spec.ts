import { expect, test, type Browser, type Page } from "@playwright/test";

// Full stack: storefront → BFF → ASP.NET Core API → Oracle. Every role acts through the UI (no API shortcuts).
// Enable with E2E_FULL_STACK=1 plus an administrator account (E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD; see README).
test.skip(
  !process.env.E2E_FULL_STACK || !process.env.E2E_ADMIN_EMAIL || !process.env.E2E_ADMIN_PASSWORD,
  "Requires the API, Oracle and an administrator account.",
);

const PASSWORD = "correct-horse-battery";

/** Matches a form label exactly, allowing the visual " *" required marker. */
const label = (text: string) => new RegExp(`^${text}( \\*)?$`);

async function newUser(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

async function register(page: Page, email: string) {
  await page.goto("/register");
  await page.getByLabel(label("Email")).fill(email);
  await page.getByLabel(label("Password")).fill(PASSWORD);
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page).toHaveURL(/\/account$/);
}

async function login(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel(label("Email")).fill(email);
  await page.getByLabel(label("Password")).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/account$/);
}

function tomorrow() {
  return new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

test("complete marketplace journey: seller, administrator and customer panels", async ({ browser }) => {
  // One long multi-actor journey (about 170 steps, four browser contexts). Every step is fast, but the sum is close to the
  // 30 s default: on a slower runner it ran out of budget at the last steps (CI run 45464006518), without any hung step.
  test.setTimeout(120_000);
  const marker = Date.now().toString(36);
  const shopName = `E2E Florist ${marker}`;
  const productName = `E2E Roses ${marker}`;

  // ---- 1. A visitor can already browse public pages -------------------------------------------------------------
  const visitor = await newUser(browser);
  await visitor.goto("/shops");
  await expect(visitor.getByRole("heading", { name: "Florists", level: 1 })).toBeVisible();
  await visitor.goto("/products");
  await expect(visitor.getByRole("heading", { name: "Products", level: 1 })).toBeVisible();

  // ---- 2. Florist applies and sees the approval status ---------------------------------------------------------
  const seller = await newUser(browser);
  await register(seller, `seller.${marker}@example.com`);
  await seller.goto("/seller");
  await seller.getByLabel(label("Florist name")).fill(shopName);
  await seller.getByLabel(label("Contact email")).fill(`shop.${marker}@example.com`);
  await seller.getByLabel(label("Contact phone")).fill("+994 12 000 00 00");
  await seller.getByLabel(label("Address")).fill("Nizami küç. 1");
  await seller.getByLabel(label("District")).fill("Nasimi");
  await seller.getByLabel(label("City")).fill("Baku");
  await seller.getByRole("button", { name: "Submit application" }).click();
  await expect(seller.getByText("awaiting approval", { exact: false }).first()).toBeVisible();
  await seller.getByRole("link", { name: "Manage" }).click();
  await expect(seller).toHaveURL(/\/seller\/shops\/[0-9a-f-]+$/);
  const shopId = seller.url().split("/").pop()!;

  // The pending shop is not public.
  await visitor.goto(`/shops?q=${marker}`);
  await expect(visitor.getByText("No florists found")).toBeVisible();

  // ---- 3. Administrator rejects with a reason, florist corrects and resubmits, administrator approves ------------
  const admin = await newUser(browser);
  await login(admin, process.env.E2E_ADMIN_EMAIL!, process.env.E2E_ADMIN_PASSWORD!);
  await admin.goto("/admin");
  await expect(admin.getByRole("navigation", { name: "Administration" })).toBeVisible();
  await expect(admin.getByText("Applications awaiting approval")).toBeVisible();

  await admin.goto(`/admin/shops/${shopId}`);
  await expect(admin.getByRole("heading", { name: shopName })).toBeVisible();
  await admin.getByRole("button", { name: "Reject" }).click();
  await expect(admin.getByRole("dialog", { name: "Reject this application?" })).toBeVisible();
  await admin.getByRole("button", { name: "Confirm" }).click();
  await expect(admin.getByText("A reason is required.")).toBeVisible(); // validation inside the dialog
  await admin.getByLabel("Reason (shown to the florist)").fill("Please add the full street address");
  await admin.getByRole("button", { name: "Confirm" }).click();
  await expect(admin.getByText("Done.")).toBeVisible();

  await seller.reload();
  await expect(
    seller.getByText("Please add the full street address", { exact: false }).first(),
  ).toBeVisible();
  await seller.getByLabel(label("Address")).fill("Nizami küç. 1, bina 5");
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller.getByText("Details saved.")).toBeVisible();
  await seller.getByRole("button", { name: "Apply again" }).click();
  await expect(seller.getByText("awaiting approval", { exact: false }).first()).toBeVisible();

  await admin.reload();
  await admin.getByRole("button", { name: "Approve" }).click();
  await admin.getByRole("button", { name: "Confirm" }).click();
  await expect(admin.getByText("Done.")).toBeVisible();

  // ---- 4. Approved florist adds and edits a product -------------------------------------------------------------
  await seller.reload();
  await expect(seller.getByRole("button", { name: "Close temporarily" })).toBeVisible();
  await seller.goto(`/seller/shops/${shopId}/products/new`);
  await seller.getByLabel(label("Category")).selectOption({ label: "Qızılgüllər" });
  await seller.getByLabel(label("Product name")).fill(productName);
  await seller.getByLabel(label("Price")).fill("0");
  await seller.getByLabel(label("Stock quantity")).fill("5");
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller.getByText("This field is required.").first()).toBeVisible(); // validation error shown

  await seller.getByLabel(label("Price")).fill("45.50");
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller).toHaveURL(new RegExp(`/seller/shops/${shopId}/products/[0-9a-f-]+$`));
  await seller.getByLabel(/^Price/).fill("48");
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller.getByText("Saved.")).toBeVisible();

  // ---- 5. Customer browses the shop, buys, and sees the order ---------------------------------------------------
  const customer = await newUser(browser);
  const customerEmail = `customer.${marker}@example.com`;
  await register(customer, customerEmail);
  await customer.goto(`/shops?q=${marker}`);
  await customer.getByRole("link", { name: shopName }).click();
  await expect(customer.getByRole("heading", { name: shopName, level: 1 })).toBeVisible();
  await customer.getByRole("link", { name: new RegExp(productName) }).click();
  await expect(customer.getByText("In stock")).toBeVisible();
  await customer.getByLabel("Quantity").selectOption("2");
  await customer.getByRole("button", { name: "Add to cart" }).click();
  await expect(customer.getByText("Added to your cart.")).toBeVisible();

  await customer.goto("/cart");
  await expect(customer.getByText(productName)).toBeVisible();
  await expect(customer.getByText(/96[.,]00/).first()).toBeVisible(); // 2 × 48 computed by the server
  await customer.getByRole("link", { name: "Proceed to checkout" }).click();
  await customer.getByRole("button", { name: "Place order" }).click();
  await expect(customer.getByText("This field is required.").first()).toBeVisible(); // empty delivery form
  await customer.getByLabel(label("Recipient name")).fill("Aysel Mammadova");
  await customer.getByLabel(label("Recipient phone")).fill("+994 50 000 00 00");
  await customer.getByLabel(label("Address")).fill("Fuzuli küç. 5");
  await customer.getByLabel(label("District")).fill("Yasamal");
  await customer.getByLabel(label("City"), { exact: true }).fill("Baku");
  await customer.getByLabel(label("Delivery date")).fill(tomorrow());
  await customer.getByRole("button", { name: "Place order" }).click();
  await expect(customer.getByText("Order received")).toBeVisible();
  const orderLink = customer.getByRole("link", { name: /^CS\d{6}-/ });
  const orderNumber = (await orderLink.textContent())!.trim();

  await customer.goto("/account/orders");
  await expect(customer.getByRole("link", { name: orderNumber })).toBeVisible();
  await customer.goto(`/account/orders/${orderNumber}`);
  await expect(customer.getByText("Payment not received yet")).toBeVisible();
  await expect(customer.getByText("Online payment is not enabled yet", { exact: false })).toBeVisible();

  // ---- 6. Administrator records the manual payment through the UI -----------------------------------------------
  await admin.goto(`/admin/orders/${orderNumber}`);
  await expect(admin.getByText("does not take a real payment", { exact: false })).toBeVisible();
  await admin.getByLabel(label("Payment reference")).fill(`E2E-${marker}`);
  await admin.getByRole("button", { name: "Record payment manually" }).click();
  await expect(admin.getByText("Done.")).toBeVisible();

  // ---- 7. Florist processes the order; the customer sees each status --------------------------------------------
  await seller.goto(`/seller/shops/${shopId}/orders/${orderNumber}`);
  await expect(seller.getByText("Platform commission", { exact: false })).toBeVisible();
  for (const [action, status] of [
    ["Accept order", "Accepted"],
    ["Start preparing", "Preparing"],
    ["Ready for delivery", "Ready for delivery"],
    ["Delivered", "Delivered"],
  ] as const) {
    await seller.getByRole("button", { name: action }).click();
    // Accepting and delivering ask for confirmation first (a consequential action).
    const dialog = seller.getByRole("dialog");
    if (action === "Accept order" || action === "Delivered") {
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: action }).click();
    }
    await expect(seller.getByText(status).first()).toBeVisible();
  }

  await customer.reload();
  await expect(customer.getByText("Delivered").first()).toBeVisible();
  await expect(customer.getByText("not courier tracking", { exact: false })).toBeVisible();
  await expect(customer.getByText("Platform commission", { exact: false })).toHaveCount(0); // never shown to customers

  // ---- 8. Isolation: other customers, non-owners and non-admins are refused -------------------------------------
  const stranger = await newUser(browser);
  await register(stranger, `stranger.${marker}@example.com`);
  await stranger.goto(`/account/orders/${orderNumber}`);
  await expect(stranger.getByText("Order not found")).toBeVisible();
  await stranger.goto(`/seller/shops/${shopId}`);
  await expect(stranger.getByText("No florists found")).toBeVisible();
  await stranger.goto("/admin");
  await expect(stranger.getByText("Access denied")).toBeVisible();
  await stranger.goto("/admin/users");
  await expect(stranger.getByText("Access denied")).toBeVisible();

  // ---- 9. Administrator manages categories, views users and the dashboard ---------------------------------------
  await admin.goto("/admin/categories");
  await admin.getByRole("textbox", { name: label("Name") }).fill(`E2E Category ${marker}`);
  await admin.getByRole("button", { name: "Add category" }).last().click();
  await expect(admin.getByText("Category saved.")).toBeVisible();
  await expect(admin.getByRole("cell", { name: `E2E Category ${marker}` })).toBeVisible();

  await admin.goto("/admin/users");
  await admin.getByLabel("Search email").fill(customerEmail);
  await admin.getByRole("button", { name: "Apply" }).click();
  await expect(admin.getByRole("cell", { name: customerEmail })).toBeVisible();

  await admin.goto("/admin");
  await expect(admin.getByText("Recent orders")).toBeVisible();
  await expect(admin.getByRole("link", { name: orderNumber })).toBeVisible();

  // The public listing now shows the approved florist and its product.
  await visitor.goto(`/products?q=${marker}`);
  await expect(visitor.getByText(productName)).toBeVisible();
});
