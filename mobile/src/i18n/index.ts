import ar from './locales/ar.json';
import en from './locales/en.json';

export type Locale = 'ar' | 'en';
export type TranslationKey = keyof typeof ar;

const dictionaries: Record<Locale, Record<TranslationKey, string>> = { ar, en };

export function translate(locale: Locale, key: TranslationKey, variables?: Record<string, string | number>) {
  const template = dictionaries[locale][key] ?? dictionaries.ar[key] ?? key;
  if (!variables) return template;

  return Object.entries(variables).reduce(
    (result, [name, value]) => result.replaceAll(`{${name}}`, String(value)),
    template,
  );
}

export function isLocale(value: unknown): value is Locale {
  return value === 'ar' || value === 'en';
}
