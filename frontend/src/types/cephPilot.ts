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
