/**
 * Compatibility shim: replaces next/navigation for the Vite/wouter port.
 * Drop-in replacements for useRouter, usePathname, useSearchParams, redirect, useParams.
 */
import { useLocation, useParams as _useWouterParams } from "wouter";

/** Access URL params from the current route (e.g. /patients/:id → { id: "..." }) */
export function useParams<T extends Record<string, string> = Record<string, string>>(): T {
  return _useWouterParams() as T;
}

export function useRouter() {
  const [, setLocation] = useLocation();
  return {
    push: (path: string) => setLocation(path),
    replace: (path: string) => setLocation(path, { replace: true }),
    back: () => window.history.back(),
    forward: () => window.history.forward(),
    refresh: () => window.location.reload(),
    prefetch: (_path: string) => {}, // no-op
  };
}

export function usePathname(): string {
  const [location] = useLocation();
  return location;
}

/** Returns a URLSearchParams object matching the current query string. */
export function useSearchParams(): URLSearchParams {
  return new URLSearchParams(window.location.search);
}

/** Client-side redirect (replaces history entry). */
export function redirect(path: string): never {
  window.location.replace(path);
  throw new Error("redirect"); // prevent further execution
}

/** Client-side navigation (pushes history entry). */
export function notFound(): never {
  window.location.href = "/404";
  throw new Error("not-found");
}
