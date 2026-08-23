"use client";

import { useEffect } from "react";

/**
 * Registers the service worker so Android offers "install app" and the phone has something
 * readable to show when it is offline.
 *
 * Registration is skipped in development, where a stale worker serving an old build is a
 * confusing way to lose an afternoon.
 */
export function ServiceWorkerRegistrar() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production") return;
    if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;

    // Failure here must never surface to the user: not having an app icon is a smaller
    // problem than an error toast on a clinical screen.
    navigator.serviceWorker.register("/sw.js").catch(() => {});
  }, []);

  return null;
}
