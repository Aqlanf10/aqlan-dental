import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { PrescriptionPrint } from "@/components/prescriptions/PrescriptionPrint";
import { RadiologyReferralPrint } from "@/components/radiology/RadiologyReferralPrint";
import type { Prescription } from "@/types/prescription";
import type { RadiologyOrder } from "@/types/radiologyOrder";

// Spec 010: forms the patient carries outside the clinic print in ENGLISH
// (RX-REQ-002/003) with settings-driven identity (RX-REQ-004).

const prescription: Prescription = {
  id: "p1",
  patientId: "pt1",
  patientName: "أحمد صالح",
  patientNumber: "P-0042",
  doctorName: "د. عقلان",
  diagnosis: "Pulpitis tooth 36",
  drugs: [{ name: "Amoxicillin 500mg", dose: "1 cap", frequency: "TID", duration: "5 days" }],
  notes: "بعد الأكل",
  createdAt: "2026-07-12",
};

const order: RadiologyOrder = {
  id: "r1",
  patientId: "pt1",
  patientName: "أحمد صالح",
  patientNumber: "P-0042",
  patientDateOfBirth: "2000-01-15",
  patientGender: "Male",
  doctorName: "د. عقلان",
  studyType: "panoramic",
  region: "Full arch",
  clinicalNotes: "Pre-orthodontic assessment",
  createdAt: "2026-07-12",
};

describe("PrescriptionPrint (English form)", () => {
  it("renders English labels, LTR direction, and the configured EN identity", () => {
    const { container } = render(
      <PrescriptionPrint
        prescription={prescription}
        clinicName="My Configured Clinic EN"
        leadDoctor="Dr. Configured — Orthodontist"
        leadDoctorCredentials="Some University"
      />,
    );
    expect(container.querySelector("#prescription-print-root")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("My Configured Clinic EN")).toBeInTheDocument();
    expect(screen.getByText("Dr. Configured — Orthodontist")).toBeInTheDocument();
    expect(screen.getByText(/Patient:/)).toBeInTheDocument();
    expect(screen.getByText(/Diagnosis:/)).toBeInTheDocument();
    expect(screen.getByText(/Doctor's signature:/)).toBeInTheDocument();
    // user-entered values print as entered (Arabic stays Arabic)
    expect(screen.getByText("أحمد صالح")).toBeInTheDocument();
    expect(screen.getByText("Amoxicillin 500mg")).toBeInTheDocument();
  });
});

describe("RadiologyReferralPrint (English referral)", () => {
  it("renders the English referral with study label, patient block, and identity", () => {
    const { container } = render(
      <RadiologyReferralPrint
        order={order}
        clinicName="My Configured Clinic EN"
        clinicPhone="04-000000"
      />,
    );
    expect(container.querySelector("#radiology-referral-print-root")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("RADIOLOGY REFERRAL")).toBeInTheDocument();
    expect(screen.getByText("To: The Radiology Center")).toBeInTheDocument();
    expect(screen.getByText("Panoramic Radiograph (OPG)")).toBeInTheDocument();
    expect(screen.getByText(/Region of interest:/)).toBeInTheDocument();
    expect(screen.getByText("Pre-orthodontic assessment")).toBeInTheDocument();
    expect(screen.getByText("My Configured Clinic EN")).toBeInTheDocument();
    expect(screen.getByText(/04-000000/)).toBeInTheDocument();
    expect(screen.getByText(/Sex:/)).toBeInTheDocument();
  });

  it("maps every study type to an English print label", () => {
    for (const t of ["panoramic", "cbct_3d", "lateral_ceph", "pa_ceph", "bitewing", "periapical"]) {
      const { unmount } = render(<RadiologyReferralPrint order={{ ...order, studyType: t }} />);
      // no raw enum value leaks onto the printout
      expect(screen.queryByText(t)).not.toBeInTheDocument();
      unmount();
    }
  });

  it("omits age when date of birth is unknown", () => {
    render(<RadiologyReferralPrint order={{ ...order, patientDateOfBirth: undefined }} />);
    expect(screen.queryByText(/Age:/)).not.toBeInTheDocument();
  });
});
