export interface Employee {
  id: string;
  userId: string;
  fullName: string;
  phone?: string;
  nationalId?: string;
  position?: string;
  branchId?: string;
  branchName?: string;
  hireDate?: string;
  baseSalary?: number;
  emergencyContact?: string;
  emergencyPhone?: string;
  notes?: string;
  username: string;
  role: string;
  userActive: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateEmployeeRequest {
  fullName: string;
  phone?: string;
  nationalId?: string;
  position?: string;
  branchId?: string;
  hireDate?: string;
  baseSalary?: number;
  emergencyContact?: string;
  emergencyPhone?: string;
  notes?: string;
  username: string;
  password?: string;
  role: string;
}

export interface UpdateEmployeeRequest {
  fullName?: string;
  phone?: string;
  nationalId?: string;
  position?: string;
  branchId?: string;
  hireDate?: string;
  baseSalary?: number;
  emergencyContact?: string;
  emergencyPhone?: string;
  notes?: string;
}

export const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

export const POSITION_LABELS: Record<string, string> = {
  doctor: "طبيب",
  reception: "موظف استقبال",
  assistant: "مساعد طبيب",
  nurse: "ممرض/ة",
  accountant: "محاسب",
  manager: "مدير",
  technician: "فني أسنان",
  cleaner: "عامل نظافة",
  other: "أخرى",
};
