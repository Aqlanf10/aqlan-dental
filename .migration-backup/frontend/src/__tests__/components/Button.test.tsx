import { describe, it, expect, vi } from "vitest";
import { render } from "@testing-library/react";
import { screen } from "@testing-library/dom";
import userEvent from "@testing-library/user-event";
import { Button } from "@/components/ui/Button";

describe("Button", () => {
  it("renders its label", () => {
    render(<Button>حفظ</Button>);
    expect(screen.getByRole("button", { name: "حفظ" })).toBeInTheDocument();
  });

  it("applies the primary variant (clinic.navy) by default", () => {
    render(<Button>حفظ</Button>);
    expect(screen.getByRole("button")).toHaveClass("bg-clinic-navy");
  });

  it("applies variant classes", () => {
    const { rerender } = render(<Button variant="danger">حذف</Button>);
    expect(screen.getByRole("button")).toHaveClass("bg-red-600");

    rerender(<Button variant="secondary">إلغاء</Button>);
    expect(screen.getByRole("button")).toHaveClass("border");

    rerender(<Button variant="ghost">رجوع</Button>);
    expect(screen.getByRole("button")).toHaveClass("bg-transparent");
  });

  it("applies size classes", () => {
    const { rerender } = render(<Button size="sm">صغير</Button>);
    expect(screen.getByRole("button")).toHaveClass("text-xs");

    rerender(<Button size="md">متوسط</Button>);
    expect(screen.getByRole("button")).toHaveClass("text-sm");
  });

  it("respects the disabled prop and blocks clicks", async () => {
    const onClick = vi.fn();
    render(
      <Button disabled onClick={onClick}>
        معطل
      </Button>,
    );
    const btn = screen.getByRole("button");
    expect(btn).toBeDisabled();
    await userEvent.click(btn);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("renders a leading icon", () => {
    render(<Button icon={<span data-testid="icon" />}>أضف</Button>);
    expect(screen.getByTestId("icon")).toBeInTheDocument();
  });

  it("defaults to type=button", () => {
    render(<Button>زر</Button>);
    expect(screen.getByRole("button")).toHaveAttribute("type", "button");
  });
});
