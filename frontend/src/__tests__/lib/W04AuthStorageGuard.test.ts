import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

function source(relativePath: string) {
  return readFileSync(resolve(process.cwd(), relativePath), "utf8");
}

describe("W04 staff authentication storage guard", () => {
  it("never persists staff or original administrator access tokens", () => {
    const apiSource = source("src/lib/api.ts");
    const storeSource = source("src/stores/authStore.ts");

    expect(apiSource).not.toContain('localStorage.setItem("access_token"');
    expect(storeSource).not.toContain('localStorage.setItem("access_token"');
    expect(storeSource).not.toContain('localStorage.setItem("aqlan_original_token"');
  });

  it("restores the administrator only from the backend stop response", () => {
    const storeSource = source("src/stores/authStore.ts");
    const stopCall = storeSource.indexOf('api.post<{\n            accessToken: string;');
    const applyReturnedToken = storeSource.indexOf(
      "setAccessToken(data.accessToken)",
      stopCall,
    );

    expect(stopCall).toBeGreaterThan(-1);
    expect(applyReturnedToken).toBeGreaterThan(stopCall);
    expect(storeSource).not.toContain("originalToken");
  });

  it("revokes the administrator refresh session before impersonation starts", () => {
    const apiSource = source("src/lib/api.ts");

    expect(apiSource).toContain('url.includes("/api/auth/impersonate/")');
    expect(apiSource).toContain('await apiRaw.post("/api/auth/logout"');
  });

  it("revokes a target refresh session after an impersonation page reload", () => {
    const layoutSource = source("src/app/(dashboard)/layout.tsx");
    const markerCheck = layoutSource.indexOf(
      "localStorage.getItem(IMPERSONATION_SESSION_MARKER)",
    );
    const revokeCall = layoutSource.indexOf(
      "await terminateImpersonatedRefreshSession()",
    );
    const normalRestore = layoutSource.indexOf("await fetchMe()");

    expect(markerCheck).toBeGreaterThan(-1);
    expect(revokeCall).toBeGreaterThan(markerCheck);
    expect(normalRestore).toBeGreaterThan(revokeCall);
  });
});
