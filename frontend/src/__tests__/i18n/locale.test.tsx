import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { LocaleProvider, useLocale, useT, useTf } from "@/i18n/LocaleProvider";
import { LanguageSwitcher } from "@/components/shared/LanguageSwitcher";
import { normalizeLocale, directionOf, DEFAULT_LOCALE } from "@/i18n/types";
import { ar, en } from "@/i18n/messages";
import { navKeyFor } from "@/components/layout/Sidebar";

/**
 * CORE-REQ-006 — Arabic RTL and English LTR application contracts.
 *
 * The property these tests protect is the one that makes migrating ~7,880 strings safe: a key
 * with no English translation renders **today's Arabic**, never a blank and never a raw key.
 * Without it, every partially-migrated screen would be a defect.
 */

function Probe() {
  const { locale, dir } = useLocale();
  const t = useT();
  return (
    <div>
      <span data-testid="locale">{locale}</span>
      <span data-testid="dir">{dir}</span>
      <span data-testid="known">{t("nav.patients")}</span>
      <span data-testid="untranslated">{t("nav.ceph")}</span>
      <span data-testid="unknown">{t("does.not.exist", "سقوط احتياطي")}</span>
    </div>
  );
}

describe("locale contract", () => {
  beforeEach(() => {
    try { window.localStorage.clear(); } catch { /* ignore */ }
    document.documentElement.removeAttribute("dir");
  });

  it("defaults to Arabic, right-to-left", () => {
    render(<LocaleProvider><Probe /></LocaleProvider>);
    expect(screen.getByTestId("locale").textContent).toBe("ar");
    expect(screen.getByTestId("dir").textContent).toBe("rtl");
  });

  it("writes lang and dir onto the document, because that is what flips the layout", () => {
    render(<LocaleProvider><Probe /></LocaleProvider>);
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    expect(document.documentElement.getAttribute("lang")).toBe("ar");
  });

  it("switches to English and to left-to-right", () => {
    render(<LocaleProvider><LanguageSwitcher /><Probe /></LocaleProvider>);
    act(() => { fireEvent.click(screen.getByRole("button")); });
    expect(screen.getByTestId("locale").textContent).toBe("en");
    expect(screen.getByTestId("dir").textContent).toBe("ltr");
    expect(document.documentElement.getAttribute("dir")).toBe("ltr");
  });

  it("falls back to today's Arabic for a key with no English translation", () => {
    render(<LocaleProvider><LanguageSwitcher /><Probe /></LocaleProvider>);
    act(() => { fireEvent.click(screen.getByRole("button")); });
    expect(screen.getByTestId("known").textContent).toBe("Patients");
    expect(screen.getByTestId("untranslated").textContent).not.toBe("nav.ceph");
    expect(screen.getByTestId("untranslated").textContent).not.toBe("");
  });

  it("uses the caller's fallback for a key in no bundle at all", () => {
    render(<LocaleProvider><Probe /></LocaleProvider>);
    expect(screen.getByTestId("unknown").textContent).toBe("سقوط احتياطي");
  });

  it("remembers the choice for this browser", () => {
    render(<LocaleProvider><LanguageSwitcher /></LocaleProvider>);
    act(() => { fireEvent.click(screen.getByRole("button")); });
    expect(window.localStorage.getItem("aqlan.locale")).toBe("en");
  });

  it("survives storage being unavailable", () => {
    const spy = vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("private mode");
    });
    render(<LocaleProvider><LanguageSwitcher /><Probe /></LocaleProvider>);
    act(() => { fireEvent.click(screen.getByRole("button")); });
    expect(screen.getByTestId("locale").textContent).toBe("en");
    spy.mockRestore();
  });
});

describe("locale normalisation", () => {
  it("rejects anything that is not a language this system serves", () => {
    for (const bad of ["fr", "", "  ", null, undefined, 7, "AR-SA"]) {
      expect(normalizeLocale(bad)).toBe(DEFAULT_LOCALE);
    }
  });

  it("accepts the two real locales, case- and space-insensitively", () => {
    expect(normalizeLocale("  EN ")).toBe("en");
    expect(normalizeLocale("ar")).toBe("ar");
  });

  it("maps each locale to its direction", () => {
    expect(directionOf("ar")).toBe("rtl");
    expect(directionOf("en")).toBe("ltr");
  });
});

describe("translation bundles", () => {
  it("every English key has an Arabic source", () => {
    const orphans = Object.keys(en).filter((key) => !(key in ar));
    expect(orphans, `English keys with no Arabic source: ${orphans.join(", ")}`).toEqual([]);
  });

  it("no bundle value is blank", () => {
    for (const [name, bundle] of Object.entries({ ar, en })) {
      const blanks = Object.entries(bundle)
        .filter(([, value]) => value.trim().length === 0)
        .map(([key]) => key);
      expect(blanks, `${name} has blank values: ${blanks.join(", ")}`).toEqual([]);
    }
  });
});

describe("navKeyFor", () => {
  it("maps routes to their navigation keys", () => {
    expect(navKeyFor("/")).toBe("nav.dashboard");
    expect(navKeyFor("/patients")).toBe("nav.patients");
    expect(navKeyFor("/daily-operations")).toBe("nav.dailyOperations");
    // A child route must keep its own key: keying on the first segment alone made the recall
    // entry render the parent's label, so the sidebar showed "Appointments" twice.
    expect(navKeyFor("/appointments/recall")).toBe("nav.appointmentsRecall");
    expect(navKeyFor("/settings/lab-work-types")).toBe("nav.settingsLabWorkTypes");
  });
});

/**
 * Sentences that carry a value.
 *
 * The tempting shortcut is to concatenate — `t("paidFor") + name` — and it is wrong in a way
 * that only shows up in the second language: Arabic puts the amount before the patient and
 * English wants it the other way round. A placeholder is what lets each bundle choose its own
 * word order, so the two rules here are that substitution happens at all, and that both
 * bundles agree on which values a sentence expects.
 */
function InterpolationProbe() {
  const tf = useTf();
  return (
    <div>
      <span data-testid="next">{tf("dailyOps.next.patient", { name: "أحمد" })}</span>
      <span data-testid="missing">{tf("dailyOps.next.patient", {})}</span>
    </div>
  );
}

const PLACEHOLDER = /\{(\w+)\}/g;

function placeholdersIn(value: string): string[] {
  return [...value.matchAll(PLACEHOLDER)].map((m) => m[1]).sort();
}

describe("interpolated messages", () => {
  it("substitutes the values into the sentence", () => {
    render(
      <LocaleProvider>
        <InterpolationProbe />
      </LocaleProvider>,
    );
    expect(screen.getByTestId("next").textContent).toBe("التالي: أحمد");
  });

  it("leaves an unsupplied placeholder visible rather than blanking it", () => {
    // A sentence with a hole in it reads as finished text and hides the bug. `{name}` does not.
    render(
      <LocaleProvider>
        <InterpolationProbe />
      </LocaleProvider>,
    );
    expect(screen.getByTestId("missing").textContent).toBe("التالي: {name}");
  });

  it("keeps both bundles expecting the same values", () => {
    // If English drops a placeholder the value silently disappears for English readers, and
    // the screen shows a shorter, wrong sentence with no error anywhere.
    const mismatched = Object.keys(en)
      .filter((key) => ar[key])
      .filter((key) => placeholdersIn(ar[key]).join(",") !== placeholdersIn(en[key]).join(","))
      .map((key) => `${key}: ar[${placeholdersIn(ar[key])}] en[${placeholdersIn(en[key])}]`);

    expect(mismatched, "these two bundles disagree on what a sentence carries").toEqual([]);
  });

  it("finds interpolated keys at all", () => {
    // A regex that matched nothing would make the parity check above vacuous.
    const withValues = Object.keys(ar).filter((key) => placeholdersIn(ar[key]).length > 0);
    expect(withValues.length).toBeGreaterThan(3);
  });
});
