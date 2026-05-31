// Attendance Status enum matching backend
export type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'HalfDay' | 'Leave' | 'Holiday';

export const ATTENDANCE_STATUS_LABELS: Record<AttendanceStatus, string> = {
  Present: 'حاضر',
  Absent: 'غائب',
  Late: 'متأخر',
  HalfDay: 'نصف يوم',
  Leave: 'إجازة',
  Holiday: 'عطلة',
};

export const ATTENDANCE_STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'bg-green-100 text-green-800',
  Absent: 'bg-red-100 text-red-800',
  Late: 'bg-yellow-100 text-yellow-800',
  HalfDay: 'bg-orange-100 text-orange-800',
  Leave: 'bg-blue-100 text-blue-800',
  Holiday: 'bg-purple-100 text-purple-800',
};

export interface AttendanceRecord {
  id: string;
  employeeId: string;
  employeeName?: string;
  date: string;
  checkIn?: string;
  checkOut?: string;
  status: AttendanceStatus;
  notes?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface AttendanceSummary {
  employeeId: string;
  employeeName: string;
  presentDays: number;
  absentDays: number;
  lateDays: number;
  halfDays: number;
  leaveDays: number;
  holidays: number;
  totalWorkHours: number;
}

// Salary types
export interface SalaryRecord {
  id: string;
  employeeId: string;
  employeeName?: string;
  year: number;
  month: number;
  baseSalary: number;
  deductions: number;
  advances: number;
  bonuses: number;
  netSalary: number;
  paidAt?: string;
  paidBy?: string;
  paymentMethod?: string;
  notes?: string;
  createdAt?: string;
  updatedAt?: string;
}

export type PaymentMethod = 'Cash' | 'BankTransfer' | 'Check';

export const PAYMENT_METHOD_LABELS: Record<PaymentMethod, string> = {
  Cash: 'نقدي',
  BankTransfer: 'تحويل بنكي',
  Check: 'شيك',
};

// Advance Payment types
export type RequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export const REQUEST_STATUS_LABELS: Record<RequestStatus, string> = {
  Pending: 'قيد المراجعة',
  Approved: 'مقبول',
  Rejected: 'مرفوض',
  Cancelled: 'ملغي',
};

export const REQUEST_STATUS_COLORS: Record<RequestStatus, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Approved: 'bg-green-100 text-green-800',
  Rejected: 'bg-red-100 text-red-800',
  Cancelled: 'bg-gray-100 text-gray-800',
};

export interface AdvanceRecord {
  id: string;
  employeeId: string;
  employeeName?: string;
  amount: number;
  reason?: string;
  requestDate: string;
  status: RequestStatus;
  approvedBy?: string;
  approvedAt?: string;
  rejectionReason?: string;
  deductFromMonth?: number;
  deductFromYear?: number;
  isDeducted: boolean;
  createdAt?: string;
  updatedAt?: string;
}

// Leave types
export type LeaveType = 'Annual' | 'Sick' | 'Emergency' | 'Unpaid' | 'Maternity' | 'Hajj';

export const LEAVE_TYPE_LABELS: Record<LeaveType, string> = {
  Annual: 'سنوية',
  Sick: 'مرضية',
  Emergency: 'طارئة',
  Unpaid: 'بدون راتب',
  Maternity: 'أمومة',
  Hajj: 'حج',
};

export interface LeaveRecord {
  id: string;
  employeeId: string;
  employeeName?: string;
  leaveType: LeaveType;
  startDate: string;
  endDate: string;
  totalDays: number;
  reason?: string;
  status: RequestStatus;
  approvedBy?: string;
  approvedAt?: string;
  rejectionReason?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface LeaveBalance {
  totalEntitlement: number;
  usedDays: number;
  remainingDays: number;
}

// Employee Document types
export type EmployeeDocumentType = 'Contract' | 'ID' | 'Certificate' | 'Medical' | 'Other';

export const DOCUMENT_TYPE_LABELS: Record<EmployeeDocumentType, string> = {
  Contract: 'عقد عمل',
  ID: 'هوية',
  Certificate: 'شهادة',
  Medical: 'تقرير طبي',
  Other: 'أخرى',
};

export interface EmployeeDocument {
  id: string;
  employeeId: string;
  documentType: string;
  title: string;
  filePath: string;
  fileName?: string;
  contentType?: string;
  fileSize?: number;
  uploadedBy?: string;
  createdAt?: string;
}

// Employee for HR (extends base Employee with HR-specific fields)
export interface EmployeeForHR {
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
  isActive?: boolean;
  username?: string;
  role?: string;
}
