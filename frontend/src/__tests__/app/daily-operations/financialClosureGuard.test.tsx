import React from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { useValidateFinancialClosure } from "@/app/(dashboard)/daily-operations/_lib/hooks";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe("daily operations financial closure guard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("sends the patient and visit to the server guard and preserves a blocked result", async () => {
    vi.mocked(api.post).mockResolvedValue({
      data: {
        canClose: false,
        reason: "يوجد مبلغ متبقي",
        reasonCode: "OUTSTANDING_BALANCE",
        outstandingAmount: 5000,
      },
    });

    const { result } = renderHook(() => useValidateFinancialClosure(), {
      wrapper: createWrapper(),
    });

    let validation;
    await act(async () => {
      validation = await result.current.mutateAsync({
        patientId: "039bd979-776c-452c-a6be-b5273c8f3602",
        visitId: "3daaff84-129d-496a-a042-20f40a1cb39f",
      });
    });

    expect(api.post).toHaveBeenCalledWith(
      "/api/patient-journey/039bd979-776c-452c-a6be-b5273c8f3602/validate-financial-closure",
      {
        visitId: "3daaff84-129d-496a-a042-20f40a1cb39f",
        managerOverride: false,
        closureReason: undefined,
      },
    );
    expect(validation).toMatchObject({
      canClose: false,
      reasonCode: "OUTSTANDING_BALANCE",
      outstandingAmount: 5000,
    });
  });
});
