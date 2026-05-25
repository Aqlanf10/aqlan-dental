"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { useDoctors } from "@/hooks/useDoctors";
import api from "@/lib/api";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import {
  TrendingUp, Wallet, AlertCircle, FileText, Printer,
  RefreshCw, CheckCircle2, Search, Percent,
  Settings, ChevronRight, Ban, Building, Coins, ShieldAlert,
  Calendar, Landmark, X
} from "lucide-react";

// --- Types ---
interface RecentInvoice {
  id: string;
  invoiceNumber: string;
  patientName: string;
  totalAmount: number;
  status: string;
  statusArabic: string;
  createdAt: string;
}

interface PaymentListItem {
  id: string;
  patientId: string;
  patientName: string;
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  serviceDescription?: string;
  receiptNumber?: string;
  invoiceNumber?: string;
  notes?: string;
}

interface InvoiceListItem {
  id: string;
  invoiceNumber: string;
  patientId: string;
  patientName: string;
  status: string;
  statusArabic: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  createdAt: string;
}

interface ContractListItem {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  specialty: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status: string;
  startDate?: string;
}

interface OverdueContractListItem {
  contractId: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  phone?: string;
  specialty?: string;
  totalAmount: number;
  paidAmount: number;
  overdueAmount: number;
  remainingAmount: number;
  monthsElapsed?: number;
  startDate?: string;
}

interface FinanceV2Summary {
  todayCollected: number;
  monthCollected: number;
  totalOutstanding: number;
  activeContracts: number;
  unpaidInvoicesCount: number;
  draftInvoicesCount: number;
  overdueAmount: number;
  pendingCommissionsAmount: number;
  recentPayments: PaymentListItem[];
  recentInvoices: RecentInvoice[];
}

interface PatientSearchItem {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
}

interface PatientFinanceDetails {
  totalTreatmentCost: number;
  totalPaid: number;
  outstandingBalance: number;
  overdueAmount: number;
  financialStatus: string;
}

export default function FinanceV2Page() {
  const { user } = useAuthStore();
  const router = useRouter();

  const userRole = user?.role ?? "";
  const isAdmin = userRole === "Admin";
  const isReception = userRole === "Reception";
  const isDoctor = ["Orthodontist", "GeneralDentist", "OralSurgeon"].includes(userRole);

  // Active Tab
  // Reception is strictly constrained to Cashier tab only.
  const [activeTab, setActiveTab] = useState(isReception ? "cashier" : "dashboard");

  // Summary State
  const [summary, setSummary] = useState<FinanceV2Summary | null>(null);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);

  // Cashier State
  const [searchQuery, setSearchQuery] = useState("");
  const [searchingPatients, setSearchingPatients] = useState(false);
  const [patientsList, setPatientsList] = useState<PatientSearchItem[]>([]);
  const [selectedPatient, setSelectedPatient] = useState<PatientSearchItem | null>(null);
  const [patientFinance, setPatientFinance] = useState<PatientFinanceDetails | null>(null);
  const [loadingPatientFinance, setLoadingPatientFinance] = useState(false);

  // Patient Invoices & Contracts inside Cashier
  const [patientInvoices, setPatientInvoices] = useState<InvoiceListItem[]>([]);
  const [patientContracts, setPatientContracts] = useState<ContractListItem[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState<InvoiceListItem | null>(null);
  const [selectedContract, setSelectedContract] = useState<ContractListItem | null>(null);

  // Payment Form State
  const [paymentAmount, setPaymentAmount] = useState<string>("");
  const [paymentMethod, setPaymentMethod] = useState<string>("cash");
  const [serviceDescription, setServiceDescription] = useState<string>("");
  const [notes, setNotes] = useState<string>("");
  const [selectedDoctorId, setSelectedDoctorId] = useState<string>("");
  const [isSubmittingPayment, setIsSubmittingPayment] = useState(false);
  // Tracks the maximum payable amount for the selected invoice (for overpayment guard)
  const [invoiceRemainingAmount, setInvoiceRemainingAmount] = useState<number | null>(null);

  // Tabs Listings States
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([]);
  const [payments, setPayments] = useState<PaymentListItem[]>([]);
  const [contracts, setContracts] = useState<ContractListItem[]>([]);
  const [overdueContracts, setOverdueContracts] = useState<OverdueContractListItem[]>([]);
  const [loadingListings, setLoadingListings] = useState(false);

  // Close Day Box UI
  const [showCloseBoxDialog, setShowCloseBoxDialog] = useState(false);

  // Active Cashier Session States
  interface CashierSession {
    id: string;
    sessionNumber: string;
    openingTime: string;
    openingBalance: number;
    status: string;
  }
  const [activeSession, setActiveSession] = useState<CashierSession | null>(null);
  const [loadingSession, setLoadingSession] = useState(false);
  const [openingBalance, setOpeningBalance] = useState("");
  const [isOpeningSession, setIsOpeningSession] = useState(false);

  // Closing Session Form State
  const [actualClosingCash, setActualClosingCash] = useState("");
  const [actualClosingCard, setActualClosingCard] = useState("");
  const [actualClosingBank, setActualClosingBank] = useState("");
  const [closeSessionNotes, setCloseSessionNotes] = useState("");
  const [isClosingSession, setIsClosingSession] = useState(false);

  // Expenses Tab States
  interface ExpenseItem {
    id: string;
    expenseNumber: string;
    title: string;
    category: string;
    categoryArabic: string;
    amount: number;
    expenseDate: string;
    paymentMethod: string;
    supplierName?: string;
    labOrderNumber?: string;
    notes?: string;
    receiptAttachmentUrl?: string;
  }
  const [expensesList, setExpensesList] = useState<ExpenseItem[]>([]);
  const [loadingExpenses, setLoadingExpenses] = useState(false);
  const [expenseTitle, setExpenseTitle] = useState("");
  const [expenseCategory, setExpenseCategory] = useState("Rent");
  const [expenseAmount, setExpenseAmount] = useState("");
  const [expensePaymentMethod, setExpensePaymentMethod] = useState("cash");
  const [expenseDate, setExpenseDate] = useState("");
  const [expenseNotes, setExpenseNotes] = useState("");
  const [isSubmittingExpense, setIsSubmittingExpense] = useState(false);

  // Profit & Loss Report States
  interface ProfitLossReport {
    fromDate: string;
    toDate: string;
    grossRevenue: number;
    totalRefunds: number;
    netRevenue: number;
    labFees: number;
    clinicSupplies: number;
    totalDirectCosts: number;
    grossProfit: number;
    salariesPaid: number;
    commissionsPaid: number;
    rent: number;
    utilities: number;
    marketing: number;
    maintenance: number;
    taxes: number;
    opexMiscellaneous: number;
    totalOpex: number;
    netProfit: number;
  }
  const [plReport, setPlReport] = useState<ProfitLossReport | null>(null);
  const [loadingPLReport, setLoadingPLReport] = useState(false);
  const [plFromDate, setPlFromDate] = useState("");
  const [plToDate, setPlToDate] = useState("");

  const { data: doctors } = useDoctors();

  // Deny Doctor Access immediately
  useEffect(() => {
    if (isDoctor) {
      toast.error("عذرًا، لا يمتلك الأطباء صلاحية الوصول إلى الإدارة المالية.", "دخول غير مصرح");
      router.push("/");
    }
  }, [isDoctor, router]);

  // Fetch Dashboard Summary
  const fetchSummary = async () => {
    if (isReception || isDoctor) return;
    setLoadingSummary(true);
    setSummaryError(null);
    try {
      const res = await api.get<FinanceV2Summary>("/api/finance/summary");
      setSummary(res.data);
    } catch (err) {
      console.error(err);
      setSummaryError("تعذر تحميل الملخص المالي. الرجاء التحقق من اتصالك بالخادم.");
    } finally {
      setLoadingSummary(false);
    }
  };

  const fetchActiveSession = async () => {
    setLoadingSession(true);
    try {
      const res = await api.get<CashierSession>("/api/cashier-sessions/active");
      setActiveSession(res.data);
    } catch (err) {
      console.warn("No active cashier session found.");
      setActiveSession(null);
    } finally {
      setLoadingSession(false);
    }
  };

  const handleOpenSession = async (e: React.FormEvent) => {
    e.preventDefault();
    const balanceNum = parseFloat(openingBalance);
    if (isNaN(balanceNum) || balanceNum < 0) {
      toast.error("الرجاء إدخال عهدة افتتاحية صحيحة.", "مبلغ غير صالح");
      return;
    }
    setIsOpeningSession(true);
    try {
      const res = await api.post<{ sessionNumber: string; id: string }>("/api/cashier-sessions/open", {
        openingBalance: balanceNum,
        notes: "افتتاح الصندوق اليومي"
      });
      toast.success(`تم فتح صندوق الاستقبال بنجاح. رقم الوردية: ${res.data.sessionNumber}`, "الخزنة مفتوحة");
      setOpeningBalance("");
      fetchActiveSession();
    } catch (err) {
      console.error(err);
      toast.error("تعذر فتح وردية الصندوق اليومي.", "فشل الإجراء");
    } finally {
      setIsOpeningSession(false);
    }
  };

  const handleCloseSession = async (e: React.FormEvent) => {
    e.preventDefault();
    const cashNum = parseFloat(actualClosingCash) || 0;
    const cardNum = parseFloat(actualClosingCard) || 0;
    const bankNum = parseFloat(actualClosingBank) || 0;

    setIsClosingSession(true);
    try {
      const res = await api.post<{ shortageOrSurplus: number; sessionNumber: string }>("/api/cashier-sessions/close", {
        actualClosingCash: cashNum,
        actualClosingCard: cardNum,
        actualClosingBank: bankNum,
        notes: closeSessionNotes.trim() || null
      });

      const shortage = res.data.shortageOrSurplus;
      if (shortage === 0) {
        toast.success(`تم إقفال الوردية ${res.data.sessionNumber} بنجاح ومطابقتها 100%!`, "تم الإقفال والمطابقة");
      } else if (shortage < 0) {
        toast.info(`تم إقفال الوردية ${res.data.sessionNumber} بنجاح مع وجود عجز قدره ${Math.abs(shortage).toLocaleString()} ر.ي`, "تم الإقفال مع عجز");
      } else {
        toast.success(`تم إقفال الوردية ${res.data.sessionNumber} بنجاح مع وجود فائض قدره ${shortage.toLocaleString()} ر.ي`, "تم الإقفال مع فائض");
      }

      setActualClosingCash("");
      setActualClosingCard("");
      setActualClosingBank("");
      setCloseSessionNotes("");
      setShowCloseBoxDialog(false);
      setActiveSession(null);
      fetchSummary();
    } catch (err) {
      console.error(err);
      toast.error("تعذر إقفال وردية الصندوق.", "فشل الإجراء");
    } finally {
      setIsClosingSession(false);
    }
  };

  const fetchExpenses = async () => {
    setLoadingExpenses(true);
    try {
      const res = await api.get<{ data: ExpenseItem[] }>("/api/expenses?pageSize=50");
      setExpensesList(res.data.data ?? []);
    } catch (err) {
      console.error("Failed to load expenses list", err);
    } finally {
      setLoadingExpenses(false);
    }
  };

  const handleRegisterExpense = async (e: React.FormEvent) => {
    e.preventDefault();
    const amtNum = parseFloat(expenseAmount);
    if (isNaN(amtNum) || amtNum <= 0) {
      toast.error("الرجاء إدخال مبلغ مصروف صحيح.", "مبلغ غير صالح");
      return;
    }
    setIsSubmittingExpense(true);
    try {
      await api.post("/api/expenses", {
        title: expenseTitle.trim(),
        category: expenseCategory,
        amount: amtNum,
        expenseDate: expenseDate || null,
        paymentMethod: expensePaymentMethod,
        notes: expenseNotes.trim() || null
      });
      toast.success("تم قيد المصروف والترحيل المالي للمركز بنجاح", "تم قيد المصروف");
      setExpenseTitle("");
      setExpenseAmount("");
      setExpenseNotes("");
      fetchExpenses();
      fetchSummary();
    } catch (err) {
      console.error(err);
      toast.error("تعذر قيد المصروف التشغيلي.", "فشل قيد المصروف");
    } finally {
      setIsSubmittingExpense(false);
    }
  };

  const handleDeleteExpense = async (id: string) => {
    if (!confirm("هل أنت متأكد من رغبتك في حذف قيد هذا المصروف وإلغاء ترحيله المالي بالكامل؟")) return;
    try {
      await api.delete(`/api/expenses/${id}`);
      toast.success("تم حذف قيد المصروف وإلغاء خصمه المالي بنجاح", "تم الحذف");
      fetchExpenses();
      fetchSummary();
    } catch (err) {
      console.error(err);
      toast.error("تعذر حذف قيد المصروف.", "فشل الحذف");
    }
  };

  const fetchPLReport = async () => {
    setLoadingPLReport(true);
    try {
      const url = `/api/reports/profit-loss${plFromDate || plToDate ? `?from=${plFromDate}&to=${plToDate}` : ""}`;
      const res = await api.get<ProfitLossReport>(url);
      setPlReport(res.data);
    } catch (err) {
      console.error("Failed to fetch P&L report", err);
    } finally {
      setLoadingPLReport(false);
    }
  };

  useEffect(() => {
    fetchSummary();
    fetchActiveSession();
  }, []);

  // Fetch Listing tabs when active tab changes
  useEffect(() => {
    if (isDoctor) return;
    const fetchListings = async () => {
      setLoadingListings(true);
      try {
        if (activeTab === "invoices") {
          const res = await api.get<{ invoices: InvoiceListItem[] }>("/api/invoices?pageSize=50");
          setInvoices(res.data.invoices ?? []);
        } else if (activeTab === "payments") {
          const res = await api.get<{ payments: PaymentListItem[] }>("/api/payments?pageSize=50");
          setPayments(res.data.payments ?? []);
        } else if (activeTab === "contracts") {
          const res = await api.get<ContractListItem[]>("/api/finance/contracts?pageSize=50");
          setContracts(res.data ?? []);
        } else if (activeTab === "overdue") {
          const res = await api.get<OverdueContractListItem[]>("/api/finance/overdue");
          setOverdueContracts(res.data ?? []);
        } else if (activeTab === "expenses") {
          fetchExpenses();
        } else if (activeTab === "reports") {
          fetchPLReport();
        }
      } catch (err) {
        console.error("Failed to fetch listings", err);
      } finally {
        setLoadingListings(false);
      }
    };
    fetchListings();
  }, [activeTab]);

  // Authenticated PDF Receipt trigger
  const handlePrintReceipt = async (paymentId: string, receiptNum?: string) => {
    try {
      toast.info("يتم تحضير السند المالي للطباعة...", "جاري توليد المستند");
      const response = await api.get(`/api/payments/${paymentId}/pdf`, { responseType: "blob" });
      const blob = new Blob([response.data], { type: "application/pdf" });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `سند_قبض_${receiptNum ?? paymentId}.pdf`);
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (err) {
      console.error("PDF Print error", err);
      toast.error("تعذر تحميل ملف PDF الخاص بسند القبض.", "خطأ في الطباعة");
    }
  };

  // Search Patient
  const handleSearchPatient = async (q: string) => {
    setSearchQuery(q);
    if (q.trim().length < 2) {
      setPatientsList([]);
      return;
    }
    setSearchingPatients(true);
    try {
      const res = await api.get<{ patients: { id: string; patientNumber: string; firstName: string; lastName: string; }[] }>(`/api/patients?search=${encodeURIComponent(q)}&pageSize=10`);
      const mapped = (res.data.patients ?? []).map((p) => ({
        id: p.id,
        patientNumber: p.patientNumber,
        fullName: `${p.firstName} ${p.lastName}`.trim()
      }));
      setPatientsList(mapped);
    } catch (err) {
      console.error(err);
    } finally {
      setSearchingPatients(false);
    }
  };

  // Select Patient inside Cashier
  const handleSelectPatient = async (patient: PatientSearchItem) => {
    setSelectedPatient(patient);
    setPatientsList([]);
    setSearchQuery("");
    setLoadingPatientFinance(true);
    setSelectedInvoice(null);
    setSelectedContract(null);
    setPaymentAmount("");
    setServiceDescription("");

    try {
      // 1. Fetch Finance Summary
      const summaryRes = await api.get<PatientFinanceDetails>(`/api/patients/${patient.id}/finance-summary`);
      setPatientFinance(summaryRes.data);

      // 2. Fetch Invoices
      const invoicesRes = await api.get<InvoiceListItem[]>(`/api/patients/${patient.id}/invoices`);
      // Only keep Issued (Unpaid) invoices for checkout
      setPatientInvoices(invoicesRes.data.filter((i) => i.status === "Issued") ?? []);

      // 3. Fetch Contracts
      const contractsRes = await api.get<ContractListItem[]>(`/api/finance/contracts?patientId=${patient.id}`);
      setPatientContracts(contractsRes.data ?? []);
    } catch (err) {
      console.error(err);
      toast.error("تعذر تحميل البيانات المالية للمريض المحدد.", "خطأ في التحميل");
    } finally {
      setLoadingPatientFinance(false);
    }
  };

  // Auto-fill form from selected invoice
  const handleSelectInvoiceToPay = (inv: InvoiceListItem) => {
    setSelectedInvoice(inv);
    setSelectedContract(null);
    setInvoiceRemainingAmount(null); // reset while fetching
    // Fetch the precise remaining balance from the invoice detail endpoint
    api.get<{ remainingAmount?: number }>(`/api/invoices/${inv.id}`).then((res) => {
      const remaining = res.data.remainingAmount ?? inv.totalAmount;
      setInvoiceRemainingAmount(remaining);
      setPaymentAmount(String(remaining));
      setServiceDescription(`سداد فاتورة رقم ${inv.invoiceNumber}`);
    }).catch(() => {
      const fallback = inv.totalAmount;
      setInvoiceRemainingAmount(fallback);
      setPaymentAmount(String(fallback));
      setServiceDescription(`سداد فاتورة رقم ${inv.invoiceNumber}`);
    });
  };

  const handleSelectContractToPay = (con: ContractListItem) => {
    setSelectedContract(con);
    setSelectedInvoice(null);
    setInvoiceRemainingAmount(null);
    setPaymentAmount(String(con.remainingAmount));
    setServiceDescription(`سداد قسط من عقد علاج ${con.specialty === "ortho" ? "تقويم" : "أسنان"}`);
  };



  // Register Payment
  const handleRegisterPayment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPatient) return;
    const amountNum = parseFloat(paymentAmount);
    if (isNaN(amountNum) || amountNum <= 0) {
      toast.error("الرجاء إدخال مبلغ صحيح أكبر من الصفر.", "مبلغ غير صالح");
      return;
    }

    // Issue 2: Require selection of invoice or contract for ALL roles — no unlinked payments.
    if (!selectedInvoice && !selectedContract) {
      toast.error("يجب ربط الدفعة بفاتورة مستحقة أو عقد نشط. الدفعات الحرة غير مسموح بها في هذه الواجهة.", "ربط مستند إلزامي");
      return;
    }

    // Issue 3: Client-side overpayment guard for invoice payments.
    if (selectedInvoice && invoiceRemainingAmount !== null && amountNum > invoiceRemainingAmount) {
      toast.error(`المبلغ المدخل (${amountNum.toLocaleString('ar-YE')} ر.ي) يتجاوز الرصيد المتبقي للفاتورة (${invoiceRemainingAmount.toLocaleString('ar-YE')} ر.ي). الرجاء تعديل المبلغ.`, "تجاوز الرصيد المتاح");
      return;
    }

    setIsSubmittingPayment(true);
    try {
      const payload = {
        patientId: selectedPatient.id,
        invoiceId: selectedInvoice?.id ?? null,
        contractId: selectedContract?.id ?? null,
        amount: amountNum,
        paymentMethod: paymentMethod,
        serviceDescription: serviceDescription.trim() || "دفعة مالية مقبوضة",
        doctorId: selectedDoctorId || null,
        notes: notes.trim() || null
      };

      const res = await api.post<{ receiptNumber: string; id: string }>("/api/payments", payload);
      toast.success(`رقم السند: ${res.data.receiptNumber} بمبلغ ${formatYemeniRiyal(amountNum)}`, "تم استلام الدفعة بنجاح");

      // Reload patient details
      handleSelectPatient(selectedPatient);
      // Fetch latest summary to keep counts current
      fetchSummary();

      // Trigger instant PDF download
      handlePrintReceipt(res.data.id, res.data.receiptNumber);

    } catch (err) {
      console.error(err);
      const apiError = err as { response?: { data?: { message?: string } } };
      toast.error(apiError.response?.data?.message ?? "حدث خطأ غير متوقع أثناء المعالجة.", "فشل تسجيل الدفعة");
    } finally {
      setIsSubmittingPayment(false);
    }
  };

  if (isDoctor) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center p-8 bg-white border border-red-100 rounded-2xl shadow-sm">
        <Ban className="w-16 h-16 text-red-500 mb-4 animate-bounce" />
        <h2 className="text-xl font-bold text-gray-900 mb-2">غير مصرح بالدخول</h2>
        <p className="text-gray-500 max-w-md">لا يمتلك حساب الطبيب صلاحيات استعراض الإدارة المالية V2 أو تسجيل المدفوعات.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4 bg-white p-5 rounded-2xl border border-gray-100 shadow-sm">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-black text-gray-900">الإدارة المالية</h1>
            <span className="bg-clinic-blue/10 text-clinic-blue text-[10px] font-bold px-2 py-0.5 rounded-full border border-clinic-blue/20">V2 جديد</span>
          </div>
          <p className="text-xs text-gray-500 mt-1">مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان</p>
        </div>
        <div className="flex items-center gap-2 w-full md:w-auto">
          {activeTab === "dashboard" && !isReception && (
            <button
              onClick={fetchSummary}
              disabled={loadingSummary}
              className="flex items-center justify-center gap-1.5 px-4 py-2 text-xs font-semibold text-[#1a3a5c] bg-gray-50 border border-gray-200 rounded-xl hover:bg-gray-100 transition disabled:opacity-60 w-full md:w-auto"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loadingSummary ? 'animate-spin' : ''}`} />
              تحديث البيانات
            </button>
          )}
          <button
            onClick={() => setShowCloseBoxDialog(true)}
            className="flex items-center justify-center gap-1.5 px-4 py-2 text-xs font-semibold text-white bg-[#f5922e] rounded-xl hover:opacity-90 transition w-full md:w-auto"
          >
            <Building className="w-3.5 h-3.5" />
            إغلاق الصندوق اليومي
          </button>
        </div>
      </div>

      {/* Tabs Layout */}
      <div className="flex flex-col lg:flex-row gap-6 items-start">
        {/* Navigation Sidebar of Tabs */}
        <div className="w-full lg:w-64 bg-white border border-gray-100 rounded-2xl p-4 shadow-sm space-y-1">
          <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider px-3 mb-2">الأقسام المالية</p>
          
          {!isReception && (
            <button
              onClick={() => setActiveTab("dashboard")}
              className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                activeTab === "dashboard"
                  ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                  : "text-gray-600 hover:bg-gray-50"
              }`}
            >
              <TrendingUp className="w-4 h-4" />
              لوحة التحكم
            </button>
          )}

          <button
            onClick={() => setActiveTab("cashier")}
            className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
              activeTab === "cashier"
                ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                : "text-gray-600 hover:bg-gray-50"
            }`}
          >
            <Wallet className="w-4 h-4" />
            صندوق اليوم (الكاشير)
          </button>

          {!isReception && (
            <>
              <button
                onClick={() => setActiveTab("invoices")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "invoices"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <FileText className="w-4 h-4" />
                الفواتير
              </button>

              <button
                onClick={() => setActiveTab("payments")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "payments"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <Landmark className="w-4 h-4" />
                السندات المقبوضة
              </button>

              <button
                onClick={() => setActiveTab("contracts")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "contracts"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <Calendar className="w-4 h-4" />
                عقود التقويم والأقساط
              </button>

              <button
                onClick={() => setActiveTab("overdue")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "overdue"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <AlertCircle className="w-4 h-4" />
                المتأخرات المالية
              </button>

              <button
                onClick={() => setActiveTab("commissions")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "commissions"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <Percent className="w-4 h-4" />
                عمولات الأطباء
              </button>

              <button
                onClick={() => setActiveTab("expenses")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "expenses"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <Landmark className="w-4 h-4" />
                المصروفات التشغيلية
              </button>

              <button
                onClick={() => setActiveTab("reports")}
                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                  activeTab === "reports"
                    ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                    : "text-gray-600 hover:bg-gray-50"
                }`}
              >
                <Coins className="w-4 h-4" />
                التقارير المالية
              </button>
            </>
          )}

          {isAdmin && (
            <button
              onClick={() => setActiveTab("settings")}
              className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-bold transition-all ${
                activeTab === "settings"
                  ? "bg-clinic-blue/10 text-clinic-blue border-r-4 border-clinic-blue"
                  : "text-gray-600 hover:bg-gray-50"
              }`}
            >
              <Settings className="w-4 h-4" />
              إعدادات المالية
            </button>
          )}
        </div>

        {/* Dynamic Panels */}
        <div className="flex-1 w-full">
          {/* TAB 1: DASHBOARD */}
          {activeTab === "dashboard" && !isReception && (
            <div className="space-y-6">
              {/* Summary Cards */}
              {loadingSummary ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 animate-pulse">
                  {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-28 bg-gray-100 rounded-2xl" />)}
                </div>
              ) : summaryError ? (
                <div className="flex items-center gap-3 bg-red-50 border border-red-200 rounded-2xl px-5 py-4 text-red-700">
                  <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
                  <p className="text-xs">{summaryError}</p>
                </div>
              ) : summary ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                  {/* Card 1 */}
                  <div className="bg-gradient-to-br from-[#1a3a5c] to-[#244b73] text-white p-5 rounded-2xl shadow-sm border border-[#1a3a5c]/20">
                    <div className="flex justify-between items-start">
                      <p className="text-[10px] font-bold text-gray-200">متحصل اليوم</p>
                      <TrendingUp className="w-4 h-4 text-[#f5922e]" />
                    </div>
                    <h3 className="text-xl font-extrabold font-mono mt-3">{formatYemeniRiyal(summary.todayCollected)}</h3>
                    <p className="text-[9px] text-gray-300 mt-2">مجموع المدفوعات المسجلة اليوم</p>
                  </div>

                  {/* Card 2 */}
                  <div className="bg-white p-5 rounded-2xl shadow-sm border border-gray-100">
                    <div className="flex justify-between items-start">
                      <p className="text-[10px] font-bold text-gray-400">متحصل هذا الشهر</p>
                      <Wallet className="w-4 h-4 text-clinic-blue" />
                    </div>
                    <h3 className="text-xl font-extrabold font-mono mt-3 text-gray-900">{formatYemeniRiyal(summary.monthCollected)}</h3>
                    <p className="text-[9px] text-gray-400 mt-2">منذ بداية الشهر الجاري</p>
                  </div>

                  {/* Card 3 */}
                  <div className="bg-white p-5 rounded-2xl shadow-sm border border-gray-100">
                    <div className="flex justify-between items-start">
                      <p className="text-[10px] font-bold text-gray-400">إجمالي الذمم المستحقة</p>
                      <AlertCircle className="w-4 h-4 text-red-500" />
                    </div>
                    <h3 className="text-xl font-extrabold font-mono mt-3 text-red-600">{formatYemeniRiyal(summary.totalOutstanding)}</h3>
                    <p className="text-[9px] text-gray-400 mt-2">تراكمي فواتير + عقود تقويم مستمرة</p>
                  </div>

                  {/* Card 4 */}
                  <div className="bg-white p-5 rounded-2xl shadow-sm border border-gray-100">
                    <div className="flex justify-between items-start">
                      <p className="text-[10px] font-bold text-gray-400">المتأخرات النشطة</p>
                      <ShieldAlert className="w-4 h-4 text-amber-500" />
                    </div>
                    <h3 className="text-xl font-extrabold font-mono mt-3 text-amber-600">{formatYemeniRiyal(summary.overdueAmount)}</h3>
                    <p className="text-[9px] text-gray-400 mt-2">دفعات أقساط عقود استحقت ولم تُدفع</p>
                  </div>
                </div>
              ) : null}

              {/* Extra KPIs */}
              {summary && (
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <div className="bg-gray-50 border border-gray-200 p-4 rounded-xl flex items-center justify-between">
                    <div>
                      <p className="text-[10px] font-bold text-gray-500">الفواتير المستحقة (غير مسددة)</p>
                      <p className="text-lg font-black text-gray-800 mt-1">{summary.unpaidInvoicesCount}</p>
                    </div>
                    <FileText className="w-5 h-5 text-gray-400" />
                  </div>
                  <div className="bg-gray-50 border border-gray-200 p-4 rounded-xl flex items-center justify-between">
                    <div>
                      <p className="text-[10px] font-bold text-gray-500">مسودات الفواتير</p>
                      <p className="text-lg font-black text-gray-800 mt-1">{summary.draftInvoicesCount}</p>
                    </div>
                    <FileText className="w-5 h-5 text-gray-400" />
                  </div>
                  <div className="bg-gray-50 border border-gray-200 p-4 rounded-xl flex items-center justify-between">
                    <div>
                      <p className="text-[10px] font-bold text-gray-500">العقود النشطة</p>
                      <p className="text-lg font-black text-gray-800 mt-1">{summary.activeContracts}</p>
                    </div>
                    <Calendar className="w-5 h-5 text-gray-400" />
                  </div>
                  <div className="bg-gray-50 border border-gray-200 p-4 rounded-xl flex items-center justify-between">
                    <div>
                      <p className="text-[10px] font-bold text-gray-500">عمولات الأطباء المعلقة</p>
                      <p className="text-lg font-black text-green-700 mt-1">{formatYemeniRiyal(summary.pendingCommissionsAmount)}</p>
                    </div>
                    <Percent className="w-5 h-5 text-green-600" />
                  </div>
                </div>
              )}

              {/* Alerts Section */}
              {summary && (summary.unpaidInvoicesCount > 0 || summary.overdueAmount > 0) && (
                <div className="bg-amber-50 border border-amber-200 p-4 rounded-2xl text-amber-800 space-y-2">
                  <div className="flex items-center gap-2">
                    <ShieldAlert className="w-4 h-4 text-amber-600" />
                    <span className="font-extrabold text-xs">تنبيهات المتابعة المالية اليومية</span>
                  </div>
                  <ul className="text-xs space-y-1.5 list-disc list-inside">
                    {summary.unpaidInvoicesCount > 0 && (
                      <li>يوجد عدد <strong className="font-bold">{summary.unpaidInvoicesCount}</strong> فواتير مصدرة معلقة بحاجة للتحصيل أو المتابعة.</li>
                    )}
                    {summary.overdueAmount > 0 && (
                      <li>تجاوز إجمالي الأقساط المتأخرة للمرضى المستحقة <strong className="font-bold">{formatYemeniRiyal(summary.overdueAmount)}</strong>. يرجى مراجعة قسم المتأخرات للتواصل مع المرضى.</li>
                    )}
                  </ul>
                </div>
              )}

              {/* Side by Side Tables */}
              {summary && (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                  {/* Recent Payments Card */}
                  <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
                    <div className="flex justify-between items-center">
                      <h3 className="font-extrabold text-xs text-gray-900">آخر المقبوضات والسندات</h3>
                      <button onClick={() => setActiveTab("payments")} className="text-[10px] text-clinic-blue font-bold hover:underline">عرض الكل</button>
                    </div>
                    {!summary.recentPayments.length ? (
                      <p className="text-xs text-gray-400 text-center py-8">لا توجد مقبوضات مقيدة مؤخرًا</p>
                    ) : (
                      <div className="divide-y divide-gray-50 max-h-[400px] overflow-y-auto pr-1">
                        {summary.recentPayments.map((p) => (
                          <div key={p.id} className="py-3 flex justify-between items-center gap-2">
                            <div>
                              <p className="text-xs font-bold text-gray-900">{p.patientName}</p>
                              <p className="text-[9px] text-gray-400 mt-1">{formatArabicDate(p.paymentDate)} · {p.paymentMethod === 'cash' ? 'نقدي' : 'بطاقة / تحويل'}</p>
                            </div>
                            <div className="text-left">
                              <span className="text-xs font-bold text-green-600 block">{formatYemeniRiyal(p.amount)}</span>
                              <button
                                onClick={() => handlePrintReceipt(p.id, p.receiptNumber)}
                                className="text-[9px] text-clinic-blue mt-1 hover:underline flex items-center gap-0.5 inline-flex"
                              >
                                <Printer className="w-2.5 h-2.5" />
                                طباعة السند
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Recent Invoices Card */}
                  <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
                    <div className="flex justify-between items-center">
                      <h3 className="font-extrabold text-xs text-gray-900">آخر الفواتير الصادرة</h3>
                      <button onClick={() => setActiveTab("invoices")} className="text-[10px] text-clinic-blue font-bold hover:underline">عرض الكل</button>
                    </div>
                    {!summary.recentInvoices.length ? (
                      <p className="text-xs text-gray-400 text-center py-8">لا توجد فواتير صادرة مؤخرًا</p>
                    ) : (
                      <div className="divide-y divide-gray-50 max-h-[400px] overflow-y-auto pr-1">
                        {summary.recentInvoices.map((inv) => (
                          <div key={inv.id} className="py-3 flex justify-between items-center gap-2">
                            <div>
                              <p className="text-xs font-bold text-gray-900">{inv.patientName}</p>
                              <p className="text-[9px] text-gray-400 mt-1">{inv.invoiceNumber} · {formatArabicDate(inv.createdAt)}</p>
                            </div>
                            <div className="text-left">
                              <span className="text-xs font-bold text-gray-800 block">{formatYemeniRiyal(inv.totalAmount)}</span>
                              <span className={`text-[8px] px-1.5 py-0.5 rounded-full inline-block mt-1 ${
                                inv.status === 'Paid' ? 'bg-green-50 text-green-600' : 'bg-red-50 text-red-600'
                              }`}>{inv.statusArabic}</span>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* TAB 2: CASHIER / DAY BOX */}
          {activeTab === "cashier" && (
            <div className="space-y-6">
              {loadingSession ? (
                <div className="bg-white border border-gray-100 p-8 rounded-2xl shadow-sm text-center animate-pulse">
                  <RefreshCw className="w-8 h-8 text-clinic-blue mx-auto animate-spin" />
                  <p className="text-xs text-gray-500 mt-3 font-bold">جاري مراجعة حالة الصندوق...</p>
                </div>
              ) : !activeSession ? (
                // 🔹 Session Opening Form
                <div className="bg-white border border-gray-100 p-8 rounded-2xl shadow-sm space-y-6 text-center max-w-xl mx-auto my-8">
                  <ShieldAlert className="w-14 h-14 text-amber-500 mx-auto animate-bounce" />
                  <div className="space-y-2">
                    <h3 className="text-lg font-black text-[#1a3a5c]">صندوق الاستقبال (الخزنة اليومية) مغلق</h3>
                    <p className="text-xs text-gray-500 max-w-md mx-auto leading-relaxed">
                      لحماية السيولة وضبط القيود المحاسبية، يرجى تأكيد العهدة الافتتاحية وفتح صندوق اليوم للبدء بتحصيل فواتير وأقساط المرضى وطباعة سندات القبض.
                    </p>
                  </div>
                  
                  <form onSubmit={handleOpenSession} className="space-y-4 text-right max-w-xs mx-auto">
                    <div>
                      <label className="block text-[10px] font-bold text-gray-400 mb-1.5">مبلغ العهدة الافتتاحية بالدرج (ريال يمني):</label>
                      <input
                        type="number"
                        placeholder="أدخل مبلغ العهدة بالدرج (مثلاً 10,000)..."
                        value={openingBalance}
                        onChange={(e) => setOpeningBalance(e.target.value)}
                        className="w-full text-xs font-mono text-center font-bold px-3 py-3 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                        required
                        min="0"
                      />
                    </div>
                    <button
                      type="submit"
                      disabled={isOpeningSession}
                      className="w-full py-3 text-xs font-bold text-white bg-clinic-blue rounded-xl hover:opacity-90 transition disabled:opacity-60"
                    >
                      {isOpeningSession ? "جاري فتح الصندوق..." : "تأكيد وفتح الصندوق لليوم"}
                    </button>
                  </form>
                </div>
              ) : (
                <>
                  <div className="bg-green-50 border border-green-200 p-4 rounded-xl flex items-center justify-between text-green-800 text-xs">
                    <div className="flex items-center gap-2">
                      <CheckCircle2 className="w-4 h-4 text-green-600 shrink-0" />
                      <span>الصندوق اليومي مفتوح بنجاح. رقم الوردية: <strong className="font-extrabold">{activeSession.sessionNumber}</strong>. تم الفتح في {new Date(activeSession.openingTime).toLocaleTimeString('ar-YE', {hour: '2-digit', minute: '2-digit'})}</span>
                    </div>
                    <span className="bg-green-600 text-white text-[9px] px-2 py-0.5 rounded-full font-bold">نشط</span>
                  </div>

                  {/* Patient Selector Workspace */}
                  <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                <h3 className="font-extrabold text-sm text-gray-900">البحث المالي وسداد المرضى</h3>
                
                <div className="relative">
                  <input
                    type="text"
                    placeholder="ابحث عن المريض بالاسم أو رقم الملف للبدء بالسداد..."
                    value={searchQuery}
                    onChange={(e) => handleSearchPatient(e.target.value)}
                    className="w-full text-xs px-10 py-3 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                  />
                  <Search className="w-4 h-4 text-gray-400 absolute right-3.5 top-3.5" />
                  
                  {searchingPatients && (
                    <div className="absolute left-3.5 top-3.5 animate-spin">
                      <RefreshCw className="w-4 h-4 text-clinic-blue" />
                    </div>
                  )}

                  {/* Autocomplete Dropdown */}
                  {patientsList.length > 0 && (
                    <div className="absolute w-full bg-white border border-gray-100 rounded-xl mt-1 shadow-lg z-20 divide-y divide-gray-50 max-h-60 overflow-y-auto">
                      {patientsList.map((p) => (
                        <button
                          key={p.id}
                          onClick={() => handleSelectPatient(p)}
                          className="w-full text-right px-4 py-2.5 hover:bg-gray-50 text-xs font-semibold flex items-center justify-between"
                        >
                          <span>{p.fullName}</span>
                          <span className="text-[10px] text-gray-400 font-mono">#{p.patientNumber}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {selectedPatient && (
                  <div className="flex flex-wrap items-center justify-between gap-3 bg-clinic-blue/5 p-4 rounded-xl border border-clinic-blue/10">
                    <div>
                      <span className="text-[10px] text-clinic-blue font-bold block">الملف النشط حالياً</span>
                      <p className="text-xs font-extrabold text-gray-900 mt-1">{selectedPatient.fullName}</p>
                    </div>
                    <button
                      onClick={() => {
                        setSelectedPatient(null);
                        setPatientFinance(null);
                        setPatientInvoices([]);
                        setPatientContracts([]);
                      }}
                      className="text-[10px] text-red-500 font-bold hover:underline"
                    >
                      إلغاء المريض الحالي
                    </button>
                  </div>
                )}
              </div>

              {/* Patient Finance Summary & Checkout Forms */}
              {selectedPatient && (
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
                  
                  {/* Left Workspace Column: Forms & Unpaid Bills */}
                  <div className="lg:col-span-2 space-y-6">
                    {loadingPatientFinance ? (
                      <div className="bg-white h-48 rounded-2xl animate-pulse" />
                    ) : (
                      <>
                        {/* 1. Unpaid Invoices Selection */}
                        <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                          <h4 className="font-extrabold text-xs text-gray-900">الفواتير المستحقة غير المسددة</h4>
                          {!patientInvoices.length ? (
                            <div className="text-center py-6">
                              <CheckCircle2 className="w-8 h-8 text-green-500 mx-auto mb-2" />
                              <p className="text-xs text-gray-500">لا توجد فواتير مستحقة الدفع لهذا المريض حالياً.</p>
                            </div>
                          ) : (
                            <div className="space-y-2 max-h-[300px] overflow-y-auto">
                              {patientInvoices.map((inv) => (
                                <div
                                  key={inv.id}
                                  onClick={() => handleSelectInvoiceToPay(inv)}
                                  className={`p-3 rounded-xl border transition cursor-pointer flex items-center justify-between ${
                                    selectedInvoice?.id === inv.id
                                      ? 'border-clinic-blue bg-clinic-blue/5'
                                      : 'border-gray-100 hover:border-gray-200'
                                  }`}
                                >
                                  <div>
                                    <p className="text-xs font-bold text-gray-800">فاتورة #{inv.invoiceNumber}</p>
                                    <p className="text-[9px] text-gray-400 mt-1">تاريخ الإصدار: {formatArabicDate(inv.createdAt)}</p>
                                  </div>
                                  <div className="text-left">
                                    <span className="text-xs font-black text-[#1a3a5c] block">{formatYemeniRiyal(inv.totalAmount)}</span>
                                    <span className="text-[9px] text-clinic-blue mt-1 block">اضغط للسداد التلقائي</span>
                                  </div>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>

                        {/* 2. Active Contracts */}
                        <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                          <h4 className="font-extrabold text-xs text-gray-900">العقود النشطة والأقساط المستمرة</h4>
                          {!patientContracts.length ? (
                            <p className="text-xs text-gray-400 py-2">لا توجد عقود تقويم أو أقساط مسجلة للمريض.</p>
                          ) : (
                            <div className="space-y-2">
                              {patientContracts.map((con) => (
                                <div
                                  key={con.id}
                                  onClick={() => handleSelectContractToPay(con)}
                                  className={`p-3 rounded-xl border transition cursor-pointer flex items-center justify-between ${
                                    selectedContract?.id === con.id
                                      ? 'border-clinic-blue bg-clinic-blue/5'
                                      : 'border-gray-100 hover:border-gray-200'
                                  }`}
                                >
                                  <div>
                                    <p className="text-xs font-bold text-gray-800">عقد تقويم - {con.specialty === "ortho" ? "تقويم أسنان" : "علاج"}</p>
                                    <p className="text-[9px] text-gray-400 mt-1">تاريخ البدء: {con.startDate ? formatArabicDate(con.startDate) : "غير محدد"}</p>
                                  </div>
                                  <div className="text-left">
                                    <span className="text-xs font-black text-amber-600 block">{formatYemeniRiyal(con.remainingAmount)} متبقي</span>
                                    <span className="text-[9px] text-clinic-blue mt-1 block">اضغط لسداد قسط</span>
                                  </div>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>

                        {/* 3. Safe Register Payment Form */}
                        <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm">
                          <h4 className="font-extrabold text-xs text-gray-900 mb-4">تسجيل الدفعة النقدية</h4>
                          <form onSubmit={handleRegisterPayment} className="space-y-4">
                            
                            {/* Warnings for unlinked payments — blocked for ALL roles in Finance V2 */}
                            {!selectedInvoice && !selectedContract && (
                              <div className="bg-red-50 border border-red-200 p-3 rounded-xl flex items-start gap-2.5 text-red-800">
                                <ShieldAlert className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                                <div className="text-xs">
                                  <p className="font-bold">يجب اختيار فاتورة أو عقد للمتابعة</p>
                                  <p className="text-[10px] mt-0.5 text-gray-600">
                                    لا يُسمح بتسجيل دفعة حرة غير مرتبطة بمستند مالي في هذه الواجهة. الرجاء اختيار فاتورة مستحقة أو عقد نشط من القائمة أعلاه قبل المتابعة.
                                  </p>
                                </div>
                              </div>
                            )}

                            {/* Overpayment warning for invoice payments */}
                            {selectedInvoice && invoiceRemainingAmount !== null && parseFloat(paymentAmount) > invoiceRemainingAmount && (
                              <div className="bg-amber-50 border border-amber-200 p-3 rounded-xl flex items-start gap-2.5 text-amber-800">
                                <ShieldAlert className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
                                <div className="text-xs">
                                  <p className="font-bold">المبلغ يتجاوز الرصيد المتبقي للفاتورة</p>
                                  <p className="text-[10px] mt-0.5">
                                    الحد الأقصى المسموح: {invoiceRemainingAmount.toLocaleString('ar-YE')} ر.ي. لا يمكن دفع أكثر من الرصيد المتبقي.
                                  </p>
                                </div>
                              </div>
                            )}

                            {selectedInvoice && (
                              <div className="bg-green-50 border border-green-200 p-3 rounded-xl flex items-center gap-2.5 text-green-800 text-xs">
                                <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0" />
                                <p>سيتم ربط الدفعة بالفاتورة رقم <strong className="font-extrabold">#{selectedInvoice.invoiceNumber}</strong> وتحديث حالتها تلقائياً.</p>
                              </div>
                            )}

                            {selectedContract && (
                              <div className="bg-green-50 border border-green-200 p-3 rounded-xl flex items-center gap-2.5 text-green-800 text-xs">
                                <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0" />
                                <p>سيتم سداد قسط بقيمة القسط المستحق من العقد النشط الحالي.</p>
                              </div>
                            )}

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div>
                                <label className="block text-[10px] font-bold text-gray-500 mb-1.5">مبلغ الدفعة المستلم (ر.ي) <span className="text-red-500">*</span></label>
                                <input
                                  type="number"
                                  required
                                  placeholder="أدخل مبلغ السداد بالريال اليمني..."
                                  value={paymentAmount}
                                  onChange={(e) => setPaymentAmount(e.target.value)}
                                  className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none font-mono"
                                />
                              </div>

                              <div>
                                <label className="block text-[10px] font-bold text-gray-500 mb-1.5">طريقة القبض / الدفع <span className="text-red-500">*</span></label>
                                <select
                                  value={paymentMethod}
                                  onChange={(e) => setPaymentMethod(e.target.value)}
                                  className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                                >
                                  <option value="cash">نقداً (كاش)</option>
                                  <option value="card">بطاقة ائتمان / شبكة</option>
                                  <option value="transfer">حوالة بنكية / مصرفية</option>
                                </select>
                              </div>
                            </div>

                            {/* Currency Planning Blueprint Section (Sprint 2 proposal preview) */}
                            <div className="bg-gray-50 p-4 rounded-xl border border-gray-200/60 space-y-3">
                              <span className="bg-gray-200 text-gray-600 text-[8px] font-bold px-2 py-0.5 rounded-full">تأسيس العملات - مقترح المرحلة 2</span>
                              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                                <div>
                                  <label className="block text-[9px] text-gray-400 mb-1">العملة المستلمة</label>
                                  <select
                                    disabled
                                    className="w-full text-xs px-2.5 py-1.5 border border-gray-200 bg-gray-100 text-gray-400 rounded-lg cursor-not-allowed"
                                  >
                                    <option>YER (الريال اليمني)</option>
                                    <option>SAR (الريال السعودي)</option>
                                    <option>USD (الدولار الأمريكي)</option>
                                  </select>
                                </div>
                                <div>
                                  <label className="block text-[9px] text-gray-400 mb-1">سعر الصرف المرجعي</label>
                                  <input
                                    type="text"
                                    disabled
                                    placeholder="1.00"
                                    className="w-full text-xs px-2.5 py-1.5 border border-gray-200 bg-gray-100 text-gray-400 rounded-lg cursor-not-allowed font-mono"
                                  />
                                </div>
                                <div>
                                  <label className="block text-[9px] text-gray-400 mb-1">المبلغ بالعملة الأجنبية</label>
                                  <input
                                    type="text"
                                    disabled
                                    placeholder="مغلق في Sprint 1"
                                    className="w-full text-xs px-2.5 py-1.5 border border-gray-200 bg-gray-100 text-gray-400 rounded-lg cursor-not-allowed font-mono"
                                  />
                                </div>
                              </div>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                              <div>
                                <label className="block text-[10px] font-bold text-gray-500 mb-1.5">البيان / الخدمة المقابلة</label>
                                <input
                                  type="text"
                                  placeholder="مثال: قسط تقويم شهري، علاج عصب..."
                                  value={serviceDescription}
                                  onChange={(e) => setServiceDescription(e.target.value)}
                                  className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                                />
                              </div>

                              <div>
                                <label className="block text-[10px] font-bold text-gray-500 mb-1.5">الطبيب المسؤول عن الحالة</label>
                                <select
                                  value={selectedDoctorId}
                                  onChange={(e) => setSelectedDoctorId(e.target.value)}
                                  className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                                >
                                  <option value="">اختار الطبيب...</option>
                                  {doctors?.map((doc) => (
                                    <option key={doc.id} value={doc.id}>{doc.name}</option>
                                  ))}
                                </select>
                              </div>
                            </div>

                            <div>
                              <label className="block text-[10px] font-bold text-gray-500 mb-1.5">ملاحظات مقبض الصندوق</label>
                              <textarea
                                rows={2}
                                placeholder="اكتب أي تفاصيل إضافية متعلقة بالدفعة النقدية هنا..."
                                value={notes}
                                onChange={(e) => setNotes(e.target.value)}
                                className="w-full text-xs px-3 py-2 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                              />
                            </div>

                            <button
                              type="submit"
                              disabled={
                                isSubmittingPayment ||
                                // Issue 2: block ALL roles when no document linked
                                (!selectedInvoice && !selectedContract) ||
                                // Issue 3: block when overpaying the invoice remaining
                                !!(selectedInvoice && invoiceRemainingAmount !== null && parseFloat(paymentAmount) > invoiceRemainingAmount)
                              }
                              className="w-full py-3 bg-[#1a3a5c] hover:bg-[#244b73] text-white text-xs font-bold rounded-xl transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                              {isSubmittingPayment ? "جاري المعالجة وإصدار السند..." : "تأكيد الدفع وطباعة سند القبض"}
                            </button>
                          </form>
                        </div>
                      </>
                    )}
                  </div>

                  {/* Right Workspace Column: Balance Details Card */}
                  <div className="space-y-6">
                    <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                      <h4 className="font-extrabold text-xs text-gray-900 border-b pb-2">تفاصيل الحساب المالي للمريض</h4>
                      
                      {loadingPatientFinance ? (
                        <div className="space-y-2 animate-pulse">
                          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-50 rounded-xl" />)}
                        </div>
                      ) : patientFinance ? (
                        <div className="space-y-3.5">
                          <div className="flex justify-between items-center text-xs">
                            <span className="text-gray-400">إجمالي تكلفة العلاج:</span>
                            <span className="font-extrabold text-gray-900 font-mono">{formatYemeniRiyal(patientFinance.totalTreatmentCost)}</span>
                          </div>
                          
                          <div className="flex justify-between items-center text-xs">
                            <span className="text-gray-400">المبلغ المدفوع (مسبقاً):</span>
                            <span className="font-extrabold text-green-600 font-mono">{formatYemeniRiyal(patientFinance.totalPaid)}</span>
                          </div>

                          <div className="flex justify-between items-center text-xs border-t pt-3">
                            <span className="text-gray-900 font-bold">الرصيد المتبقي المستحق:</span>
                            <span className="font-black text-red-600 font-mono">{formatYemeniRiyal(patientFinance.outstandingBalance)}</span>
                          </div>

                          <div className="flex justify-between items-center text-xs">
                            <span className="text-gray-400">إجمالي المتأخرات الفورية:</span>
                            <span className="font-extrabold text-amber-600 font-mono">{formatYemeniRiyal(patientFinance.overdueAmount)}</span>
                          </div>

                          <div className="flex justify-between items-center text-xs border-t pt-3">
                            <span className="text-gray-400">الحالة المالية للمريض:</span>
                            <span className={`px-2 py-0.5 rounded-full text-[9px] font-bold ${
                              patientFinance.financialStatus === "paid" ? "bg-green-50 text-green-600" :
                              patientFinance.financialStatus === "overdue" ? "bg-red-50 text-red-600 font-bold animate-pulse" :
                              "bg-clinic-blue/10 text-clinic-blue"
                            }`}>
                              {patientFinance.financialStatus === "paid" ? "مسدد بالكامل" :
                               patientFinance.financialStatus === "overdue" ? "متأخر عن السداد" :
                               "منتظم على الخطة"}
                            </span>
                          </div>
                          
                          <Link
                            href={`/patients/${selectedPatient.id}`}
                            className="flex items-center justify-center gap-1.5 w-full py-2 bg-gray-50 hover:bg-gray-100 text-gray-700 text-[10px] font-bold rounded-lg border border-gray-200 mt-2 transition"
                          >
                            عرض ملف المريض بالكامل
                            <ChevronRight className="w-3.5 h-3.5" />
                          </Link>
                        </div>
                      ) : (
                        <p className="text-xs text-gray-400">اختار مريضاً لاستعراض كشف الحساب المالي الموحد.</p>
                      )}
                    </div>
                  </div>

                </div>
              )}
                </>
              )}
            </div>
          )}

          {/* TAB 3: INVOICES LIST */}
          {activeTab === "invoices" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-gray-900">سجل الفواتير الصادرة والمسودات</h3>
              
              {loadingListings ? (
                <div className="space-y-3 animate-pulse">
                  {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-50 rounded-xl" />)}
                </div>
              ) : !invoices.length ? (
                <p className="text-xs text-gray-400 text-center py-12">لا توجد فواتير مسجلة في هذا الفرع.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs text-right">
                    <thead className="bg-gray-50 text-gray-500 font-bold border-b border-gray-100">
                      <tr>
                        <th className="px-4 py-3">رقم الفاتورة</th>
                        <th className="px-4 py-3">المريض</th>
                        <th className="px-4 py-3">الإجمالي (ر.ي)</th>
                        <th className="px-4 py-3">تاريخ الإنشاء</th>
                        <th className="px-4 py-3">الحالة</th>
                        <th className="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 font-medium text-gray-700">
                      {invoices.map((inv) => (
                        <tr key={inv.id} className="hover:bg-gray-50/50 transition">
                          <td className="px-4 py-3 font-mono font-bold text-gray-900">{inv.invoiceNumber}</td>
                          <td className="px-4 py-3">{inv.patientName}</td>
                          <td className="px-4 py-3 font-mono font-bold">{formatYemeniRiyal(inv.totalAmount)}</td>
                          <td className="px-4 py-3">{formatArabicDate(inv.createdAt)}</td>
                          <td className="px-4 py-3">
                            <span className={`px-2 py-0.5 rounded-full text-[9px] font-bold ${
                              inv.status === 'Paid' ? 'bg-green-50 text-green-600' :
                              inv.status === 'Issued' ? 'bg-amber-50 text-amber-600' : 'bg-gray-50 text-gray-500'
                            }`}>{inv.statusArabic}</span>
                          </td>
                          <td className="px-4 py-3 text-left">
                            <Link href={`/finance/invoices/${inv.id}`} className="text-clinic-blue font-bold hover:underline">تفاصيل</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 4: PAYMENTS LIST */}
          {activeTab === "payments" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-gray-900">دفتر سندات المقبوضات المالية</h3>
              
              {loadingListings ? (
                <div className="space-y-3 animate-pulse">
                  {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-50 rounded-xl" />)}
                </div>
              ) : !payments.length ? (
                <p className="text-xs text-gray-400 text-center py-12">لا توجد دفعات مالية مسجلة.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs text-right">
                    <thead className="bg-gray-50 text-gray-500 font-bold border-b border-gray-100">
                      <tr>
                        <th className="px-4 py-3">رقم السند</th>
                        <th className="px-4 py-3">اسم المريض</th>
                        <th className="px-4 py-3">المبلغ المقبوض</th>
                        <th className="px-4 py-3">التاريخ</th>
                        <th className="px-4 py-3">طريقة القبض</th>
                        <th className="px-4 py-3">البيان</th>
                        <th className="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 font-medium text-gray-700">
                      {payments.map((p) => (
                        <tr key={p.id} className="hover:bg-gray-50/50 transition">
                          <td className="px-4 py-3 font-mono font-bold text-gray-900">{p.receiptNumber ?? "—"}</td>
                          <td className="px-4 py-3">{p.patientName}</td>
                          <td className="px-4 py-3 font-mono font-bold text-green-600">{formatYemeniRiyal(p.amount)}</td>
                          <td className="px-4 py-3">{formatArabicDate(p.paymentDate)}</td>
                          <td className="px-4 py-3">{p.paymentMethod === 'cash' ? 'نقدي' : p.paymentMethod === 'card' ? 'شبكة' : 'تحويل'}</td>
                          <td className="px-4 py-3 truncate max-w-[150px]">{p.serviceDescription ?? "دفعة حساب"}</td>
                          <td className="px-4 py-3 text-left">
                            <button
                              onClick={() => handlePrintReceipt(p.id, p.receiptNumber)}
                              className="text-clinic-blue font-bold hover:underline flex items-center gap-1 inline-flex"
                            >
                              <Printer className="w-3.5 h-3.5" />
                              طباعة
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 5: CONTRACTS LIST */}
          {activeTab === "contracts" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-gray-900">عقود المرضى والأقساط المستمرة</h3>
              
              {loadingListings ? (
                <div className="space-y-3 animate-pulse">
                  {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-50 rounded-xl" />)}
                </div>
              ) : !contracts.length ? (
                <p className="text-xs text-gray-400 text-center py-12">لا توجد عقود تقويم علاجية نشطة.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs text-right">
                    <thead className="bg-gray-50 text-gray-500 font-bold border-b border-gray-100">
                      <tr>
                        <th className="px-4 py-3">المريض</th>
                        <th className="px-4 py-3">التخصص</th>
                        <th className="px-4 py-3">إجمالي العقد</th>
                        <th className="px-4 py-3">المدفوع</th>
                        <th className="px-4 py-3">المتبقي</th>
                        <th className="px-4 py-3">الحالة</th>
                        <th className="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 font-medium text-gray-700">
                      {contracts.map((c) => (
                        <tr key={c.id} className="hover:bg-gray-50/50 transition">
                          <td className="px-4 py-3">
                            <span className="font-bold block">{c.patientName}</span>
                            <span className="text-[10px] text-gray-400">#{c.patientNumber}</span>
                          </td>
                          <td className="px-4 py-3">{c.specialty === 'ortho' ? 'تقويم أسنان' : 'علاج أسنان'}</td>
                          <td className="px-4 py-3 font-mono font-bold">{formatYemeniRiyal(c.totalAmount)}</td>
                          <td className="px-4 py-3 font-mono text-green-600">{formatYemeniRiyal(c.paidAmount)}</td>
                          <td className="px-4 py-3 font-mono text-red-500 font-bold">{formatYemeniRiyal(c.remainingAmount)}</td>
                          <td className="px-4 py-3">
                            <span className={`px-2 py-0.5 rounded-full text-[9px] font-bold ${
                              c.status === 'Active' ? 'bg-green-50 text-green-600' : 'bg-gray-50 text-gray-500'
                            }`}>{c.status === 'Active' ? 'نشط' : 'مكتمل'}</span>
                          </td>
                          <td className="px-4 py-3 text-left">
                            <Link href={`/finance/contracts/${c.id}`} className="text-clinic-blue font-bold hover:underline">تفاصيل</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 6: OVERDUE CONTRACTS */}
          {activeTab === "overdue" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-red-600 flex items-center gap-1.5">
                <AlertCircle className="w-4 h-4" />
                متأخرات أقساط المرضى النشطة
              </h3>
              
              {loadingListings ? (
                <div className="space-y-3 animate-pulse">
                  {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-50 rounded-xl" />)}
                </div>
              ) : !overdueContracts.length ? (
                <div className="text-center py-12">
                  <CheckCircle2 className="w-12 h-12 text-green-500 mx-auto mb-3" />
                  <p className="text-xs text-gray-500">لا توجد أقساط متأخرة الدفع للمرضى حالياً. رائع!</p>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs text-right">
                    <thead className="bg-red-50 text-red-600 font-bold border-b border-red-100">
                      <tr>
                        <th className="px-4 py-3">المريض</th>
                        <th className="px-4 py-3">قيمة العقد</th>
                        <th className="px-4 py-3">المدفوع الفعلي</th>
                        <th className="px-4 py-3">المبلغ المتأخر</th>
                        <th className="px-4 py-3">المتبقي الإجمالي</th>
                        <th className="px-4 py-3">تاريخ البدء</th>
                        <th className="px-4 py-3"></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 font-medium text-gray-700">
                      {overdueContracts.map((c) => (
                        <tr key={c.contractId} className="hover:bg-red-50/10 transition">
                          <td className="px-4 py-3">
                            <span className="font-bold block text-gray-900">{c.patientName}</span>
                            <span className="text-[10px] text-gray-400">#{c.patientNumber}</span>
                          </td>
                          <td className="px-4 py-3 font-mono">{formatYemeniRiyal(c.totalAmount)}</td>
                          <td className="px-4 py-3 font-mono text-green-600">{formatYemeniRiyal(c.paidAmount)}</td>
                          <td className="px-4 py-3 font-mono text-red-600 font-extrabold">{formatYemeniRiyal(c.overdueAmount)}</td>
                          <td className="px-4 py-3 font-mono text-gray-500">{formatYemeniRiyal(c.remainingAmount)}</td>
                          <td className="px-4 py-3">{c.startDate ? formatArabicDate(c.startDate) : '—'}</td>
                          <td className="px-4 py-3 text-left">
                            <Link href={`/finance/contracts/${c.contractId}`} className="text-clinic-blue font-bold hover:underline">استعراض العقد</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 7: DOCTOR COMMISSIONS */}
          {activeTab === "commissions" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-gray-900">عمولات الأطباء المعلقة</h3>
              <p className="text-xs text-gray-500">
                يتم احتساب عمولات الأطباء تلقائياً بناءً على سياسة التعرف المقررة لكل خدمة علاجية عند استلام الدفعات.
              </p>
              
              <div className="bg-green-50/50 p-6 rounded-2xl border border-green-100 flex items-center justify-between gap-4">
                <div>
                  <span className="text-[10px] text-green-700 font-bold block">إجمالي العمولات المستحقة للأطباء (غير المدفوعة)</span>
                  <h4 className="text-2xl font-black font-mono text-green-700 mt-2">
                    {summary ? formatYemeniRiyal(summary.pendingCommissionsAmount) : "YER 0.00"}
                  </h4>
                </div>
                <Link
                  href="/commissions"
                  className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white text-xs font-bold rounded-xl transition"
                >
                  إدارة العمولات والصرف
                </Link>
              </div>
            </div>
          )}

          {/* TAB 7.5: OPERATIONAL EXPENSES */}
          {activeTab === "expenses" && (
            <div className="space-y-6">
              {/* Add Expense Form */}
              <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                <h3 className="font-extrabold text-sm text-gray-900">تسجيل مصروف تشغيلي جديد</h3>
                
                <form onSubmit={handleRegisterExpense} className="grid grid-cols-1 md:grid-cols-3 gap-4 text-right">
                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">بيان المصروف (العنوان):</label>
                    <input
                      type="text"
                      placeholder="مثال: سداد كهرباء المركز، إيجار العيادة..."
                      value={expenseTitle}
                      onChange={(e) => setExpenseTitle(e.target.value)}
                      className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none font-semibold text-gray-800"
                      required
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">صنف المصروف:</label>
                    <select
                      value={expenseCategory}
                      onChange={(e) => setExpenseCategory(e.target.value)}
                      className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none font-semibold text-gray-700 bg-white"
                    >
                      <option value="Rent">إيجارات وفروع</option>
                      <option value="Utilities">خدمات ومنافع (كهرباء/مياه/إنترنت)</option>
                      <option value="LabFees">تكاليف مختبرات الأسنان</option>
                      <option value="Marketing">إعلانات وتسويق</option>
                      <option value="ClinicSupplies">مواد ومستلزمات عيادات</option>
                      <option value="Maintenance">صيانة أدوات ومعدات</option>
                      <option value="Salaries">رواتب موظفين</option>
                      <option value="Commissions">عمولات أطباء</option>
                      <option value="Taxes">ضرائب ورسوم حكومية</option>
                      <option value="Miscellaneous">نثريات ومصاريف متنوعة</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">مبلغ المصروف (ريال يمني):</label>
                    <input
                      type="number"
                      placeholder="المبلغ بالريال..."
                      value={expenseAmount}
                      onChange={(e) => setExpenseAmount(e.target.value)}
                      className="w-full text-xs font-mono font-bold px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                      required
                      min="1"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">طريقة الدفع:</label>
                    <select
                      value={expensePaymentMethod}
                      onChange={(e) => setExpensePaymentMethod(e.target.value)}
                      className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none font-semibold text-gray-700 bg-white"
                    >
                      <option value="cash">نقداً (من الخزنة اليومية)</option>
                      <option value="card">بطاقة / POS</option>
                      <option value="bank_transfer">تحويل بنكي / حساب المركز</option>
                    </select>
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">تاريخ الصرف (اختياري - افتراضياً اليوم):</label>
                    <input
                      type="date"
                      value={expenseDate}
                      onChange={(e) => setExpenseDate(e.target.value)}
                      className="w-full text-xs px-3 py-2 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none font-semibold text-gray-700"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-400 mb-1.5">ملاحظات إضافية:</label>
                    <input
                      type="text"
                      placeholder="أي تفاصيل أو مرجع سداد..."
                      value={expenseNotes}
                      onChange={(e) => setExpenseNotes(e.target.value)}
                      className="w-full text-xs px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none text-gray-700"
                    />
                  </div>

                  <div className="md:col-span-3 flex justify-end pt-2">
                    <button
                      type="submit"
                      disabled={isSubmittingExpense}
                      className="px-6 py-2.5 text-xs font-bold text-white bg-clinic-blue rounded-xl hover:opacity-90 transition disabled:opacity-60"
                    >
                      {isSubmittingExpense ? "جاري تقييد المصروف..." : "حفظ وتقييد المصروف فوراً"}
                    </button>
                  </div>
                </form>
              </div>

              {/* Expenses List Grid */}
              <div className="bg-white border border-gray-100 p-5 rounded-2xl shadow-sm space-y-4">
                <h3 className="font-extrabold text-sm text-gray-900">سجل المصروفات المقيّدة</h3>
                
                {loadingExpenses ? (
                  <div className="space-y-3 animate-pulse">
                    {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-50 rounded-xl" />)}
                  </div>
                ) : !expensesList.length ? (
                  <p className="text-xs text-gray-400 text-center py-8">لا توجد مصروفات مقيّدة مؤخراً في هذا الفرع.</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-xs text-right">
                      <thead className="bg-gray-50 text-gray-500 font-bold border-b border-gray-100">
                        <tr>
                          <th className="px-4 py-3">رقم السند</th>
                          <th className="px-4 py-3">البيان</th>
                          <th className="px-4 py-3">الصنف</th>
                          <th className="px-4 py-3">طريقة السداد</th>
                          <th className="px-4 py-3">التاريخ</th>
                          <th className="px-4 py-3">المبلغ الصافي</th>
                          <th className="px-4 py-3"></th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50 font-medium text-gray-700">
                        {expensesList.map((e) => (
                          <tr key={e.id} className="hover:bg-gray-50/50 transition">
                            <td className="px-4 py-3 font-mono font-bold text-[#1a3a5c]">{e.expenseNumber}</td>
                            <td className="px-4 py-3">
                              <span className="font-bold block text-gray-900">{e.title}</span>
                              {e.notes && <span className="text-[10px] text-gray-400 block">{e.notes}</span>}
                            </td>
                            <td className="px-4 py-3 font-bold text-clinic-blue">{e.categoryArabic}</td>
                            <td className="px-4 py-3">
                              {e.paymentMethod === 'cash' ? 'نقدي (الدرج)' : e.paymentMethod === 'card' ? 'بطاقة POS' : 'تحويل بنكي'}
                            </td>
                            <td className="px-4 py-3">{formatArabicDate(e.expenseDate)}</td>
                            <td className="px-4 py-3 font-mono font-bold text-red-600">{formatYemeniRiyal(e.amount)}</td>
                            <td className="px-4 py-3 text-left">
                              <button
                                onClick={() => handleDeleteExpense(e.id)}
                                className="text-red-500 hover:text-red-700 font-bold hover:underline"
                              >
                                حذف القيد
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB 8: REPORTS & CONSOLIDATED P&L SHEET */}
          {activeTab === "reports" && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-6">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-4">
                <div>
                  <h3 className="font-extrabold text-sm text-gray-900">قائمة الأرباح والخسائر الموحدة (Consolidated P&L)</h3>
                  <p className="text-[10px] text-gray-500 mt-1">كشف تدفقات حقيقي يربط الإيرادات والمصروفات والرواتب وعمولات الفروع بالتاريخ</p>
                </div>
                
                {/* Date range filter */}
                <div className="flex items-center gap-2">
                  <input
                    type="date"
                    value={plFromDate}
                    onChange={(e) => setPlFromDate(e.target.value)}
                    className="text-xs px-2 py-1.5 border border-gray-200 rounded-lg text-gray-700 font-semibold"
                  />
                  <span className="text-xs text-gray-400">إلى</span>
                  <input
                    type="date"
                    value={plToDate}
                    onChange={(e) => setPlToDate(e.target.value)}
                    className="text-xs px-2 py-1.5 border border-gray-200 rounded-lg text-gray-700 font-semibold"
                  />
                  <button
                    onClick={fetchPLReport}
                    className="px-3 py-1.5 bg-[#1a3a5c] text-white text-xs font-bold rounded-lg hover:opacity-90 transition shrink-0"
                  >
                    تطبيق الفلترة
                  </button>
                </div>
              </div>
              
              {loadingPLReport ? (
                <div className="space-y-4 animate-pulse">
                  <div className="h-24 bg-gray-50 rounded-2xl" />
                  <div className="h-48 bg-gray-50 rounded-2xl" />
                </div>
              ) : plReport ? (
                <div className="space-y-6">
                  {/* Top Margins Glass Card */}
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="bg-gradient-to-br from-green-50 to-green-100/50 p-5 rounded-2xl border border-green-200/50 text-right space-y-2">
                      <span className="text-[10px] font-bold text-green-700 block">صافي الإيرادات التشغيلية (Revenue)</span>
                      <h4 className="text-2xl font-black font-mono text-green-700">
                        {formatYemeniRiyal(plReport.netRevenue)}
                      </h4>
                      <p className="text-[9px] text-green-600">إجمالي المقبوضات مخصوم منها المرتجعات</p>
                    </div>

                    <div className="bg-gradient-to-br from-red-50 to-red-100/50 p-5 rounded-2xl border border-red-200/50 text-right space-y-2">
                      <span className="text-[10px] font-bold text-red-700 block">إجمالي تكاليف التشغيل المباشرة (COGS)</span>
                      <h4 className="text-2xl font-black font-mono text-red-700">
                        {formatYemeniRiyal(plReport.totalDirectCosts)}
                      </h4>
                      <p className="text-[9px] text-red-600">تسويات المعامل ومواد العيادات الطبية</p>
                    </div>

                    <div className="bg-gradient-to-br from-clinic-blue/5 to-clinic-blue/10 p-5 rounded-2xl border border-clinic-blue/20 text-right space-y-2">
                      <span className="text-[10px] font-bold text-clinic-blue block">مجمل أرباح الفترة (Gross Profit)</span>
                      <h4 className="text-2xl font-black font-mono text-[#1a3a5c]">
                        {formatYemeniRiyal(plReport.grossProfit)}
                      </h4>
                      <p className="text-[9px] text-clinic-blue/80">هامش الربح التشغيلي الأولي للمركز</p>
                    </div>
                  </div>

                  {/* P&L Detailed Ledger Breakdown */}
                  <div className="border border-gray-100 rounded-2xl overflow-hidden shadow-sm">
                    <div className="bg-gray-50 px-4 py-3 border-b border-gray-100 font-bold text-xs text-gray-900">
                      تفاصيل قائمة الإيرادات والرواتب والمصروفات
                    </div>
                    
                    <div className="divide-y divide-gray-50 font-semibold text-xs text-gray-700 bg-white">
                      {/* Row 1 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-950 font-bold">إجمالي مقبوضات علاج المرضى (+)</span>
                        <span className="font-mono text-green-600 font-bold">{formatYemeniRiyal(plReport.grossRevenue)}</span>
                      </div>
                      
                      {/* Row 2 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">مبالغ مستردة للجمهور (مرتجعات) (-)</span>
                        <span className="font-mono text-red-600 font-bold">({formatYemeniRiyal(plReport.totalRefunds)})</span>
                      </div>

                      {/* Row 3 */}
                      <div className="px-5 py-3 flex justify-between items-center bg-gray-50/50">
                        <span className="text-gray-950 font-bold">تكاليف المعامل الخارجية والمختبرات (-)</span>
                        <span className="font-mono text-gray-800">{formatYemeniRiyal(plReport.labFees)}</span>
                      </div>

                      {/* Row 4 */}
                      <div className="px-5 py-3 flex justify-between items-center bg-gray-50/50">
                        <span className="text-gray-950 font-bold">مستلزمات ومواد طبية وأدوات العيادات (-)</span>
                        <span className="font-mono text-gray-800">{formatYemeniRiyal(plReport.clinicSupplies)}</span>
                      </div>

                      {/* Row 5 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-950 font-bold">رواتب وأجور الموظفين المنصرفة فعلياً (-)</span>
                        <span className="font-mono text-gray-800">{formatYemeniRiyal(plReport.salariesPaid)}</span>
                      </div>

                      {/* Row 6 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-950 font-bold">عمولات الأطباء المنصرفة فعلياً (-)</span>
                        <span className="font-mono text-gray-800">{formatYemeniRiyal(plReport.commissionsPaid)}</span>
                      </div>

                      {/* Row 7 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">إيجار الفروع والمباني (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.rent)}</span>
                      </div>

                      {/* Row 8 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">منافع وخدمات عادية (كهرباء/مياه/إنترنت) (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.utilities)}</span>
                      </div>

                      {/* Row 9 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">إعلانات وتسويق المركز (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.marketing)}</span>
                      </div>

                      {/* Row 10 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">صيانة المعدات والأجهزة الطبية (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.maintenance)}</span>
                      </div>

                      {/* Row 11 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">رسوم ومصاريف ضرائب حكومية (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.taxes)}</span>
                      </div>

                      {/* Row 12 */}
                      <div className="px-5 py-3 flex justify-between items-center">
                        <span className="text-gray-500">نثريات ومصاريف أخرى متنوعة (-)</span>
                        <span className="font-mono text-gray-600">{formatYemeniRiyal(plReport.opexMiscellaneous)}</span>
                      </div>

                      {/* Bottom Line Row */}
                      <div className="px-5 py-4 flex justify-between items-center bg-gradient-to-l from-[#1a3a5c]/5 to-[#1a3a5c]/10 text-[#1a3a5c]">
                        <div className="space-y-1">
                          <span className="text-sm font-black block">صافي أرباح الفترة النهائية (Net Profit)</span>
                          <span className="text-[9px] text-gray-500">بعد خصم التكاليف المباشرة والرواتب وعمولات الأطباء وكافة المصروفات العمومية</span>
                        </div>
                        <div className="text-left space-y-1">
                          <span className={`text-xl font-extrabold font-mono block ${plReport.netProfit >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                            {formatYemeniRiyal(plReport.netProfit)}
                          </span>
                          <span className={`text-[8px] px-2 py-0.5 rounded-full inline-block font-bold text-white ${plReport.netProfit >= 0 ? 'bg-green-600' : 'bg-red-600'}`}>
                            {plReport.netProfit >= 0 ? 'أداء إيرادي إيجابي' : 'تجاوز المصروفات'}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ) : null}

              {/* Print patient statement reference links */}
              <div className="border-t pt-4 grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="p-4 border border-gray-100 rounded-xl space-y-2 text-right">
                  <h4 className="text-xs font-bold text-gray-900">طباعة كشوفات حساب المرضى الموحدة</h4>
                  <p className="text-[10px] text-gray-500">
                    لطباعة كشف مالي موحد (RTL PDF) شامل تفاصيل الأقساط والمدفوعات لأي مريض، يرجى البحث عن المريض من تبويب &quot;صندوق اليوم&quot; والضغط على زر الطباعة.
                  </p>
                </div>
                
                <div className="p-4 border border-gray-100 rounded-xl space-y-2 text-right">
                  <h4 className="text-xs font-bold text-gray-900">الإيرادات الحركية وتوزيع الفروع</h4>
                  <p className="text-[10px] text-gray-500">
                    لمراجعة تقارير التحليلات المتقدمة لتطور إيرادات الأطباء ونشاط حجز المواعيد، يرجى الانتقال إلى صفحة التقارير العامة.
                  </p>
                  <Link href="/reports" className="text-[10px] text-[#1a3a5c] font-black hover:underline block">الانتقال للتقارير الرئيسية ←</Link>
                </div>
              </div>
            </div>
          )}

          {/* TAB 9: SETTINGS */}
          {activeTab === "settings" && isAdmin && (
            <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-sm space-y-4">
              <h3 className="font-extrabold text-sm text-gray-900">إعدادات الإدارة المالية</h3>
              
              <div className="p-4 bg-gray-50 rounded-xl border border-gray-200/60 text-xs text-gray-700 space-y-2">
                <p className="font-bold">مواصفات العملة الأساسية للمركز:</p>
                <ul className="list-disc list-inside space-y-1 mt-2 text-[11px] text-gray-500">
                  <li>العملة المرجعية للنظام: <strong className="font-bold">YER (الريال اليمني)</strong>.</li>
                  <li>تقريب الفواتير: تقريب تلقائي لأقرب ريال يمني.</li>
                  <li>سقف المدفوعات الحرة: يتطلب موافقة محاسب.</li>
                </ul>
              </div>
            </div>
          )}

        </div>
      </div>

      {/* Close Day Box Dialog Preview (Active in Sprint 2) */}
      {showCloseBoxDialog && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white max-w-md w-full rounded-2xl p-6 shadow-xl space-y-4 text-right">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="font-extrabold text-sm text-gray-900">إغلاق الصندوق المالي (قفل الوردية اليومية)</h3>
              <button onClick={() => setShowCloseBoxDialog(false)} className="text-gray-400 hover:text-gray-600">
                <X className="w-4 h-4" />
              </button>
            </div>
            
            {!activeSession ? (
              <div className="space-y-4 py-4 text-center">
                <AlertCircle className="w-12 h-12 text-amber-500 mx-auto animate-pulse" />
                <h4 className="text-xs font-bold text-gray-800">لا توجد وردية صندوق مفتوحة حالياً</h4>
                <p className="text-[11px] text-gray-500 leading-relaxed">
                  يجب أن تكون هناك وردية نشطة للتمكن من إغلاقها. يرجى الذهاب إلى تبويب &quot;صندوق اليوم&quot; لفتح الصندوق أولاً.
                </p>
                <div className="flex justify-center pt-2">
                  <button
                    onClick={() => setShowCloseBoxDialog(false)}
                    className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-xs font-bold rounded-xl transition"
                  >
                    إغلاق النافذة
                  </button>
                </div>
              </div>
            ) : (
              <form onSubmit={handleCloseSession} className="space-y-4">
                <p className="text-xs text-gray-600">
                  يرجى عد المبالغ الفعلية المتوفرة في درج الصندوق ونقاط البيع والتحويلات وإدخالها بدقة لتسوية الوردية رقم <span className="font-extrabold text-clinic-blue">#{activeSession.sessionNumber}</span>.
                </p>

                <div className="bg-clinic-blue/5 p-3.5 rounded-xl border border-clinic-blue/10 space-y-1">
                  <span className="text-[10px] text-gray-500 block">رصيد العهدة الافتتاحية للوردية:</span>
                  <span className="text-xs font-bold text-gray-900 font-mono">
                    {formatYemeniRiyal(activeSession.openingBalance)}
                  </span>
                </div>

                <div className="space-y-3">
                  <div>
                    <label className="block text-[10px] font-bold text-gray-500 mb-1">
                      النقد الفعلي بالدرج - كاش (YER): <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="number"
                      placeholder="أدخل إجمالي النقد الفعلي بالريال اليمني..."
                      value={actualClosingCash}
                      onChange={(e) => setActualClosingCash(e.target.value)}
                      className="w-full text-xs font-mono text-center font-bold px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                      required
                      min="0"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-500 mb-1">
                      مقبوضات البطاقة / نقاط البيع الفعلية (YER):
                    </label>
                    <input
                      type="number"
                      placeholder="أدخل إجمالي مقبوضات الشبكة..."
                      value={actualClosingCard}
                      onChange={(e) => setActualClosingCard(e.target.value)}
                      className="w-full text-xs font-mono text-center font-bold px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                      min="0"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-500 mb-1">
                      مقبوضات التحويلات البنكية الفعلية (YER):
                    </label>
                    <input
                      type="number"
                      placeholder="أدخل إجمالي الحوالات البنكية..."
                      value={actualClosingBank}
                      onChange={(e) => setActualClosingBank(e.target.value)}
                      className="w-full text-xs font-mono text-center font-bold px-3 py-2.5 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none"
                      min="0"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-gray-500 mb-1">
                      ملاحظات وتفاصيل التسوية (اختياري):
                    </label>
                    <textarea
                      placeholder="اكتب أي ملاحظات أو أسباب وجود فوارق بالصندوق إن وجدت..."
                      value={closeSessionNotes}
                      onChange={(e) => setCloseSessionNotes(e.target.value)}
                      rows={2}
                      className="w-full text-xs px-3 py-2 border border-gray-200 rounded-xl focus:border-clinic-blue focus:outline-none resize-none"
                    />
                  </div>
                </div>

                <div className="bg-amber-50 border border-amber-200 p-3 rounded-xl flex items-start gap-2 text-amber-800">
                  <AlertCircle className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
                  <p className="text-[10px] leading-relaxed">
                    <strong>تنبيه هام</strong>: بمجرد تأكيد الإقفال، سيتم قفل وتأمين جميع مدفوعات هذه الوردية تلقائياً لمنع التعديل أو الحذف العشوائي، وسيتم إخطار الإدارة المالية بأي عجز أو فائض فوراً.
                  </p>
                </div>

                <div className="flex items-center justify-end gap-2 pt-2 border-t">
                  <button
                    type="button"
                    onClick={() => setShowCloseBoxDialog(false)}
                    className="px-4 py-2 border border-gray-200 text-gray-700 text-xs font-bold rounded-xl hover:bg-gray-50 transition"
                  >
                    إلغاء
                  </button>
                  <button
                    type="submit"
                    disabled={isClosingSession}
                    className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-xs font-bold rounded-xl transition disabled:opacity-60"
                  >
                    {isClosingSession ? "جاري إقفال وترحيل الصندوق..." : "تأكيد الترحيل وإقفال الصندوق"}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}

    </div>
  );
}
