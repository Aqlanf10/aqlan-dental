export type CephPilotProjectStatus =
  | "Draft"
  | "ReadyForAnnotation"
  | "AnnotationInProgress"
  | "AwaitingAdjudication"
  | "AdjudicationInProgress"
  | "GoldStandardComplete"
  | "ReleaseReady"
  | "Evaluated"
  | "Archived";

export interface CephPilotProject {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  landmarkDefinitionVersion: string;
  status: CephPilotProjectStatus;
  datasetVersion?: string | null;
  revision: number;
  caseCount: number;
  readyCaseCount: number;
  myReviewerSlot?: "A" | "B" | "Adjudicator" | "Admin";
}

export type CephPilotContourDecision =
  | "NotApplicable"
  | "SingleContour"
  | "ReceptorSideContour"
  | "Unresolvable";

export interface CephPilotReviewPoint {
  landmarkKey: string;
  isCore: boolean;
  visibility: "Visible" | "NotVisible";
  xCoordPx?: number | null;
  yCoordPx?: number | null;
  contourDecision: CephPilotContourDecision;
  annotatedAt?: string;
}

export interface CephPilotReviewSession {
  id: string;
  caseId: string;
  reviewerSlot: "A" | "B";
  status: "Draft" | "Submitted";
  landmarkDefinitionVersion: string;
  toolVersion: string;
  startedAt: string;
  lastSavedAt?: string | null;
  submittedAt?: string | null;
  revision: number;
  caseCode: string;
  imageWidth: number;
  imageHeight: number;
  mmPerPixel: number;
  coreLandmarkCount: number;
  points: CephPilotReviewPoint[];
}

export interface CephPilotCase {
  id: string;
  projectId: string;
  caseCode: string;
  sourceType?: string;
  sourceReference?: string | null;
  imageWidth: number;
  imageHeight: number;
  mmPerPixel?: number | null;
  calibrationSource?: string | null;
  siteCode: string;
  deviceCode: string;
  qualityCategory: string;
  status: string;
  revision: number;
  metadataSanitized?: boolean;
  deIdentificationConfirmed?: boolean;
  pixelInspectionConfirmed?: boolean;
  noBarcodeOrQrConfirmed?: boolean;
  legalBasisConfirmed?: boolean;
  orientationConfirmed?: boolean;
  hasWebCephLandmarkTable?: boolean;
  hasWebCephMeasurementReport?: boolean;
}

export interface PilotEligibleUser {
  id: string;
  username: string;
  role: string;
  doctorName?: string | null;
  isActive: boolean;
}
