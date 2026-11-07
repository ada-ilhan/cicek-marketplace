import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import { getMessages, locales } from "@/lib/i18n/messages";
import { AdminShell } from "./AdminShell";
import { adminPermissions } from "./permissions";
import { AdminShopDetail } from "./AdminShops";

vi.mock("next/navigation", () => ({
  usePathname: () => "/admin/shops",
  useSearchParams: () => new URLSearchParams(),
}));

const messages = getMessages("en");
const t = messages.admin;

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

describe("AdminShell access control (UX layer; the API enforces the same permissions)", () => {
  it("shows access denied to a user without the permission", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          json(200, { id: "1", email: "c@example.com", roles: ["Customer"], permissions: [] }),
        ),
    );
    wrap(<AdminShell permission={adminPermissions.shops}>secret admin content</AdminShell>);

    expect(await screen.findByText(t.accessDenied.title)).toBeInTheDocument();
    expect(screen.queryByText("secret admin content")).not.toBeInTheDocument();
  });

  it("shows access denied when the session is gone (401)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(401, { status: 401 })));
    wrap(<AdminShell permission={adminPermissions.shops}>secret admin content</AdminShell>);

    expect(await screen.findByText(t.accessDenied.title)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: t.accessDenied.signIn })).toHaveAttribute(
      "href",
      "/login?next=%2Fadmin%2Fshops",
    );
  });

  it("renders content and only the navigation the permissions allow", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        json(200, {
          id: "1",
          email: "s@example.com",
          roles: ["Support"],
          permissions: [adminPermissions.shops, adminPermissions.orders],
        }),
      ),
    );
    wrap(<AdminShell permission={adminPermissions.shops}>shops content</AdminShell>);

    expect(await screen.findByText("shops content")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: t.nav.shops })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: t.nav.orders })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: t.nav.users })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: t.nav.categories })).not.toBeInTheDocument();
  });
});

describe("AdminShopDetail", () => {
  const detail = (status: string) => ({
    ownerEmail: "owner@example.com",
    activeProducts: 3,
    shop: {
      id: "s1",
      name: "Gül Evi",
      slug: "gul-evi",
      description: null,
      contactEmail: "g@example.com",
      contactPhone: "+994 12 000 00 00",
      addressLine: "Küçə 1",
      district: "Nəsimi",
      city: "Bakı",
      postalCode: null,
      status,
      statusReason: null,
      commissionRateBps: null,
      createdAt: "2026-10-19T10:00:00Z",
      approvedAt: null,
      myRole: null,
      version: 0,
    },
  });

  it("asks for confirmation and a reason before rejecting, then calls the API once", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation((url: string, init?: RequestInit) =>
        Promise.resolve(
          init?.method === "POST" ? json(200, detail("Rejected").shop) : json(200, detail("PendingApproval")),
        ),
      );
    vi.stubGlobal("fetch", fetchMock);
    wrap(<AdminShopDetail shopId="s1" />);

    await userEvent.click(await screen.findByRole("button", { name: t.shops.actions.reject }));
    expect(screen.getByRole("dialog", { name: t.confirm.rejectTitle })).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(0);

    await userEvent.click(screen.getByRole("button", { name: t.confirm.confirm }));
    expect(screen.getByText(t.confirm.reasonRequired)).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText(t.confirm.reasonLabel), "Missing license");
    await userEvent.click(screen.getByRole("button", { name: t.confirm.confirm }));

    await vi.waitFor(() => expect(screen.getByText(t.shops.actionDone)).toBeInTheDocument());
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST")!;
    expect(post[0]).toBe("/api/bff/v1/admin/shops/s1/reject");
    expect(JSON.parse(String((post[1] as RequestInit).body))).toEqual({ reason: "Missing license" });
  });

  it("offers only the actions valid for the status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(200, detail("Active"))));
    wrap(<AdminShopDetail shopId="s1" />);

    expect(await screen.findByRole("button", { name: t.shops.actions.suspend })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: t.shops.actions.approve })).not.toBeInTheDocument();
  });
});

describe("localization", () => {
  it("has a complete catalogue for every supported locale, with Azerbaijani first", () => {
    expect(locales[0]).toBe("az");
    for (const locale of locales) {
      const m = getMessages(locale);
      expect(m.admin.nav.overview).toBeTruthy();
      expect(m.market.orders.statuses.PendingPayment).toBeTruthy();
      expect(m.market.seller.shopStatuses.Rejected).toBeTruthy();
    }

    expect(getMessages("az").market.orders.statuses.Delivered).toBe("Çatdırıldı");
    expect(getMessages("az").market.intlLocale).toBe("az-Latn-AZ");
  });
});
