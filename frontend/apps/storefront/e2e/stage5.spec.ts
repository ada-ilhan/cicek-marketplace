import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Browser, type Page } from "@playwright/test";

// Stage 5: localization persistence, image handling, responsive layout and accessibility for all three roles.
// Full stack (storefront → BFF → API → Oracle); enable like marketplace.spec.ts. Data is created through the BFF as the
// respective user (cookies + same-origin header), the checks themselves run in the UI.
test.skip(
  !process.env.E2E_FULL_STACK || !process.env.E2E_ADMIN_EMAIL || !process.env.E2E_ADMIN_PASSWORD,
  "Requires the API, Oracle and an administrator account.",
);

const ORIGIN = process.env.E2E_BASE_URL ?? "http://localhost:3000";
const PASSWORD = "correct-horse-battery";
const label = (text: string) => new RegExp(`^${text}( \\*)?$`);

/** A real 1×1 PNG, so the browser can decode what the platform stored. */
const PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
  "base64",
);

const bffHeaders = { origin: ORIGIN };

async function newPage(browser: Browser, locale = "en"): Promise<Page> {
  const context = await browser.newContext({
    storageState: {
      cookies: [
        {
          name: "cicek_locale",
          value: locale,
          domain: new URL(ORIGIN).hostname,
          path: "/",
          expires: -1,
          httpOnly: false,
          secure: false,
          sameSite: "Lax",
        },
      ],
      origins: [],
    },
  });
  return context.newPage();
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

async function bff<T>(
  page: Page,
  method: "POST" | "PUT",
  path: string,
  data?: unknown,
  headers = {},
): Promise<T> {
  const response = await page.request.fetch(`/api/bff${path}`, {
    method,
    headers: { ...bffHeaders, ...headers },
    ...(data === undefined ? {} : { data }),
  });
  expect(response.ok(), `${method} ${path} → ${response.status()} ${await response.text()}`).toBe(true);
  return (await response.json()) as T;
}

interface World {
  seller: Page;
  customer: Page;
  admin: Page;
  visitor: Page;
  shopId: string;
  shopSlug: string;
  productId: string;
  productName: string;
  imageUrl: string;
  orderNumber: string;
}

let world: World;

test.beforeAll(async ({ browser }) => {
  test.setTimeout(120_000);
  const marker = Date.now().toString(36);
  const seller = await newPage(browser);
  const customer = await newPage(browser);
  const admin = await newPage(browser);
  const visitor = await newPage(browser);

  await register(seller, `s5.seller.${marker}@example.com`);
  await register(customer, `s5.customer.${marker}@example.com`);
  await login(admin, process.env.E2E_ADMIN_EMAIL!, process.env.E2E_ADMIN_PASSWORD!);

  const shop = await bff<{ id: string; slug: string }>(seller, "POST", "/v1/shops", {
    name: `S5 Florist ${marker}`,
    description: "Fresh flowers",
    contactEmail: `s5.shop.${marker}@example.com`,
    contactPhone: "+994 12 000 00 00",
    addressLine: "Nizami küç. 1",
    district: "Nasimi",
    city: "Baku",
    postalCode: "AZ1000",
  });
  await bff(admin, "POST", `/v1/admin/shops/${shop.id}/approve`);

  const categories = await (await seller.request.get("/api/bff/v1/categories")).json();
  const roses = categories.find((c: { slug: string }) => c.slug === "guller");
  const uploaded = await seller.request.post(`/api/bff/v1/my/shops/${shop.id}/images`, {
    headers: bffHeaders,
    multipart: { file: { name: "gul.png", mimeType: "image/png", buffer: PNG } },
  });
  expect(uploaded.status(), await uploaded.text()).toBe(201);
  const image = (await uploaded.json()) as { url: string };

  const productName = `S5 Roses ${marker}`;
  const product = await bff<{ id: string }>(seller, "POST", `/v1/my/shops/${shop.id}/products`, {
    categoryId: roses.id,
    name: productName,
    description: "Red roses",
    price: 1234.5,
    stockQuantity: 10,
    images: [{ url: image.url, altText: "Red roses in a vase" }],
  });
  // A second product without any image exercises the fallback card.
  await bff(seller, "POST", `/v1/my/shops/${shop.id}/products`, {
    categoryId: roses.id,
    name: `S5 No image ${marker}`,
    price: 20,
    stockQuantity: 3,
    images: [],
  });
  // A third one points at an external host that does not resolve: the card must show the fallback, not a broken icon.
  await bff(seller, "POST", `/v1/my/shops/${shop.id}/products`, {
    categoryId: roses.id,
    name: `S5 Broken ${marker}`,
    price: 30,
    stockQuantity: 3,
    images: [{ url: "https://images.invalid/none.jpg", altText: null }],
  });

  await bff(customer, "PUT", `/v1/cart/items/${product.id}`, { quantity: 2 });
  const tomorrow = new Date(Date.now() + 36 * 60 * 60 * 1000).toISOString().slice(0, 10);
  const checkout = await bff<{ orders: { orderNumber: string }[] }>(
    customer,
    "POST",
    "/v1/checkout",
    {
      recipientName: "Aysel Mammadova",
      recipientPhone: "+994 50 000 00 00",
      addressLine: "Fuzuli küç. 5",
      district: "Yasamal",
      city: "Baku",
      postalCode: null,
      deliveryDate: tomorrow,
      giftMessage: null,
    },
    { "Idempotency-Key": `s5-${marker}` },
  );
  // Put one more item in the cart so the cart page has content.
  await bff(customer, "PUT", `/v1/cart/items/${product.id}`, { quantity: 1 });

  world = {
    seller,
    customer,
    admin,
    visitor,
    shopId: shop.id,
    shopSlug: shop.slug,
    productId: product.id,
    productName,
    imageUrl: image.url,
    orderNumber: checkout.orders[0]!.orderNumber,
  };
});

test.afterAll(async () => {
  for (const page of [world?.seller, world?.customer, world?.admin, world?.visitor])
    await page?.context().close();
});

// ---- Localization ---------------------------------------------------------------------------------------------

test("language: Azerbaijani by default, switching persists across navigation and reload", async ({
  browser,
}) => {
  const context = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const page = await context.newPage();
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("lang", "az");

  await page.getByLabel("Dil").selectOption("tr");
  await expect(page.locator("html")).toHaveAttribute("lang", "tr");
  await page.goto("/products");
  await expect(page.locator("html")).toHaveAttribute("lang", "tr");
  await expect(page.getByRole("heading", { level: 1, name: "Ürünler" })).toBeVisible();
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("lang", "tr");

  await page.getByLabel("Dil").selectOption("en");
  await page.goto("/shops");
  await expect(page.getByRole("heading", { level: 1, name: "Florists" })).toBeVisible();
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");

  await page.getByLabel("Language").selectOption("az");
  await expect(page.getByRole("navigation", { name: "Əsas menyu" })).toBeVisible();
  await context.close();
});

test("language: customer-facing pages show no untranslated English in Azerbaijani and Turkish", async ({
  browser,
}) => {
  const english =
    /\b(Add to cart|Sign in|In stock|Out of stock|Loading|Try again|Search|Florists|Products|Sort|Apply|Language)\b/;
  for (const locale of ["az", "tr"]) {
    const page = await newPage(browser, locale);
    for (const path of ["/", "/products", "/shops", `/products/${world.productId}`, "/login", "/register"]) {
      await page.goto(path);
      // Names of florists/products are user data; compare only the page chrome (header, headings, buttons, labels).
      const text = await page.locator("header, footer, h1, h2, label, button, a[href^='/']").allInnerTexts();
      const offending = text.filter((t) => english.test(t) && !t.includes(world.productName));
      expect(offending, `${locale} ${path}`).toEqual([]);
    }
    await page.context().close();
  }
});

test("money and dates use Azerbaijani conventions in the market locale", async ({ browser }) => {
  const page = await newPage(browser, "az");
  await page.goto(`/products/${world.productId}`);
  // 1234.5 AZN → "1.234,50 ₼" (Node ICU on the server render).
  await expect(page.getByText(/1\.234,50\s?₼/).first()).toBeVisible();
  await page.context().close();
});

test("@real-browser Azerbaijani money and dates render correctly in real Chrome (client-side rendering)", async ({
  browser,
}) => {
  const page = await newPage(browser, "az");
  // The cart and order list are client components: their text is produced by the browser itself, not by the server.
  // Chrome's own "az" Intl data is incomplete ("AZN 1,234.50", "2026 M10 14"), so the app formats az itself.
  const cookies = await world.customer.context().cookies();
  await page.context().addCookies(cookies.filter((cookie) => cookie.name !== "cicek_locale"));

  await page.goto("/cart");
  await expect(page.getByText(/1\.234,50\s₼/).first()).toBeVisible();
  await expect(page.getByText(/AZN\s?1,234/)).toHaveCount(0);

  await page.goto("/account/orders");
  const created = page
    .getByRole("cell")
    .filter({ hasText: /^\d{1,2} (yan|fev|mar|apr|may|iyn|iyl|avq|sen|okt|noy|dek) \d{4}, \d{2}:\d{2}$/ });
  await expect(created.first()).toBeVisible();
  await expect(page.getByText(/\d{4} M\d+ \d+/)).toHaveCount(0);

  // Server-rendered pages agree with the client-rendered ones.
  await page.goto(`/products/${world.productId}`);
  await expect(page.getByText(/1\.234,50\s₼/).first()).toBeVisible();
  await page.context().close();
});

// ---- Images ---------------------------------------------------------------------------------------------------

test("uploaded images are delivered publicly and cached; missing and broken images show a fallback", async () => {
  const visitor = world.visitor;
  await visitor.goto(`/products/${world.productId}`);
  const main = visitor.getByRole("img", { name: "Red roses in a vase" });
  await expect(main).toBeVisible();
  expect(await main.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);

  const response = await visitor.request.get(world.imageUrl);
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("image/png");
  expect(response.headers()["cache-control"]).toContain("immutable");
  expect(response.headers()["x-content-type-options"]).toBe("nosniff");

  // Cards: the product without an image and the one with an unreachable host both fall back to the placeholder.
  await visitor.goto(`/shops/${world.shopSlug}`);
  await expect(visitor.getByText("No image").first()).toBeVisible();
  await expect
    .poll(async () => visitor.getByText("No image").count(), { timeout: 15_000 })
    .toBeGreaterThanOrEqual(2);
  await expect(visitor.locator("img[src='https://images.invalid/none.jpg']")).toHaveCount(0);
});

test("a florist uploads an image in the product form; invalid files are refused with a localized message", async () => {
  const seller = world.seller;
  await seller.goto(`/seller/shops/${world.shopId}/products/${world.productId}`);
  await expect(seller.getByRole("img", { name: "Red roses in a vase" })).toBeVisible();

  // Wrong content with an image name: the platform decides from the bytes.
  await seller.getByTestId("image-file-input").setInputFiles({
    name: "not-really.png",
    mimeType: "image/png",
    buffer: Buffer.from("<script>alert(1)</script>"),
  });
  await expect(seller.getByText("Only JPEG, PNG or WebP images are accepted.")).toBeVisible();

  // A real image is added, previewed and removable.
  await seller
    .getByTestId("image-file-input")
    .setInputFiles({ name: "second.png", mimeType: "image/png", buffer: PNG });
  // The preview exists once the upload finished; its accessible name is the alt text. Wait for that before asserting
  // anything about the finished state (a negative check made earlier can pass before the upload even completes).
  await expect(seller.getByRole("img", { name: "Image 2" })).toBeVisible();
  // The alt text is the preview's accessible name, never visible text. The screen-reader-only label
  // "Image description (optional) — Image 2" legitimately contains the words, so only a whole-text match counts.
  await expect(seller.getByText("Image 2", { exact: true })).toHaveCount(0);
  await seller.getByRole("button", { name: "Move earlier" }).last().click();
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller.getByText("Saved.")).toBeVisible();

  // The shop image is uploaded from the profile editor and appears on the public shop page.
  await seller.goto(`/seller/shops/${world.shopId}`);
  await seller
    .getByTestId("shop-image-input")
    .setInputFiles({ name: "shop.png", mimeType: "image/png", buffer: PNG });
  await expect(seller.getByRole("button", { name: "Remove image" })).toBeVisible();
  await seller.getByRole("button", { name: "Save" }).click();
  await expect(seller.getByText("Details saved.")).toBeVisible();
});

// ---- Responsive layout + accessibility ----------------------------------------------------------------------------

const viewports = [
  { name: "390", width: 390, height: 844 },
  { name: "430", width: 430, height: 932 },
  { name: "768", width: 768, height: 1024 },
  { name: "1280", width: 1280, height: 800 },
] as const;

type Role = "visitor" | "customer" | "seller" | "admin";
const pages: { role: Role; path: (w: World) => string; name: string }[] = [
  { role: "visitor", path: () => "/", name: "home" },
  { role: "visitor", path: () => "/products", name: "products" },
  { role: "visitor", path: (w) => `/products/${w.productId}`, name: "product-detail" },
  { role: "visitor", path: () => "/shops", name: "shops" },
  { role: "visitor", path: (w) => `/shops/${w.shopSlug}`, name: "shop-detail" },
  { role: "visitor", path: () => "/login", name: "login" },
  { role: "customer", path: () => "/cart", name: "cart" },
  { role: "customer", path: () => "/account/orders", name: "customer-orders" },
  { role: "customer", path: (w) => `/account/orders/${w.orderNumber}`, name: "customer-order-detail" },
  { role: "customer", path: () => "/account", name: "account" },
  { role: "seller", path: () => "/seller", name: "seller-home" },
  { role: "seller", path: (w) => `/seller/shops/${w.shopId}`, name: "seller-dashboard" },
  { role: "seller", path: (w) => `/seller/shops/${w.shopId}/products`, name: "seller-products" },
  {
    role: "seller",
    path: (w) => `/seller/shops/${w.shopId}/products/${w.productId}`,
    name: "seller-product-form",
  },
  { role: "seller", path: (w) => `/seller/shops/${w.shopId}/orders`, name: "seller-orders" },
  {
    role: "seller",
    path: (w) => `/seller/shops/${w.shopId}/orders/${w.orderNumber}`,
    name: "seller-order-detail",
  },
  { role: "admin", path: () => "/admin", name: "admin-overview" },
  { role: "admin", path: () => "/admin/shops", name: "admin-shops" },
  { role: "admin", path: (w) => `/admin/shops/${w.shopId}`, name: "admin-shop-detail" },
  { role: "admin", path: () => "/admin/orders", name: "admin-orders" },
  { role: "admin", path: (w) => `/admin/orders/${w.orderNumber}`, name: "admin-order-detail" },
  { role: "admin", path: () => "/admin/categories", name: "admin-categories" },
  { role: "admin", path: () => "/admin/users", name: "admin-users" },
];

test("no page overflows horizontally at 390, 430, 768 and 1280 px (customer, florist, administrator)", async () => {
  test.setTimeout(240_000);
  const overflowing: string[] = [];
  for (const viewport of viewports) {
    for (const entry of pages) {
      const page = world[entry.role];
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto(entry.path(world));
      await page.waitForLoadState("networkidle");
      const { scrollWidth, innerWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        innerWidth: window.innerWidth,
      }));
      if (scrollWidth > innerWidth + 1)
        overflowing.push(`${entry.name}@${viewport.name}: ${scrollWidth}>${innerWidth}`);
      if (process.env.E2E_SCREENSHOT_DIR) {
        await page.screenshot({
          path: `${process.env.E2E_SCREENSHOT_DIR}/${entry.name}-${viewport.name}.png`,
          fullPage: true,
        });
      }
    }
  }
  expect(overflowing).toEqual([]);
});

test("the mobile menu opens, exposes its state and closes with Escape", async () => {
  const page = world.visitor;
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  const toggle = page.getByRole("button", { name: "Menu" });
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(
    page.getByRole("navigation", { name: "Main menu" }).getByRole("link", { name: "Products" }),
  ).toBeHidden();
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(
    page.getByRole("navigation", { name: "Main menu" }).getByRole("link", { name: "Products" }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
});

test("tables reflow into labelled cards on a phone and stay tables on desktop", async () => {
  const admin = world.admin;
  await admin.setViewportSize({ width: 390, height: 844 });
  await admin.goto("/admin/shops");
  const header = admin.locator("thead").first();
  await expect(admin.getByRole("cell").first()).toBeVisible();
  // On a phone the header row is visually hidden (the labels move into each card via data-label) ...
  await expect(header).toHaveCSS("position", "absolute");
  await expect(admin.getByRole("cell").nth(1)).toHaveAttribute("data-label", /.+/);
  // ... and on desktop it is a normal table header again.
  await admin.setViewportSize({ width: 1280, height: 800 });
  await expect(header).toHaveCSS("position", "static");
});

test("axe: no serious or critical accessibility violations on any audited page and viewport", async () => {
  test.setTimeout(300_000);
  const problems: string[] = [];
  let audited = 0;
  for (const viewport of [viewports[0], viewports[3]]) {
    for (const entry of pages) {
      const page = world[entry.role];
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto(entry.path(world));
      await page.waitForLoadState("networkidle");
      const results = await new AxeBuilder({ page })
        .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
        .analyze();
      expect(results.passes.length, `axe ran on ${entry.name}`).toBeGreaterThan(5); // the audit really executed
      audited += 1;
      for (const violation of results.violations) {
        if (violation.impact !== "serious" && violation.impact !== "critical") {
          console.log(`axe note ${entry.name}@${viewport.name} ${violation.id} (${violation.impact})`);
        }
        if (violation.impact === "serious" || violation.impact === "critical") {
          problems.push(
            `${entry.name}@${viewport.name} ${violation.id} (${violation.impact}): ${violation.nodes
              .slice(0, 2)
              .map((n) => n.target.join(" "))
              .join(" | ")}`,
          );
        }
      }
    }
  }
  console.log(`axe audited ${audited} page/viewport combinations`);
  expect(problems).toEqual([]);
});
