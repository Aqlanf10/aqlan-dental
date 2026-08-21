/**
 * CORE-REQ-006 — Arabic RTL and English LTR application contracts.
 *
 * ## Why this shape, and not `next-intl`
 *
 * The conventional Next.js answer is a `[locale]` route segment. It was rejected deliberately:
 * every URL in this system would change, which breaks the canonical route inventory
 * (`CORE-P1-S2`), the checked redirect contract, the route-role manifest (`CORE-P1-S3`) and the
 * route/policy contract (`CORE-P1-S4`) — four pieces of Phase 1 that were just proved correct.
 * Restructuring routes to add a language toggle would trade a working guarantee for a
 * convention, on a system a clinic uses every day.
 *
 * This is an authenticated dashboard, not indexed marketing content, so locale-prefixed URLs
 * and server-rendered translated HTML buy nothing here.
 *
 * ## The property that makes migration safe
 *
 * There are roughly 7,880 Arabic strings across 419 files. They cannot all move at once, and a
 * half-migrated app must never show a blank label or a raw key to a receptionist mid-shift.
 * So `t()` falls back to **Arabic** — today's exact text — for any key not yet translated.
 * A screen that has not been touched keeps working unchanged; a screen that has been touched
 * gains English. Nothing is ever half-rendered.
 *
 * That is also why Arabic is the source of truth rather than English: Arabic is what the code
 * says today, so the fallback is always the string that is already on screen.
 */

export type Locale = "ar" | "en";

export const LOCALES: readonly Locale[] = ["ar", "en"] as const;

export const DEFAULT_LOCALE: Locale = "ar";

/** Text direction for a locale. Arabic is the only RTL locale this system serves. */
export function directionOf(locale: Locale): "rtl" | "ltr" {
  return locale === "ar" ? "rtl" : "ltr";
}

/**
 * Narrows an arbitrary value to a supported locale.
 *
 * Anything unrecognised becomes Arabic rather than being passed through: a stray value in a
 * settings row or a stale localStorage entry must not leave the interface in no language.
 */
export function normalizeLocale(value: unknown): Locale {
  const raw = typeof value === "string" ? value.trim().toLowerCase() : "";
  return (LOCALES as readonly string[]).includes(raw) ? (raw as Locale) : DEFAULT_LOCALE;
}

/** A translation bundle: Arabic source text keyed by a stable identifier. */
export type Bundle = Record<string, string>;
