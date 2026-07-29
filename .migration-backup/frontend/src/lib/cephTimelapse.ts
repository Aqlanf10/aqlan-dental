import type { CephAnalysisList } from "@/types/ceph";
import type { OrthoPhoto } from "@/types/ortho";

export interface CephTimelapseFrame {
  id: string;
  source: "ceph" | "photo";
  sourceId: string;
  date: string;
  imageUrl: string;
  label: string;
  approved: boolean;
}

const photoLabel = (photo: OrthoPhoto) => {
  if (photo.caption?.trim()) return photo.caption.trim();
  if (photo.subtype === "UpperOcclusal") return "إطباقية علوية";
  if (photo.subtype === "LowerOcclusal") return "إطباقية سفلية";
  if (photo.subtype === "Frontal") return "داخل الفم أمامية";
  return "صورة سريرية";
};

/** Builds an ordered real-record sequence. No synthetic/interpolated frames. */
export function buildCephTimelapseFrames(
  analyses: CephAnalysisList[],
  photos: OrthoPhoto[],
): CephTimelapseFrame[] {
  const analysisFrames = analyses
    .filter(item => Boolean(item.xrayFileUrl))
    .map(item => ({
      id: `ceph:${item.id}`,
      source: "ceph" as const,
      sourceId: item.id,
      date: item.analysisDate,
      imageUrl: item.xrayFileUrl!,
      label: item.analysisType === "pa" ? "سيفالو أمامي PA" : "سيفالو جانبي",
      approved: item.isApproved,
    }));

  const photoFrames = photos
    .filter(photo => Boolean(photo.photoUrl) && Boolean(photo.takenAt))
    .map(photo => ({
      id: `photo:${photo.id}`,
      source: "photo" as const,
      sourceId: photo.id,
      date: photo.takenAt!,
      imageUrl: photo.preparedImageUrl || photo.photoUrl,
      label: photoLabel(photo),
      approved: photo.preparationStatus === "ApprovedForPresentation",
    }));

  return [...analysisFrames, ...photoFrames].sort((a, b) =>
    a.date.localeCompare(b.date) || a.id.localeCompare(b.id));
}
