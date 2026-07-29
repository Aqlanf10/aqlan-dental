import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import AppointmentsTable from "@/app/(dashboard)/daily-operations/_components/AppointmentsTable";
import type { TodayJourneyItem } from "@/components/shared/journey/types";

const baseItem: TodayJourneyItem = {
  appointmentId: "appointment-1",
  patientId: "patient-1",
  patientName: "مريض اختبار",
  patientPhone: "770000000",
  appointmentTime: "10:00",
  appointmentStatus: "InRoom",
  doctorName: "د. اختبار",
  roomName: "غرفة 1",
  queueItemId: "queue-1",
  queueStatus: "InRoom",
  nextAction: "StartVisit",
};

function renderTable(
  item: TodayJourneyItem,
  onStartVisit = vi.fn(),
) {
  render(
    <AppointmentsTable
      items={[item]}
      loading={false}
      isDoctor={false}
      canProcessCheckout
      isReception={false}
      isAccountant={false}
      onIntake={vi.fn()}
      onSendToQueue={vi.fn()}
      onCallPatient={vi.fn()}
      onEnterRoom={vi.fn()}
      onStartVisit={onStartVisit}
      onQuickPayment={vi.fn()}
      onCreateDraftInvoice={vi.fn()}
      createDraftInvoicePending={false}
      onBookAppointment={vi.fn()}
      onWhatsApp={vi.fn()}
      onNoShow={vi.fn()}
      onCancel={vi.fn()}
      onViewPatient={vi.fn()}
      onCompleteVisit={vi.fn()}
      onOpenSidePanel={vi.fn()}
    />,
  );
  return onStartVisit;
}

describe("AppointmentsTable visit transition actions", () => {
  it("shows بدء الزيارة for an InRoom patient and invokes the start action", () => {
    const onStartVisit = renderTable(baseItem);

    const startButtons = screen.getAllByRole("button", { name: "بدء الزيارة" });
    expect(startButtons).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "إكمال الزيارة" })).not.toBeInTheDocument();

    fireEvent.click(startButtons[0]);
    expect(onStartVisit).toHaveBeenCalledWith(baseItem);
  });

  it("only exposes إكمال الزيارة after the visit is in progress", () => {
    renderTable({
      ...baseItem,
      appointmentStatus: "InProgress",
      queueStatus: "InProgress",
      nextAction: "Handoff",
      visitId: "visit-1",
    });

    expect(screen.queryByRole("button", { name: "بدء الزيارة" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إكمال الزيارة" })).toBeInTheDocument();
  });
});
