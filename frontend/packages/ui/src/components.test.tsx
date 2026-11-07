import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Button } from "./Button";
import { Alert, EmptyState, ErrorState } from "./Feedback";
import { TextField } from "./TextField";

describe("TextField", () => {
  it("associates the label with the input", () => {
    render(<TextField label="Email" name="email" />);

    expect(screen.getByLabelText("Email")).toBeInTheDocument();
  });

  it("marks the input invalid and describes it with the first error", () => {
    render(<TextField label="Email" errors={["Enter a valid email.", "Second error"]} />);

    const input = screen.getByLabelText("Email");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Enter a valid email.");
    expect(screen.queryByText("Second error")).not.toBeInTheDocument();
  });

  it("is not marked invalid without errors", () => {
    render(<TextField label="Email" hint="We never share it." />);

    const input = screen.getByLabelText("Email");
    expect(input).not.toHaveAttribute("aria-invalid");
    expect(input).toHaveAccessibleDescription("We never share it.");
  });
});

describe("Button", () => {
  it("is disabled and busy while loading", () => {
    render(<Button loading>Save</Button>);

    const button = screen.getByRole("button", { name: "Save" });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
  });

  it("defaults to type=button so it never submits forms accidentally", () => {
    render(<Button>Click</Button>);

    expect(screen.getByRole("button")).toHaveAttribute("type", "button");
  });
});

describe("feedback states", () => {
  it("announces errors as alerts", () => {
    render(<Alert tone="error" title="Failed" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Failed");
  });

  it("offers retry in the error state", async () => {
    const onRetry = vi.fn();
    render(
      <ErrorState
        title="Could not load"
        onRetry={onRetry}
        retryLabel="Retry"
        reference="abc123"
        referenceLabel="Ref"
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "Retry" }));

    expect(onRetry).toHaveBeenCalledOnce();
    expect(screen.getByText("abc123")).toBeInTheDocument();
  });

  it("has no built-in language: unlabeled retry and reference are omitted, not shown in English", () => {
    render(<ErrorState title="Could not load" onRetry={() => undefined} reference="abc123" />);

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByText(/Try again|Reference/)).not.toBeInTheDocument();
  });

  it("renders the empty state", () => {
    render(<EmptyState title="Nothing here" description="Add something." />);

    expect(screen.getByText("Nothing here")).toBeInTheDocument();
  });
});

describe("SelectField and TextAreaField", () => {
  it("label and describe their controls like TextField", async () => {
    const { SelectField, TextAreaField } = await import("./Fields");
    render(
      <>
        <SelectField label="Category" errors={["Choose a category."]}>
          <option value="">-</option>
        </SelectField>
        <TextAreaField label="Description" hint="Optional" />
      </>,
    );

    expect(screen.getByLabelText("Category")).toHaveAccessibleDescription("Choose a category.");
    expect(screen.getByLabelText("Category")).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByLabelText("Description")).toHaveAccessibleDescription("Optional");
  });
});

describe("ConfirmDialog", () => {
  it("is an accessible labelled modal that requires a reason before confirming", async () => {
    const { ConfirmDialog } = await import("./ConfirmDialog");
    const onConfirm = vi.fn();
    render(
      <ConfirmDialog
        open
        title="Reject application?"
        confirmLabel="Confirm"
        cancelLabel="Cancel"
        reason={{ label: "Reason", requiredMessage: "A reason is required." }}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      >
        The florist sees the reason.
      </ConfirmDialog>,
    );

    const dialog = screen.getByRole("dialog", { name: "Reject application?" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(screen.getByLabelText("Reason")).toHaveFocus();

    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));
    expect(screen.getByText("A reason is required.")).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText("Reason"), "  Missing license  ");
    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));
    expect(onConfirm).toHaveBeenCalledWith("Missing license");
  });

  it("cancels with Escape, renders nothing when closed, and traps Tab inside", async () => {
    const { ConfirmDialog } = await import("./ConfirmDialog");
    const onCancel = vi.fn();
    const { rerender } = render(
      <ConfirmDialog
        open
        title="Sure?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />,
    );

    await userEvent.tab();
    await userEvent.tab();
    expect(screen.getByRole("button", { name: "No" })).toHaveFocus(); // wrapped around inside the dialog

    await userEvent.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledOnce();

    rerender(
      <ConfirmDialog
        open={false}
        title="Sure?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />,
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
