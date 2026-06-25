import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { screen } from "@testing-library/dom";
import { EmptyState } from "@/components/ui/EmptyState";

describe("EmptyState", () => {
  it("renders the message", () => {
    render(<EmptyState message="لا توجد بيانات" />);
    expect(screen.getByText("لا توجد بيانات")).toBeInTheDocument();
  });

  it("renders an optional description", () => {
    render(
      <EmptyState message="لا توجد مواعيد" description="أضف موعدًا للبدء" />,
    );
    expect(screen.getByText("أضف موعدًا للبدء")).toBeInTheDocument();
  });

  it("renders an action element", () => {
    render(
      <EmptyState
        message="لا توجد حالات"
        action={<button>إضافة حالة</button>}
      />,
    );
    expect(
      screen.getByRole("button", { name: "إضافة حالة" }),
    ).toBeInTheDocument();
  });

  it("renders an optional icon", () => {
    render(
      <EmptyState message="فارغ" icon={<span data-testid="empty-icon" />} />,
    );
    expect(screen.getByTestId("empty-icon")).toBeInTheDocument();
  });
});
