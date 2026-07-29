import { describe, expect, it } from "vitest";
import { buildCephTimelapseFrames } from "@/lib/cephTimelapse";
import type { CephAnalysisList } from "@/types/ceph";
import type { OrthoPhoto } from "@/types/ortho";

const analysis = (id: string, date: string, image = `/uploads/${id}.jpg`): CephAnalysisList => ({
  id,
  orthoCaseId: "case-1",
  patientName: "Patient",
  analysisType: "steiner",
  analysisDate: date,
  xrayFileUrl: image,
  aiAssisted: false,
  landmarkCount: 24,
  hasMeasurements: true,
  isApproved: true,
  clinicalTags: [],
  createdAt: `${date}T12:00:00Z`,
});

describe("buildCephTimelapseFrames", () => {
  it("orders real ceph and photo records without creating intermediate frames", () => {
    const photo: OrthoPhoto = {
      id: "p1",
      photoUrl: "/uploads/p1.jpg",
      photoType: "Intraoral",
      subtype: "UpperOcclusal",
      takenAt: "2026-02-01",
    };

    const frames = buildCephTimelapseFrames(
      [analysis("a2", "2026-03-01"), analysis("a1", "2026-01-01")],
      [photo],
    );

    expect(frames.map(frame => frame.id)).toEqual(["ceph:a1", "photo:p1", "ceph:a2"]);
    expect(frames).toHaveLength(3);
  });

  it("omits records that do not have a dated image", () => {
    const undatedPhoto = { id: "p1", photoUrl: "/uploads/p1.jpg", photoType: "Intraoral" } as OrthoPhoto;
    expect(buildCephTimelapseFrames([analysis("a1", "2026-01-01", "")], [undatedPhoto])).toEqual([]);
  });
});
