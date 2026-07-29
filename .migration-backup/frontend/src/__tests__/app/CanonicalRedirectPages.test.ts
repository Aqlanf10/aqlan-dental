import { beforeEach, describe, expect, it, vi } from "vitest";
import ClinicQueueRedirectPage from "@/app/(dashboard)/clinic-queue/page";
import HrIndexRoute from "@/app/(dashboard)/hr/page";
import OrthoSurgicalLegacyListRoute from "@/app/(dashboard)/ortho-surgical/page";
import PatientJourneyRedirectPage from "@/app/(dashboard)/patient-journey/page";
import PatientJourneyDetailRedirectPage from "@/app/(dashboard)/patient-journey/[patientId]/page";
import SettingsPermissionsRoute from "@/app/(dashboard)/settings/permissions/page";
import SettingsUsersRoute from "@/app/(dashboard)/settings/users/page";
import UsersRoute from "@/app/(dashboard)/users/page";

const { redirectMock } = vi.hoisted(() => ({ redirectMock: vi.fn() }));

vi.mock("next/navigation", () => ({ redirect: redirectMock }));

describe("legacy dashboard route redirects", () => {
  beforeEach(() => {
    redirectMock.mockClear();
  });

  it.each([
    { Route: ClinicQueueRedirectPage, destination: "/daily-operations?tab=queue" },
    { Route: PatientJourneyRedirectPage, destination: "/daily-operations" },
    { Route: UsersRoute, destination: "/settings?tab=permissions" },
    { Route: SettingsUsersRoute, destination: "/settings?tab=permissions" },
    { Route: SettingsPermissionsRoute, destination: "/settings?tab=permissions" },
    { Route: OrthoSurgicalLegacyListRoute, destination: "/ortho" },
    { Route: HrIndexRoute, destination: "/hr/attendance" },
  ])("redirects a static alias to $destination", ({ Route, destination }) => {
    Route();
    expect(redirectMock).toHaveBeenCalledOnce();
    expect(redirectMock).toHaveBeenCalledWith(destination);
  });

  it("preserves supported query parameters on the patient detail alias", async () => {
    await PatientJourneyDetailRedirectPage({
      params: Promise.resolve({ patientId: "patient-1" }),
      searchParams: Promise.resolve({ tab: "timeline", filter: ["open", "today"] }),
    });

    expect(redirectMock).toHaveBeenCalledOnce();
    expect(redirectMock).toHaveBeenCalledWith(
      "/patients/patient-1?focus=journey&tab=timeline&filter=open&filter=today",
    );
  });
});
