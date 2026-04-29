// ──────────────────────────────────────────────────────────────────────────────
// AI Advisory Types — Aqlan Dental Pro Phase 6
// ──────────────────────────────────────────────────────────────────────────────

export interface AiRecommendation {
  id: string;
  context?: string;
  recommendation?: string;
  confidence?: number;
  doctorAction?: 'pending' | 'approved' | 'rejected' | 'modified';
  doctorName?: string;
  actionAt?: string;
  createdAt: string;
}

export interface ProblemListSuggestion {
  category: 'skeletal' | 'dental' | 'soft_tissue' | 'functional' | 'space';
  description: string;
  severity: 'mild' | 'moderate' | 'severe';
  reasoning: string;
}

export interface ExtractionDecisionAnalysis {
  recommendation: 'no_extraction' | 'extract_4_premolars' | 'extract_2_upper' | 'extract_2_lower' | 'borderline';
  confidence: number;
  proExtraction: string[];
  conExtraction: string[];
  clinicalReasoning: string;
  alternativeOptions: string[];
}

export interface CaseSummary {
  executiveSummary: string;
  skeletalAnalysis: string;
  dentalAnalysis: string;
  softTissueAnalysis: string;
  treatmentProgress: string;
  recommendations: string[];
  riskFactors: string[];
}

export interface VtoSuggestion {
  targetMovements: {
    landmark: string;
    deltaX: number;
    deltaY: number;
    description: string;
  }[];
  expectedOutcomes: string[];
  treatmentSequence: string[];
  estimatedDuration: string;
  warnings: string[];
}

export interface SmartAlert {
  type: 'overdue_payment' | 'delayed_ortho_case' | 'today_appointment' | 'unconfirmed_appointment';
  severity: 'info' | 'warning' | 'error';
  title: string;
  message: string;
  relatedEntity?: string;
  relatedId?: string;
  patientId?: string;
}

export interface Notification {
  id: string;
  type?: string;
  title?: string;
  body?: string;
  isRead: boolean;
  relatedEntity?: string;
  relatedId?: string;
  createdAt: string;
}

export interface SaveRecommendationRequest {
  patientId: string;
  context?: string;
  inputData?: unknown;
  recommendation?: string;
  confidence?: number;
}
