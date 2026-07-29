import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { screen } from "@testing-library/dom";
import { Badge } from "@/components/ui/Badge";

describe("Badge", () => {
  it("renders its content", () => {
    render(<Badge>نشط</Badge>);
    expect(screen.getByText("نشط")).toBeInTheDocument();
  });

  it("defaults to the neutral variant", () => {
    render(<Badge>عادي</Badge>);
    expect(screen.getByText("عادي")).toHaveClass("bg-gray-100");
  });

  it.each([
    ["success", "bg-green-100"],
    ["warning", "bg-clinic-orange-100"],
    ["danger", "bg-red-100"],
    ["info", "bg-clinic-blue-100"],
  ] as const)("applies the %s variant", (variant, expectedClass) => {
    render(<Badge variant={variant}>حالة</Badge>);
    expect(screen.getByText("حالة")).toHaveClass(expectedClass);
  });

  it("merges a custom className", () => {
    render(<Badge className="custom-x">حالة</Badge>);
    expect(screen.getByText("حالة")).toHaveClass("custom-x");
  });
});
