import { apiRequest } from "@/lib/api";
import * as DocumentPicker from "expo-document-picker";
import * as ImagePicker from "expo-image-picker";

const MAX_FILE_SIZE = 10 * 1024 * 1024;
const PHOTO_MIME_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const CLINICAL_DOCUMENT_MIME_TYPES = new Set([...PHOTO_MIME_TYPES, "application/pdf"]);
const RADIOGRAPH_MIME_TYPES = CLINICAL_DOCUMENT_MIME_TYPES;

export const PHOTO_CATEGORIES = [
  { label: "خارج فموية", value: "extraoral" },
  { label: "داخل فموية", value: "intraoral" },
  { label: "ابتسامة", value: "smile" },
  { label: "أمامية", value: "frontal" },
  { label: "جانبية", value: "profile" },
  { label: "إطباقية علوية", value: "occlusal_upper" },
  { label: "إطباقية سفلية", value: "occlusal_lower" },
  { label: "أخرى", value: "other" }
] as const;

export const PHOTO_CATEGORY_LABELS: Record<string, string> = Object.fromEntries(
  PHOTO_CATEGORIES.map((item) => [item.value, item.label])
);

export const PHOTO_STAGE_OPTIONS = [
  { label: "أولية", value: "initial" },
  { label: "متابعة", value: "progress" },
  { label: "نهائية", value: "final" }
] as const;

export const PHOTO_STAGE_LABELS: Record<string, string> = Object.fromEntries(
  PHOTO_STAGE_OPTIONS.map((item) => [item.value, item.label])
);

export const XRAY_TYPES = [
  { label: "بانوراما OPG", value: "OPG" },
  { label: "سيفالومتري جانبي", value: "lateral_ceph" },
  { label: "سيفالومتري أمامي خلفي", value: "PA_ceph" },
  { label: "حول ذروية", value: "periapical" },
  { label: "Bitewing", value: "bitewing" },
  { label: "CBCT", value: "CBCT" },
  { label: "أخرى", value: "other" }
] as const;

export const XRAY_TYPE_LABELS: Record<string, string> = Object.fromEntries(
  XRAY_TYPES.map((item) => [item.value, item.label])
);

export type ClinicalPhotoItem = {
  id: string;
  category: string;
  photoType?: string | null;
  fileUrl: string;
  thumbnailUrl?: string | null;
  stage?: string | null;
  notes?: string | null;
  fileSize?: number | null;
  photoDate: string;
  orthoCaseId?: string | null;
  isActive?: boolean;
  createdAt?: string | null;
};

export type RadiographItem = {
  id: string;
  xrayType: string;
  fileUrl: string;
  fileName?: string | null;
  fileSize?: number | null;
  mimeType?: string | null;
  toothRelated?: string | null;
  notes?: string | null;
  xrayDate: string;
  doctorName?: string | null;
  orthoCaseId?: string | null;
  isActive?: boolean;
  createdAt?: string | null;
};

export type PickedClinicalFile = {
  uri: string;
  name: string;
  mimeType: string;
  size?: number | null;
};

export type UploadResult = {
  url: string;
  fileName: string;
  originalName: string;
  size: number;
  contentType: string;
};

export async function pickClinicalPhoto(): Promise<PickedClinicalFile | null> {
  const result = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ["images"],
    allowsEditing: false,
    quality: 1,
    selectionLimit: 1
  });
  if (result.canceled || !result.assets[0]) return null;
  return normalizeImageAsset(result.assets[0], "clinical-photo");
}

export async function captureClinicalPhoto(): Promise<PickedClinicalFile | null> {
  const permission = await ImagePicker.requestCameraPermissionsAsync();
  if (!permission.granted) {
    throw new Error("يلزم السماح باستخدام الكاميرا لالتقاط صورة سريرية.");
  }

  const result = await ImagePicker.launchCameraAsync({
    mediaTypes: ["images"],
    allowsEditing: false,
    quality: 0.9,
    cameraType: ImagePicker.CameraType.back
  });
  if (result.canceled || !result.assets[0]) return null;
  return normalizeImageAsset(result.assets[0], "clinical-camera");
}

export async function pickRadiographFile(): Promise<PickedClinicalFile | null> {
  return pickDocumentLikeFile("radiograph");
}

export async function pickClinicalDocument(): Promise<PickedClinicalFile | null> {
  return pickDocumentLikeFile("document");
}

async function pickDocumentLikeFile(prefix: string): Promise<PickedClinicalFile | null> {
  const result = await DocumentPicker.getDocumentAsync({
    type: ["image/jpeg", "image/png", "image/webp", "application/pdf"],
    copyToCacheDirectory: true,
    multiple: false
  });
  if (result.canceled || !result.assets[0]) return null;

  const asset = result.assets[0];
  const mimeType = normalizeMime(asset.mimeType, asset.name);
  validatePickedFile(asset.size, mimeType, CLINICAL_DOCUMENT_MIME_TYPES);
  return {
    uri: asset.uri,
    name: asset.name || `${prefix}-${Date.now()}${extensionForMime(mimeType)}`,
    mimeType,
    size: asset.size
  };
}

export async function uploadClinicalFile(
  file: PickedClinicalFile,
  patientId: string,
  purpose: "clinical-photo" | "radiograph" | "document"
): Promise<UploadResult> {
  const formData = new FormData();
  const filePart = {
    uri: file.uri,
    name: file.name,
    type: file.mimeType
  } as unknown as Blob;
  formData.append("file", filePart);
  formData.append("patientId", patientId);
  formData.append("purpose", purpose);

  return apiRequest<UploadResult>("/api/uploads", {
    method: "POST",
    body: formData
  });
}

export function formatFileSize(bytes?: number | null): string {
  if (!bytes) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function isImageMime(mimeType?: string | null): boolean {
  return !mimeType || mimeType.startsWith("image/");
}

function normalizeImageAsset(asset: ImagePicker.ImagePickerAsset, prefix: string): PickedClinicalFile {
  const fallbackName = `${prefix}-${Date.now()}.jpg`;
  const name = asset.fileName || fileNameFromUri(asset.uri) || fallbackName;
  const mimeType = normalizeMime(asset.mimeType, name);
  validatePickedFile(asset.fileSize, mimeType, PHOTO_MIME_TYPES);
  return { uri: asset.uri, name, mimeType, size: asset.fileSize };
}

function validatePickedFile(
  size: number | null | undefined,
  mimeType: string,
  allowedMimeTypes: Set<string>
) {
  if (size != null && size > MAX_FILE_SIZE) {
    throw new Error("حجم الملف يتجاوز 10 ميجابايت.");
  }
  if (!allowedMimeTypes.has(mimeType)) {
    throw new Error("نوع الملف غير مدعوم. استخدم JPG أو PNG أو WEBP، ويمكن PDF للمستندات والأشعة.");
  }
}

function normalizeMime(value: string | null | undefined, name: string): string {
  const normalized = value?.toLowerCase().trim();
  if (normalized === "image/jpg") return "image/jpeg";
  if (normalized) return normalized;

  const lower = name.toLowerCase();
  if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
  if (lower.endsWith(".png")) return "image/png";
  if (lower.endsWith(".webp")) return "image/webp";
  if (lower.endsWith(".pdf")) return "application/pdf";
  return "application/octet-stream";
}

function extensionForMime(mimeType: string): string {
  switch (mimeType) {
    case "image/jpeg": return ".jpg";
    case "image/png": return ".png";
    case "image/webp": return ".webp";
    case "application/pdf": return ".pdf";
    default: return "";
  }
}

function fileNameFromUri(uri: string): string | null {
  const [clean = ""] = uri.split("?");
  const name = clean.split("/").pop();
  return name && name.includes(".") ? decodeURIComponent(name) : null;
}
