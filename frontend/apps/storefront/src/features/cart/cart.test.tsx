import type { Cart } from "@cicek/api-client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import { getMessages } from "@/lib/i18n/messages";
import { CartView } from "./CartView";
import { CheckoutForm } from "./CheckoutForm";

const messages = getMessages("en");
const t = messages.market;

function wrap(children: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider locale="en" messages={messages}>
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </I18nProvider>,
  );
}

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

afterEach(() => vi.unstubAllGlobals());

describe("CartView", () => {
  const cart: Cart = {
    currency: "TRY",
    total: 230,
    itemCount: 3,
    canCheckout: false,
    shops: [
      {
        shopId: "s1",
        shopName: "Gül Bahçesi",
        subtotal: 200,
        deliveryFee: 30,
        total: 230,
        lines: [
          {
            productId: "p1",
            productName: "Roses",
            imageUrl: null,
            unitPrice: 100,
            quantity: 2,
            lineTotal: 200,
            availableQuantity: 5,
            issue: "None",
          },
        ],
      },
      {
        shopId: "s2",
        shopName: "Orkide Evi",
        subtotal: 0,
        deliveryFee: 0,
        total: 0,
        lines: [
          {
            productId: "p2",
            productName: "Orchid",
            imageUrl: null,
            unitPrice: 600,
            quantity: 1,
            lineTotal: 600,
            availableQuantity: 0,
            issue: "ProductUnavailable",
          },
        ],
      },
    ],
  };

  it("shows server totals per shop, flags unavailable items and blocks checkout", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(200, cart)));
    wrap(<CartView />);

    expect(await screen.findByText("Gül Bahçesi")).toBeInTheDocument();
    expect(screen.getByText(t.cart.issues.ProductUnavailable)).toBeInTheDocument();
    expect(screen.getByText(t.cart.issuesAlert)).toBeInTheDocument();
    expect(screen.getByText(t.cart.splitNotice)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: t.cart.checkout })).toBeDisabled();
    expect(screen.getAllByText(/230\.00/).length).toBeGreaterThan(0);
  });

  it("renders the empty state", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(json(200, { ...cart, shops: [], total: 0, canCheckout: false })),
    );
    wrap(<CartView />);

    expect(await screen.findByText(t.cart.emptyTitle)).toBeInTheDocument();
  });

  it("renders the error state with retry", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(503, { code: "service.unavailable" })));
    wrap(<CartView />);

    expect(await screen.findByText(t.common.loadError)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: t.common.retry })).toBeInTheDocument();
  });
});

describe("CheckoutForm", () => {
  async function fillValid() {
    await userEvent.type(screen.getByLabelText(new RegExp(t.checkout.recipientName)), "Ayşe Yılmaz");
    await userEvent.type(screen.getByLabelText(new RegExp(t.checkout.recipientPhone)), "+90 555 000 00 00");
    await userEvent.type(screen.getByLabelText(new RegExp(`^${t.checkout.addressLine}`)), "Moda Cad. 5");
    await userEvent.type(screen.getByLabelText(new RegExp(t.checkout.district)), "Kadıköy");
    await userEvent.type(screen.getByLabelText(new RegExp(`^${t.checkout.city}`)), "İstanbul");
    await userEvent.type(screen.getByLabelText(new RegExp(t.checkout.deliveryDate)), "2030-02-14");
  }

  it("validates on the client without calling the server", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    wrap(<CheckoutForm />);

    await userEvent.type(screen.getByLabelText(new RegExp(t.checkout.recipientPhone)), "abc");
    await userEvent.click(screen.getByRole("button", { name: t.checkout.submit }));

    expect(screen.getByText(t.checkout.errors.invalidPhone)).toBeInTheDocument();
    expect(screen.getAllByText(t.checkout.errors.required).length).toBeGreaterThan(0);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("reuses the same idempotency key when the submission is retried", async () => {
    const result = {
      checkoutId: "c1",
      total: 230,
      currency: "TRY",
      replayed: false,
      orders: [{ orderNumber: "CS261013-AB12CD", shopName: "Gül Bahçesi" }],
    };
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new TypeError("network down"))
      .mockResolvedValue(json(200, result));
    vi.stubGlobal("fetch", fetchMock);
    wrap(<CheckoutForm />);

    await fillValid();
    await userEvent.click(screen.getByRole("button", { name: t.checkout.submit }));
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: t.checkout.submit }));

    expect(await screen.findByText(t.checkout.successTitle)).toBeInTheDocument();
    const keys = fetchMock.mock.calls
      .filter(([url]) => url === "/api/bff/v1/checkout")
      .map(
        ([, init]) => (init as RequestInit & { headers: Record<string, string> }).headers["Idempotency-Key"],
      );
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[0]).toBe(keys[1]);

    const body = JSON.parse(String((fetchMock.mock.calls[1]![1] as RequestInit).body));
    expect(body).not.toHaveProperty("total");
    expect(body.postalCode).toBeNull();
  });
});
