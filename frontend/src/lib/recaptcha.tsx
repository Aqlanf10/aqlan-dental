"use client";

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";

const GoogleReCaptchaProvider = dynamic(
  () => import("react-google-recaptcha-v3").then((mod) => ({ default: mod.GoogleReCaptchaProvider })),
  { ssr: false }
);

const RECAPTCHA_SITE_KEY = process.env.NEXT_PUBLIC_RECAPTCHA_SITE_KEY || '';
const PRODUCTION_VERCEL_HOSTS = new Set([
  "aqlan-dental.vercel.app",
  "aqlan-dental-pro.vercel.app",
]);

export function shouldEnableRecaptcha(
  siteKey: string,
  hostname?: string
): boolean {
  if (!siteKey) {
    return false;
  }

  const isVercelPreview =
    hostname?.endsWith(".vercel.app") === true &&
    !PRODUCTION_VERCEL_HOSTS.has(hostname);

  return !isVercelPreview;
}

export function RecaptchaProvider({ children }: { children: React.ReactNode }) {
  const [enabled, setEnabled] = useState(false);

  useEffect(() => {
    setEnabled(
      shouldEnableRecaptcha(RECAPTCHA_SITE_KEY, window.location.hostname)
    );
  }, []);

  if (!enabled) {
    // Keep generated preview domains free of Google domain-key errors. Public
    // APIs remain protected because the backend still validates required tokens.
    return <>{children}</>;
  }

  return (
    <GoogleReCaptchaProvider reCaptchaKey={RECAPTCHA_SITE_KEY} scriptProps={{ async: true }}>
      {children}
    </GoogleReCaptchaProvider>
  );
}

export { RECAPTCHA_SITE_KEY };
