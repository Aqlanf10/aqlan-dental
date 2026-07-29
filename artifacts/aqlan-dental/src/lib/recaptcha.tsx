import React, { lazy, Suspense } from "react";

const GoogleReCaptchaProvider = lazy(() =>
  import("react-google-recaptcha-v3").then((mod) => ({
    default: mod.GoogleReCaptchaProvider,
  }))
);

export const RECAPTCHA_SITE_KEY = import.meta.env.VITE_RECAPTCHA_SITE_KEY || '';

export function RecaptchaProvider({ children }: { children: React.ReactNode }) {
  if (!RECAPTCHA_SITE_KEY) {
    // reCAPTCHA not configured — render children without provider
    return <>{children}</>;
  }

  return (
    <Suspense fallback={<>{children}</>}>
      <GoogleReCaptchaProvider reCaptchaKey={RECAPTCHA_SITE_KEY} scriptProps={{ async: true }}>
        {children}
      </GoogleReCaptchaProvider>
    </Suspense>
  );
}
