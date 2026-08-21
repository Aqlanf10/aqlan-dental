import { describe, it, expect } from "vitest";
import { printIdentity, type ClinicBranding } from "@/hooks/useClinicBranding";

/**
 * CORE-REQ-006 — printed forms carry the clinic's identity in the language the clinic chose.
 *
 * Prescriptions and radiology referrals read the English fields directly, so they were
 * permanently English with no way to change that: the Arabic identity existed in Settings and
 * no screen could reach it. The default stays English so a clinic that has not chosen sees no
 * change (Spec 010, RX-REQ-004 — these are the forms a patient carries outside the clinic).
 */

const BRANDING: ClinicBranding = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  phone: "04-253028",
  whatsApp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  footer: "",
  logoUrl: "",
  clinicNameEn: "Dr. Aqlan Alkamel Center",
  addressEn: "Upper Al-Tahrir Street, Taiz, Yemen",
  leadDoctorEn: "Dr. Aqlan Alkamel — Orthodontic Specialist",
  leadDoctorCredentialsEn: "Central University of Manila — Philippines",
  printLanguage: "en",
  leadDoctorAr: "د. عقلان الكامل — أخصائي تقويم الأسنان",
  leadDoctorCredentialsAr: "جامعة مانيلا المركزية — الفلبين",
};

describe("printIdentity", () => {
  it("prints English by default, which is what these forms have always done", () => {
    const id = printIdentity(BRANDING);

    expect(id.clinicName).toBe("Dr. Aqlan Alkamel Center");
    expect(id.leadDoctor).toBe("Dr. Aqlan Alkamel — Orthodontic Specialist");
    expect(id.isArabic).toBe(false);
  });

  it("prints Arabic identity when the clinic selects Arabic", () => {
    const id = printIdentity({ ...BRANDING, printLanguage: "ar" });

    expect(id.clinicName).toContain("عقلان");
    expect(id.clinicAddress).toContain("تعز");
    expect(id.isArabic).toBe(true);
  });

  /**
   * The owner's standing rule: every report carries the lead doctor and their qualification.
   * An Arabic form that dropped them would satisfy the language switch and break the rule.
   */
  it("keeps the lead doctor and qualification in Arabic too", () => {
    const id = printIdentity({ ...BRANDING, printLanguage: "ar" });

    expect(id.leadDoctor).toBe("د. عقلان الكامل — أخصائي تقويم الأسنان");
    expect(id.leadDoctorCredentials).toBe("جامعة مانيلا المركزية — الفلبين");
    expect(id.leadDoctor).not.toBe("");
  });

  it("treats an unrecognised language as English rather than rendering nothing", () => {
    const id = printIdentity({ ...BRANDING, printLanguage: "fr" });

    expect(id.clinicName).toBe("Dr. Aqlan Alkamel Center");
    expect(id.isArabic).toBe(false);
  });

  /**
   * Whichever language is chosen, no field may come back empty — a printed medical form with a
   * blank clinic name or a blank doctor is worse than one in the wrong language.
   */
  it("never returns an empty field in either language", () => {
    for (const lang of ["ar", "en"]) {
      const id = printIdentity({ ...BRANDING, printLanguage: lang });
      for (const [field, value] of Object.entries(id)) {
        if (field === "isArabic") continue;
        expect(String(value).length, `${lang}/${field} must not be blank`).toBeGreaterThan(0);
      }
    }
  });
});
