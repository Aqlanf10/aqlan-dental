import { createContext, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';

import { useLocale } from '@/i18n/LocaleProvider';
import { fetchClinicIdentityPayload, selectIdentity, type ClinicIdentity } from './clinicIdentity';

const ClinicIdentityContext = createContext<ClinicIdentity | null>(null);

/**
 * Loads the centre's identity once and shares it with every screen.
 *
 * The payload is fetched a single time and re-selected when the language changes, rather than
 * re-fetched: the Arabic and English fields arrive in the same response, so switching language
 * must not cost a round trip — the toggle is meant to be instant.
 *
 * A failure is deliberately silent. Identity is chrome, not data: if the request fails the
 * screens keep the bundled strings they already had, which is the correct text for this clinic
 * in the overwhelming majority of cases. Blocking sign-in, or showing an error banner about a
 * clinic name, would be a worse outcome than a stale address.
 */
export function ClinicIdentityProvider({ children }: PropsWithChildren) {
  const { locale } = useLocale();
  const [payload, setPayload] = useState<unknown>(null);
  const loaded = useRef(false);

  useEffect(() => {
    if (loaded.current) return;
    loaded.current = true;
    let active = true;
    void (async () => {
      try {
        const next = await fetchClinicIdentityPayload();
        if (active) setPayload(next);
      } catch {
        // Keep the bundled identity. See the note above.
      }
    })();
    return () => { active = false; };
  }, []);

  const identity = useMemo(() => selectIdentity(payload, locale), [payload, locale]);

  return <ClinicIdentityContext.Provider value={identity}>{children}</ClinicIdentityContext.Provider>;
}

/**
 * The configured identity, or `null` until it arrives. Callers pass their bundled string as the
 * fallback so nothing on screen is ever blank while the request is in flight.
 */
export function useClinicIdentity(): ClinicIdentity | null {
  return useContext(ClinicIdentityContext);
}
