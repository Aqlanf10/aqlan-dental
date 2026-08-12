/**
 * CORE-F-014 — the dashboard's session gate must always finish.
 *
 * The dashboard renders "جارٍ تحميل النظام..." until a session check completes. That check
 * awaits the network, and the network is not guaranteed to answer:
 *
 *   * `api` has no timeout configured, so a connection that never settles produces a promise
 *     that never settles either; and
 *   * the 401 refresh interceptor parks concurrent requests in a queue whose promises resolve
 *     only when `processQueue` runs — which does not happen if the refresh request itself
 *     hangs.
 *
 * Either case left the user on a spinner forever, with no error, no redirect and no way out
 * except typing a different URL. It shows up most often after a hard reload, because access
 * tokens have been in-memory since W04, so a reload always depends on the silent refresh.
 *
 * This module exists so that behaviour is testable without mounting the whole dashboard: the
 * rule is simply that the returned promise always settles, and always says what to do next.
 */

/** How long to wait for the session check before giving up on it. */
export const SESSION_BOOT_TIMEOUT_MS = 15_000;

export type SessionBootOutcome =
  /** The session is valid — render the dashboard. */
  | { status: "ready" }
  /** No usable session, for any reason — send the user to /login. */
  | { status: "redirect-to-login"; reason: "unauthenticated" | "timeout" | "error" };

/**
 * Resolves the promise, or gives up after <paramref name="timeoutMs"/>.
 *
 * Deliberately does not cancel the underlying work: an in-flight refresh may still succeed and
 * populate the store, and aborting it could log out a user whose network merely stalled. The
 * point is to stop the UI waiting on it, not to undo it.
 */
export function withTimeout<T>(
  promise: Promise<T>,
  timeoutMs: number,
): Promise<{ timedOut: false; value: T } | { timedOut: true }> {
  return new Promise((resolve) => {
    const timer = setTimeout(() => resolve({ timedOut: true }), timeoutMs);

    promise
      .then((value) => {
        clearTimeout(timer);
        resolve({ timedOut: false, value });
      })
      .catch(() => {
        clearTimeout(timer);
        // A rejection is not a reason to hang; it is a reason to stop waiting.
        resolve({ timedOut: true });
      });
  });
}

/**
 * Runs the session check and always returns an outcome.
 *
 * Every path — success, failure, rejection, or a check that never answers — ends in either
 * "ready" or "redirect-to-login". There is no fourth possibility, which is the property the
 * spinner bug violated.
 */
export async function resolveSessionBoot(options: {
  fetchMe: () => Promise<void>;
  /** Read AFTER fetchMe, because fetchMe is what populates it. */
  isAuthenticated: () => boolean;
  timeoutMs?: number;
}): Promise<SessionBootOutcome> {
  const { fetchMe, isAuthenticated, timeoutMs = SESSION_BOOT_TIMEOUT_MS } = options;

  let result: { timedOut: false; value: void } | { timedOut: true };
  try {
    result = await withTimeout(fetchMe(), timeoutMs);
  } catch {
    // fetchMe throwing synchronously still must not strand the caller.
    return { status: "redirect-to-login", reason: "error" };
  }

  if (result.timedOut) {
    return { status: "redirect-to-login", reason: "timeout" };
  }

  // The check answered. Whether it found a session is a separate question — and reading the
  // store can itself throw if something upstream is broken, which must not strand the caller
  // either.
  try {
    return isAuthenticated()
      ? { status: "ready" }
      : { status: "redirect-to-login", reason: "unauthenticated" };
  } catch {
    return { status: "redirect-to-login", reason: "error" };
  }
}
