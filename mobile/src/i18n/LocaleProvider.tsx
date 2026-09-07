import { createContext, type PropsWithChildren, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { secureStorage } from '@/storage/secureStorage';
import { isLocale, translate, type Locale, type TranslationKey } from './index';

const LOCALE_KEY = 'aqlan.locale.v2';

type LocaleContextValue = {
  locale: Locale;
  isRtl: boolean;
  ready: boolean;
  setLocale: (locale: Locale) => Promise<void>;
  toggleLocale: () => Promise<void>;
  t: (key: TranslationKey, variables?: Record<string, string | number>) => string;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

export function LocaleProvider({ children }: PropsWithChildren) {
  const [locale, setLocaleState] = useState<Locale>('ar');
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let active = true;
    secureStorage.get(LOCALE_KEY).then((saved) => {
      if (active && isLocale(saved)) setLocaleState(saved);
    }).finally(() => {
      if (active) setReady(true);
    });
    return () => { active = false; };
  }, []);

  const setLocale = useCallback(async (next: Locale) => {
    setLocaleState(next);
    await secureStorage.set(LOCALE_KEY, next);
  }, []);

  const toggleLocale = useCallback(
    () => setLocale(locale === 'ar' ? 'en' : 'ar'),
    [locale, setLocale],
  );

  const value = useMemo<LocaleContextValue>(() => ({
    locale,
    isRtl: locale === 'ar',
    ready,
    setLocale,
    toggleLocale,
    t: (key, variables) => translate(locale, key, variables),
  }), [locale, ready, setLocale, toggleLocale]);

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>;
}

export function useLocale() {
  const value = useContext(LocaleContext);
  if (!value) throw new Error('useLocale must be used inside LocaleProvider');
  return value;
}
