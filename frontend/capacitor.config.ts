import type { CapacitorConfig } from "@capacitor/cli";

/**
 * Android wrapper for the mobile screen.
 *
 * The app loads the deployed site rather than bundling a copy of it, and that is a deliberate
 * choice for a stopgap:
 *
 *   * The main system is still changing daily. A bundled build would freeze one moment of it
 *     inside an APK, and every backend change would need a new APK installed by hand on every
 *     phone in the clinic.
 *   * This app's whole value is that it shows what the system currently says. A copy that
 *     drifts is worse than no app, because a stale schedule looks exactly like a current one.
 *   * The Next.js app is server-rendered. Producing a static bundle would mean a real
 *     refactor of the main product to serve a temporary tool — the wrong way round.
 *
 * The cost is honest and worth stating: the app needs a connection to be useful. It already
 * did, since nothing caches patient data on the phone.
 *
 * The URL is overridable at build time so a test build can point at staging without editing
 * this file:  APP_URL=https://... npx cap sync android
 */
const appUrl = process.env.APP_URL ?? "https://aqlan-dental.vercel.app";

const config: CapacitorConfig = {
  appId: "com.aqlandental.clinic",
  appName: "عقلان",
  // Capacitor requires a webDir even when loading a remote URL. It holds only the offline
  // fallback, which is what the shell shows if the site cannot be reached at launch.
  webDir: "android-shell",
  server: {
    url: appUrl,
    // The clinic's site is HTTPS. Cleartext stays off so a misconfigured URL fails loudly
    // instead of quietly sending patient data over plain HTTP.
    cleartext: false,
    androidScheme: "https",
  },
  android: {
    // Chrome's own WebView, so the app renders exactly what the browser does and inherits its
    // security updates rather than a bundled engine that ages.
    webContentsDebuggingEnabled: false,
  },
};

export default config;
