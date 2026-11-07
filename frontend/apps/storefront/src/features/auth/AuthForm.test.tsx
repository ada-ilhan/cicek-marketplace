import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import { getMessages } from "@/lib/i18n/messages";
import { AuthForm } from "./AuthForm";
import { registerSchema } from "./schemas";

const replace = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh: vi.fn() }),
  useSearchParams: () => new URLSearchParams("next=/account"),
}));

const messages = getMessages("en");

function renderForm(mode: "login" | "register") {
  return render(
    <I18nProvider locale="en" messages={messages}>
      <QueryClientProvider client={new QueryClient()}>
        <AuthForm mode={mode} />
      </QueryClientProvider>
    </I18nProvider>,
  );
}

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
  replace.mockReset();
});

describe("AuthForm", () => {
  it("shows localized client-side validation errors without calling the server", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderForm("register");

    await userEvent.type(screen.getByLabelText(/Email/), "not-an-email");
    await userEvent.type(screen.getByLabelText(/Password/), "short");
    await userEvent.click(screen.getByRole("button", { name: messages.auth.submitRegister }));

    expect(screen.getByText(messages.auth.errors.invalid_email)).toBeInTheDocument();
    expect(screen.getByText(messages.auth.errors.password_too_short)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("maps the API's invalid-credentials problem to a friendly alert", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse(401, { status: 401, code: "auth.invalid_credentials" })),
    );
    renderForm("login");

    await userEvent.type(screen.getByLabelText(/Email/), "user@example.com");
    await userEvent.type(screen.getByLabelText(/Password/), "wrong-password");
    await userEvent.click(screen.getByRole("button", { name: messages.auth.submitLogin }));

    expect(await screen.findByRole("alert")).toHaveTextContent(messages.auth.errors.invalidCredentials);
    expect(replace).not.toHaveBeenCalled();
  });

  it("shows the unavailable message when the network fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("fetch failed")));
    renderForm("login");

    await userEvent.type(screen.getByLabelText(/Email/), "user@example.com");
    await userEvent.type(screen.getByLabelText(/Password/), "some-password");
    await userEvent.click(screen.getByRole("button", { name: messages.auth.submitLogin }));

    expect(await screen.findByRole("alert")).toHaveTextContent(messages.auth.errors.unavailable);
  });

  it("navigates to the requested page after a successful login", async () => {
    const user = { id: "1", email: "user@example.com", roles: ["Customer"], permissions: [] };
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ user }), { status: 200 })),
    );
    renderForm("login");

    await userEvent.type(screen.getByLabelText(/Email/), "user@example.com");
    await userEvent.type(screen.getByLabelText(/Password/), "correct-password");
    await userEvent.click(screen.getByRole("button", { name: messages.auth.submitLogin }));

    await vi.waitFor(() => expect(replace).toHaveBeenCalledWith("/account"));
  });
});

describe("registerSchema", () => {
  it("trims the email and enforces password length bounds", () => {
    expect(registerSchema.safeParse({ email: " a@example.com ", password: "x".repeat(10) }).success).toBe(
      true,
    );
    expect(registerSchema.safeParse({ email: "a@example.com", password: "x".repeat(129) }).success).toBe(
      false,
    );
  });
});
