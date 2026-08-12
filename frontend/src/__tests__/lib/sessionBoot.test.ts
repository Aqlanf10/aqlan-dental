import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { resolveSessionBoot, withTimeout, SESSION_BOOT_TIMEOUT_MS } from "@/lib/sessionBoot";

/**
 * CORE-F-014 — the dashboard boot gate must always finish.
 *
 * The defect was a permanent spinner: the layout awaited the session check and only then
 * opened its gate, so a check that never settled left "جارٍ تحميل النظام..." on screen
 * forever — no error, no redirect, nothing to click. It surfaced after a hard reload, because
 * access tokens are in-memory since W04 and a reload always depends on the silent refresh.
 *
 * The property under test is deliberately blunt: whatever the session check does, this
 * resolves, and it resolves to either "render" or "go to /login". The first test is the
 * regression itself — before the fix it would hang until the runner timed the suite out.
 */
describe("resolveSessionBoot", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("gives up on a session check that never settles, instead of waiting forever", async () => {
    // The exact shape of the bug: api has no timeout, and the 401 refresh interceptor parks
    // callers in a queue that drains only when the refresh answers. If it never answers, this
    // promise never settles.
    const neverSettles = () => new Promise<void>(() => {});

    const outcome = resolveSessionBoot({
      fetchMe: neverSettles,
      isAuthenticated: () => true,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(1_000);

    expect(await outcome).toEqual({ status: "redirect-to-login", reason: "timeout" });
  });

  it("renders the dashboard when the check succeeds and a session exists", async () => {
    const outcome = resolveSessionBoot({
      fetchMe: async () => {},
      isAuthenticated: () => true,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(0);
    expect(await outcome).toEqual({ status: "ready" });
  });

  it("sends the user to /login when the check succeeds but finds no session", async () => {
    const outcome = resolveSessionBoot({
      fetchMe: async () => {},
      isAuthenticated: () => false,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(0);
    expect(await outcome).toEqual({ status: "redirect-to-login", reason: "unauthenticated" });
  });

  it("treats a rejected check as a redirect, not as a reason to hang", async () => {
    const outcome = resolveSessionBoot({
      fetchMe: async () => {
        throw new Error("network down");
      },
      isAuthenticated: () => true,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(0);
    // Reported as a timeout because, from the gate's point of view, the check did not answer.
    // What matters is that it settles and points at /login.
    expect((await outcome).status).toBe("redirect-to-login");
  });

  it("survives a check that throws synchronously", async () => {
    const outcome = resolveSessionBoot({
      fetchMe: () => {
        throw new Error("boom");
      },
      isAuthenticated: () => true,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(0);
    expect(await outcome).toEqual({ status: "redirect-to-login", reason: "error" });
  });

  it("survives a store read that throws", async () => {
    const outcome = resolveSessionBoot({
      fetchMe: async () => {},
      isAuthenticated: () => {
        throw new Error("store broken");
      },
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(0);
    expect(await outcome).toEqual({ status: "redirect-to-login", reason: "error" });
  });

  it("does not give up early on a slow but working check", async () => {
    // A stalled network must not be confused with a slow one — logging someone out because
    // their connection was briefly poor would be its own defect.
    const slow = () => new Promise<void>((resolve) => setTimeout(resolve, 900));

    const outcome = resolveSessionBoot({
      fetchMe: slow,
      isAuthenticated: () => true,
      timeoutMs: 1_000,
    });

    await vi.advanceTimersByTimeAsync(950);
    expect(await outcome).toEqual({ status: "ready" });
  });

  it("ships with a timeout short enough to be a UI, not a wait", () => {
    // A gate that opens after five minutes is the same defect with extra steps.
    expect(SESSION_BOOT_TIMEOUT_MS).toBeLessThanOrEqual(30_000);
    expect(SESSION_BOOT_TIMEOUT_MS).toBeGreaterThanOrEqual(5_000);
  });
});

describe("withTimeout", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("clears its timer once the promise settles", async () => {
    // Otherwise every navigation leaves a pending timer behind.
    const clearSpy = vi.spyOn(globalThis, "clearTimeout");

    const result = withTimeout(Promise.resolve("ok"), 5_000);
    await vi.advanceTimersByTimeAsync(0);

    expect(await result).toEqual({ timedOut: false, value: "ok" });
    expect(clearSpy).toHaveBeenCalled();
  });
});
