import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

function source(relativePath: string) {
  return readFileSync(resolve(process.cwd(), relativePath), "utf8");
}

describe("W05 future appointment journey guard", () => {
  it("sends an explicit override flag and reason through every guarded mutation", () => {
    const hooks = source("src/app/(dashboard)/daily-operations/_lib/hooks.ts");

    expect(hooks).toContain("overrideFutureAppointment?: boolean");
    expect(hooks).toContain("overrideReason?: string");
    expect(hooks).toContain("/intake");
    expect(hooks).toContain("/send-to-queue");
    expect(hooks).toContain("/start-visit");
  });

  it("opens the reason dialog only for an authenticated administrator", () => {
    const page = source("src/app/(dashboard)/daily-operations/page.tsx");

    expect(page).toContain('journeyBusinessDateErrorCode(error) === "future_appointment"');
    expect(page).toContain('if (userRole !== "Admin") return false');
    expect(page).toContain("setFutureOverrideOpen(true)");
    expect(page).toContain("overrideFutureAppointment: true");
    expect(page).toContain("overrideReason: reason");
  });

  it("requires a non-empty operational reason before confirmation", () => {
    const dialog = source(
      "src/app/(dashboard)/daily-operations/_components/modals/FutureAppointmentOverrideDialog.tsx",
    );

    expect(dialog).toContain("const normalizedReason = reason.trim()");
    expect(dialog).toContain("disabled={isPending || !normalizedReason}");
    expect(dialog).toContain("سيتم تسجيل تجاوز إداري");
  });

  it("exposes appointment and real event timestamps in the journey read model", () => {
    const types = source("src/components/shared/journey/types.ts");

    expect(types).toContain("appointmentDate?: string | null");
    expect(types).toContain("arrivedAt?: string | null");
    expect(types).toContain("queueAddedAt?: string | null");
    expect(types).toContain("visitStartedAt?: string | null");
  });
});
