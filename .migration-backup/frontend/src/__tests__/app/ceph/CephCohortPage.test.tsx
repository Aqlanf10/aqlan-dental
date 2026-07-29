import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import CephCohortPage from "@/app/(dashboard)/ceph/cohort/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));

describe("CephCohortPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the privacy suppression state without a cohort count", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        suppressed: true,
        minimumCohortSize: 5,
        cohortSize: null,
        availableTags: [],
        skeletalDistribution: [],
        verticalDistribution: [],
        incisorDistribution: [],
        measurements: [],
      },
    } as never);

    render(<CephCohortPage />);

    expect(await screen.findByRole("heading", { name: "النتيجة محجوبة لحماية الخصوصية" })).toBeInTheDocument();
    expect(screen.queryByText("حجم المجموعة")).not.toBeInTheDocument();
  });

  it("renders aggregate distributions without patient rows", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        suppressed: false,
        minimumCohortSize: 5,
        cohortSize: 6,
        availableTags: [],
        skeletalDistribution: [{ key: "skeletal:class-ii", label: "هيكلي Class II", count: 6, percentage: 100 }],
        verticalDistribution: [],
        incisorDistribution: [],
        measurements: [{ measurementName: "SNA", unit: "°", count: 6, mean: 82, minimum: 80, maximum: 84, standardDeviation: 1.2 }],
      },
    } as never);

    render(<CephCohortPage />);

    expect(await screen.findByText("هيكلي Class II")).toBeInTheDocument();
    expect(screen.getByText("6 | 100%")).toBeInTheDocument();
    expect(screen.getByText("SNA")).toBeInTheDocument();
    expect(screen.queryByText("اسم المريض")).not.toBeInTheDocument();
  });
});
