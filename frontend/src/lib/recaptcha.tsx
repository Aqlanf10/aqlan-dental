"use client";

import dynamic from "next/dynamic";

const GoogleReCaptchaProvider = dynamic(
  () => import("react-google-recaptcha-v3").then((mod) => ({ default: mod.GoogleReCaptchaProvider })),
  { ssr: false }
);

const RECAPTCHA_SITE_KEY = process.env.NEXT_PUBLIC_RECAPTCHA_SITE_KEY || '';

export function RecaptchaProvider({ children }: { children: React.ReactNode }) {
  if (!RECAPTCHA_SITE_KEY) {
    // reCAPTCHA not configured — render children without provider
    return <>{children}</>;
  }

  return (
    <GoogleReCaptchaProvider reCaptchaKey={RECAPTCHA_SITE_KEY} scriptProps={{ async: true }}>
      {children}
    </GoogleReCaptchaProvider>
  );
}

export { RECAPTCHA_SITE_KEY };
