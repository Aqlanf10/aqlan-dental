/*
 * Service worker for the mobile screen.
 *
 * Its ONLY job is to make the app installable on Android and to show a readable page when the
 * phone is offline. It deliberately does not cache anything else.
 *
 * That restraint is the point. Everything this app displays is patient data — names, phone
 * numbers, appointment times, what work is at which lab. A cache on a phone outlives the
 * session, survives logout, and travels with a device that gets lent, lost or repaired. An
 * offline-capable clinical app is a real feature, but it is a feature with a consent and
 * retention story attached, and it is not something to acquire as a side effect of wanting an
 * app icon.
 *
 * So: no API responses are cached, ever. If the network is down the app says so instead of
 * showing yesterday's schedule as though it were today's — which for a clinic is worse than
 * showing nothing, because a stale schedule looks exactly like a current one.
 */

const SHELL_CACHE = "aqlan-shell-v1";
const OFFLINE_URL = "/offline.html";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(SHELL_CACHE).then((cache) => cache.addAll([OFFLINE_URL])).then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== SHELL_CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const { request } = event;

  // Anything that is not a plain page navigation — API calls above all — goes straight to the
  // network and is never stored.
  if (request.method !== "GET" || request.mode !== "navigate") return;

  event.respondWith(
    fetch(request).catch(() => caches.match(OFFLINE_URL).then((r) => r ?? Response.error())),
  );
});
