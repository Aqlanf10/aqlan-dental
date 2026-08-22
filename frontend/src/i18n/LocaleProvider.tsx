"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { ar, BUNDLES } from "./messages";
import { DEFAULT_LOCALE, directionOf, normalizeLocale, type Locale } from "./types";

/**
 * CORE-REQ-006 — the interface language, and the document direction that follows from it.
 *
 * The chosen locale lives in `localStorage`, which is deliberate: language is a property of the
 * person reading the screen, not of the clinic. A Yemeni receptionist and a visiting English
 * speaking colleague share one deployment and one database, and neither should change what the
 * other sees. The clinic-wide default still comes from settings; this only overrides it per
 * browser.
 *
 * `dir` and `lang` are written onto `<html>` on change rather than being fixed in the root
 * layout, because Tailwind's logical properties (`ps-*`, `pe-*`, `ms-*`) and the browser's own
 * bidi algorithm both key off `dir`. Setting it is what actually flips the layout; translating
 * strings alone would leave an English interface reading right to left.
 */

interface LocaleContextValue {
  locale: Locale;
  dir: "rtl" | "ltr";
  setLocale: (next: Locale) => void;
  /** Translate a key, falling back to Arabic and then to the key itself. */
  t: (key: string, fallback?: string) => string;
}

const LocaleContext = createContext<LocaleContextValue | null>(null);

const STORAGE_KEY = "aqlan.locale";

export function LocaleProvider({
  children,
  initialLocale = DEFAULT_LOCALE,
}: {
  children: ReactNode;
  initialLocale?: Locale;
}) {
  // Server and first client render must agree, so the stored preference is read in an effect
  // rather than during render — reading localStorage inline would hydrate-mismatch every page.
  const [locale, setLocaleState] = useState<Locale>(initialLocale);

  useEffect(() => {
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (stored) setLocaleState(normalizeLocale(stored));
    } catch {
      // Private mode, or storage disabled. The default locale is a correct answer.
    }
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    root.setAttribute("lang", locale);
    root.setAttribute("dir", directionOf(locale));
  }, [locale]);

  const setLocale = useCallback((next: Locale) => {
    const normalized = normalizeLocale(next);
    setLocaleState(normalized);
    try {
      window.localStorage.setItem(STORAGE_KEY, normalized);
    } catch {
      // Not being able to remember the choice must not stop the choice taking effect now.
    }
  }, []);

  const t = useCallback(
    (key: string, fallback?: string) => {
      const bundle = BUNDLES[locale];
      const translated = bundle?.[key];
      if (translated) return translated;

      // Arabic is the source of truth: an untranslated key renders the string the screen
      // already showed before this system existed, never a blank and never a raw key.
      const source = ar[key];
      if (source) return source;

      return fallback ?? key;
    },
    [locale],
  );

  const value = useMemo<LocaleContextValue>(
    () => ({ locale, dir: directionOf(locale), setLocale, t }),
    [locale, setLocale, t],
  );

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>;
}

/**
 * Reads the active locale and translator.
 *
 * Falls back to a working Arabic translator outside a provider rather than throwing: a missing
 * provider must never be the reason a clinical screen fails to render.
 */
export function useLocale(): LocaleContextValue {
  const ctx = useContext(LocaleContext);
  if (ctx) return ctx;

  return {
    locale: DEFAULT_LOCALE,
    dir: directionOf(DEFAULT_LOCALE),
    setLocale: () => {},
    t: (key: string, fallback?: string) => ar[key] ?? fallback ?? key,
  };
}

/** Convenience for components that only need to translate. */
export function useT() {
  return useLocale().t;
}
