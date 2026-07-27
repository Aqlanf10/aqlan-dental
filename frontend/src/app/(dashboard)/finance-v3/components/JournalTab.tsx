"use client";

import { useState, useEffect, useCallback } from "react";
import {
  BookOpen,
  RefreshCw,
  Loader2,
  ChevronDown,
  ChevronUp,
  Filter,
  Download,
  Plus,
  Trash2,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { SectionHeader, LoadingSkeleton, EmptyState, Modal, StatusBadge, tokens, inputStyle, labelStyle, btnGhost } from "./FinanceSharedUI";
import { formatMoney, formatYER, safeFormatDate } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Types for Journal Entries
   ═══════════════════════════════════════════════════════════════════════════════ */

interface JournalLine {
  id: string;
  accountType: string;
  accountId: string;
  debit: number;
  credit: number;
  description: string;
}

interface JournalEntry {
  id: string;
  entryNumber: string;
  documentType: string;
  description: string;
  entryDate: string;
  currency?: string;
  exchangeRateToYer?: number;
  branchId: string;
  treasuryId: string | null;
  performedBy: string;
  isPosted: boolean;
  isReversal: boolean;
  reversalOfEntryId: string | null;
  reversedByEntryId: string | null;
  createdAt: string;
  totalDebit: number;
  totalCredit: number;
  lineCount: number;
  lines: JournalLine[];
}

interface JournalEntryDetail {
  id: string;
  entryNumber: string;
  documentType: string;
  financialDocumentId: string;
  description: string;
  entryDate: string;
  currency?: string;
  exchangeRateToYer?: number;
  branchName: string;
  treasuryName: string;
  performedByName: string;
  isPosted: boolean;
  isReversal: boolean;
  reversalOfEntryNumber: string | null;
  reversedByEntryNumber: string | null;
  cashierSessionId: string | null;
  createdAt: string;
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  lines: JournalLine[];
  branchId: string;
  performedBy: string;
}

type ManualJournalLine = {
  accountType: string;
  debit: string;
  credit: string;
  description: string;
};

/* ── Arabic labels for document types ── */
const DOCUMENT_TYPE_LABELS: Record<string, string> = {
  Payment: "دفعة مريض",
  Refund: "استرداد",
  Invoice: "فاتورة",
  Expense: "مصروف تشغيلي",
  SalaryPayment: "صرف راتب",
  AdvancePayment: "سلفة موظف",
  CommissionPayment: "صرف عمولة",
  SupplierBill: "استحقاق فاتورة مورد/معمل",
  SupplierPayment: "دفع مورد",
  CreditNoteRefund: "استرداد إشعار دائن",
  VaultTransfer: "ترحيل سيولة",
  ContractCancellation: "إلغاء عقد",
  PaymentDeletion: "حذف دفعة",
  PaymentAllocation: "تسوية دفعة مقدمة",
  YearEndClosing: "إقفال سنة مالية",
  OpeningBalance: "رصيد افتتاحي",
  Other: "أخرى",
};

/* ── Arabic labels for account types ── */
const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Treasury: "خزينة/صندوق",
  PatientReceivable: "ذمم مرضى مدينة",
  PatientAdvance: "دفعات مقدمة مرضى",
  Payable: "ذمم دائنة",
  Revenue: "إيرادات",
  Expense: "مصروفات",
  OwnerEquity: "حقوق الملكية",
  OtherReceivable: "ذمم مدينة أخرى",
  ContraRevenue: "إيرادات مقابلة",
  ContraExpense: "مصروفات مقابلة",
  AccountsPayable: "ذمم دائنة (موردين)",
  SalesReturns: "مرتجعات مبيعات",
};

/* ── Document type filter options ── */
const DOCUMENT_TYPE_OPTIONS = [
  { value: "", label: "الكل" },
  { value: "Payment", label: "دفعة مريض" },
  { value: "Refund", label: "استرداد" },
  { value: "Invoice", label: "فاتورة" },
  { value: "Expense", label: "مصروف تشغيلي" },
  { value: "SupplierBill", label: "استحقاق فاتورة مورد/معمل" },
  { value: "SupplierPayment", label: "دفع مورد" },
  { value: "CreditNoteRefund", label: "استرداد إشعار دائن" },
  { value: "VaultTransfer", label: "ترحيل سيولة" },
  { value: "ContractCancellation", label: "إلغاء عقد" },
  { value: "PaymentDeletion", label: "حذف دفعة" },
  { value: "OpeningBalance", label: "رصيد افتتاحي" },
  { value: "YearEndClosing", label: "إقفال سنة مالية" },
];


const DISBURSEMENT_DOCUMENT_TYPES = new Set([
  "Expense",
  "SalaryPayment",
  "AdvancePayment",
  "CommissionPayment",
  "SupplierPayment",
  "Refund",
  "CreditNoteRefund",
  "VaultTransfer",
]);

const canDownloadDisbursementVoucher = (entry: JournalEntry) =>
  entry.lines.some((line) => line.accountType === "Treasury" && line.credit > 0)
  || DISBURSEMENT_DOCUMENT_TYPES.has(entry.documentType);

const MANUAL_ACCOUNT_OPTIONS = ["Revenue", "Expense", "OwnerEquity", "OtherReceivable", "ContraRevenue", "ContraExpense", "SalesReturns"];
/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 10: Journal Entries — قيود اليومية
   ═══════════════════════════════════════════════════════════════════════════════ */
export function JournalTab() {
  const [entries, setEntries] = useState<JournalEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);

  // Filters
  const [filterDocType, setFilterDocType] = useState("");
  const [filterFromDate, setFilterFromDate] = useState("");
  const [filterToDate, setFilterToDate] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  // Detail modal
  const [detail, setDetail] = useState<JournalEntryDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // Expanded rows (for inline line display)
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());
  const [showManualEntry, setShowManualEntry] = useState(false);
  const [manualDescription, setManualDescription] = useState("");
  const [manualDate, setManualDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [manualCurrency, setManualCurrency] = useState("YER");
  const [manualRate, setManualRate] = useState("");
  const [manualLines, setManualLines] = useState<ManualJournalLine[]>([
    { accountType: "Expense", debit: "", credit: "", description: "" },
    { accountType: "OwnerEquity", debit: "", credit: "", description: "" },
  ]);
  const [manualSubmitting, setManualSubmitting] = useState(false);

  const fetchEntries = useCallback(async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", String(page));
      params.set("pageSize", String(pageSize));
      if (filterDocType) params.set("documentType", filterDocType);
      if (filterFromDate) params.set("fromDate", filterFromDate);
      if (filterToDate) params.set("toDate", filterToDate);

      const { data: responseData } = await api.get<{ data: JournalEntry[]; total: number; page: number }>(`/api/finance-v3/journal-entries?${params.toString()}`);
      setEntries(responseData?.data ?? []);
      setTotal(responseData?.total ?? 0);
    } catch {
      toast.error("فشل في تحميل قيود اليومية");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, filterDocType, filterFromDate, filterToDate]);

  useEffect(() => { fetchEntries(); }, [fetchEntries]);

  const fetchDetail = async (id: string) => {
    try {
      setDetailLoading(true);
      const { data } = await api.get<JournalEntryDetail>(`/api/finance-v3/journal-entries/${id}`);
      setDetail(data);
    } catch {
      toast.error("فشل في تحميل تفاصيل القيد");
    } finally {
      setDetailLoading(false);
    }
  };

  const downloadDisbursementVoucher = async (entry: JournalEntry) => {
    try {
      await downloadPdfFromApi(
        `/api/finance-v3/journal-entries/${entry.id}/disbursement-voucher/pdf`,
        `disbursement-voucher-${entry.entryNumber}.pdf`
      );
      toast.success("تم تحميل سند الصرف");
    } catch {
      toast.error("تعذر تحميل سند الصرف لهذا القيد");
    }
  };

  const manualDebit = manualLines.reduce((sum, line) => sum + (Number(line.debit) || 0), 0);
  const manualCredit = manualLines.reduce((sum, line) => sum + (Number(line.credit) || 0), 0);
  const updateManualLine = (index: number, patch: Partial<ManualJournalLine>) =>
    setManualLines((current) => current.map((line, i) => i === index ? { ...line, ...patch } : line));
  const resetManualEntry = () => {
    setManualDescription(""); setManualDate(new Date().toISOString().slice(0, 10)); setManualCurrency("YER"); setManualRate("");
    setManualLines([{ accountType: "Expense", debit: "", credit: "", description: "" }, { accountType: "OwnerEquity", debit: "", credit: "", description: "" }]);
  };
  const submitManualEntry = async () => {
    if (!manualDescription.trim() || !manualDate) return toast.error("أدخل بيان القيد وتاريخه");
    if (manualLines.length < 2 || manualDebit <= 0 || manualDebit !== manualCredit) return toast.error("يجب أن يكون القيد متوازناً ومدينه مساوياً لدائنه");
    if (manualCurrency !== "YER" && (!Number(manualRate) || Number(manualRate) <= 0)) return toast.error("أدخل سعر الصرف إلى الريال اليمني");
    try {
      setManualSubmitting(true);
      await api.post("/api/finance-v3/journal-entries/manual", { description: manualDescription.trim(), entryDate: manualDate, currency: manualCurrency, exchangeRateToYer: manualCurrency === "YER" ? 1 : Number(manualRate), lines: manualLines.map((line) => ({ accountType: line.accountType, debit: Number(line.debit) || 0, credit: Number(line.credit) || 0, description: line.description.trim() || null })) });
      toast.success("تم ترحيل القيد اليدوي المتوازن"); setShowManualEntry(false); resetManualEntry(); fetchEntries();
    } catch { toast.error("تعذر ترحيل القيد اليدوي"); }
    finally { setManualSubmitting(false); }
  };

  const toggleRow = (id: string) => {
    setExpandedRows((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const totalPages = Math.ceil(total / pageSize);
  const debitByCurrency = entries.reduce<Record<string, number>>((totals, entry) => {
    const currency = entry.currency ?? "YER";
    totals[currency] = (totals[currency] ?? 0) + entry.totalDebit;
    return totals;
  }, {});

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="قيود اليومية" action={
        <div className="flex items-center gap-2">
          <button onClick={() => setShowManualEntry(true)} className="flex items-center gap-1 px-3 py-1.5 rounded-md text-xs font-medium" style={{ color: tokens.textOnBrand, backgroundColor: tokens.brand }}>
            <Plus className="w-3.5 h-3.5" /> قيد يدوي
          </button>
          <button onClick={() => setShowFilters(!showFilters)} className="flex items-center gap-1 px-3 py-1.5 rounded-md text-xs font-medium" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }}>
            <Filter className="w-3.5 h-3.5" /> تصفية
          </button>
          <button onClick={fetchEntries} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* ── Filters ── */}
      {showFilters && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label style={labelStyle}>نوع المستند</label>
              <select value={filterDocType} onChange={(e) => { setFilterDocType(e.target.value); setPage(1); }} style={inputStyle}>
                {DOCUMENT_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label style={labelStyle}>من تاريخ</label>
              <input type="date" value={filterFromDate} onChange={(e) => { setFilterFromDate(e.target.value); setPage(1); }} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>إلى تاريخ</label>
              <input type="date" value={filterToDate} onChange={(e) => { setFilterToDate(e.target.value); setPage(1); }} style={inputStyle} />
            </div>
          </div>
          <div className="flex gap-2 mt-3">
            <button onClick={() => { setFilterDocType(""); setFilterFromDate(""); setFilterToDate(""); setPage(1); }} style={btnGhost}>مسح التصفية</button>
          </div>
        </div>
      )}

      {/* ── KPI summary ── */}
      <div className="grid grid-cols-4 gap-3">
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>إجمالي القيود</span>
          <p className="text-lg font-bold" style={{ color: tokens.textPrimary }}>{total.toLocaleString("ar-EG")}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>مُرحّلة</span>
          <p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{entries.filter((e) => e.isPosted).length}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>عكسية</span>
          <p className="text-lg font-bold" style={{ color: tokens.warningText }}>{entries.filter((e) => e.isReversal).length}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>إجمالي المدين</span>
          <p className="text-sm font-bold" style={{ color: tokens.brand }}>{Object.entries(debitByCurrency).map(([currency, amount]) => formatMoney(amount, currency)).join(" • ") || formatMoney(0, "YER")}</p>
        </div>
      </div>

      {loading ? <LoadingSkeleton /> : entries.length === 0 ? <EmptyState icon={BookOpen} message="لا توجد قيود يومية" /> : (
        <div className="space-y-2">
          {entries.map((entry) => {
            const isExpanded = expandedRows.has(entry.id);
            return (
              <div key={entry.id} className="rounded-lg border" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                {/* ── Entry header row ── */}
                <div
                  className="flex items-center gap-3 px-4 py-3 cursor-pointer"
                  onClick={() => toggleRow(entry.id)}
                  onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }}
                  onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                >
                  <div className="flex items-center gap-2 flex-1 min-w-0">
                    <span className="text-xs font-mono font-bold" style={{ color: tokens.brand }}>{entry.entryNumber}</span>
                    <span className="text-xs px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>
                      {DOCUMENT_TYPE_LABELS[entry.documentType] ?? entry.documentType}
                    </span>
                    {entry.isReversal && (
                      <span className="text-xs px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.warningBg, color: tokens.warningText }}>قيد عكسي</span>
                    )}
                    <span className="text-xs truncate" style={{ color: tokens.textSecondary }}>{entry.description}</span>
                  </div>
                  <div className="flex items-center gap-4 text-xs flex-shrink-0">
                    <span style={{ color: tokens.textTertiary }}>{safeFormatDate(entry.entryDate)}</span>
                    <span className="font-mono" style={{ color: tokens.successBorder }}>{formatMoney(entry.totalDebit, entry.currency)}</span>
                    <span className="font-mono" style={{ color: tokens.dangerBorder }}>{formatMoney(entry.totalCredit, entry.currency)}</span>
                    <span className="text-xs" style={{ color: tokens.textTertiary }}>{entry.lineCount} بنود</span>
                    <button onClick={(e) => { e.stopPropagation(); fetchDetail(entry.id); }} className="w-6 h-6 rounded flex items-center justify-center" style={{ color: tokens.brand }} title="تفاصيل">
                      <BookOpen className="w-3.5 h-3.5" />
                    </button>
                    {canDownloadDisbursementVoucher(entry) && (
                      <button onClick={(e) => { e.stopPropagation(); downloadDisbursementVoucher(entry); }} className="w-6 h-6 rounded flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="سند صرف">
                        <Download className="w-3.5 h-3.5" />
                      </button>
                    )}
                    {isExpanded ? <ChevronUp className="w-4 h-4" style={{ color: tokens.textTertiary }} /> : <ChevronDown className="w-4 h-4" style={{ color: tokens.textTertiary }} />}
                  </div>
                </div>

                {/* ── Expanded lines ── */}
                {isExpanded && entry.lines.length > 0 && (
                  <div className="border-t px-4 py-2" style={{ borderColor: tokens.border, backgroundColor: tokens.bg }}>
                    <table className="w-full text-xs">
                      <thead>
                        <tr style={{ color: tokens.textTertiary }}>
                          <th className="text-right py-1 px-2 font-medium">الحساب</th>
                          <th className="text-right py-1 px-2 font-medium">البيان</th>
                          <th className="text-right py-1 px-2 font-medium">مدين</th>
                          <th className="text-right py-1 px-2 font-medium">دائن</th>
                        </tr>
                      </thead>
                      <tbody>
                        {entry.lines.map((line) => (
                          <tr key={line.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                            <td className="py-1.5 px-2">
                              <span className="font-medium" style={{ color: tokens.textPrimary }}>{ACCOUNT_TYPE_LABELS[line.accountType] ?? line.accountType}</span>
                            </td>
                            <td className="py-1.5 px-2" style={{ color: tokens.textSecondary }}>{line.description || "—"}</td>
                            <td className="py-1.5 px-2 font-mono" style={{ color: line.debit > 0 ? tokens.successBorder : tokens.textTertiary }}>
                              {line.debit > 0 ? formatMoney(line.debit, entry.currency) : "—"}
                            </td>
                            <td className="py-1.5 px-2 font-mono" style={{ color: line.credit > 0 ? tokens.dangerBorder : tokens.textTertiary }}>
                              {line.credit > 0 ? formatMoney(line.credit, entry.currency) : "—"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr className="font-bold" style={{ borderTop: `2px solid ${tokens.border}` }}>
                          <td className="py-1.5 px-2" style={{ color: tokens.textPrimary }}>المجموع</td>
                          <td></td>
                          <td className="py-1.5 px-2 font-mono" style={{ color: tokens.successBorder }}>{formatMoney(entry.totalDebit, entry.currency)}</td>
                          <td className="py-1.5 px-2 font-mono" style={{ color: tokens.dangerBorder }}>{formatMoney(entry.totalCredit, entry.currency)}</td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                )}
              </div>
            );
          })}

          {/* ── Pagination ── */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-4">
              <button onClick={() => setPage(Math.max(1, page - 1))} disabled={page <= 1} style={{ ...btnGhost, opacity: page <= 1 ? 0.4 : 1 }}>السابق</button>
              <span className="text-xs" style={{ color: tokens.textSecondary }}>صفحة {page} من {totalPages} ({total} قيد)</span>
              <button onClick={() => setPage(Math.min(totalPages, page + 1))} disabled={page >= totalPages} style={{ ...btnGhost, opacity: page >= totalPages ? 0.4 : 1 }}>التالي</button>
            </div>
          )}
        </div>
      )}

      <Modal open={showManualEntry} onClose={() => { setShowManualEntry(false); resetManualEntry(); }} title="قيد يومية يدوي" wide>
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="sm:col-span-3"><label style={labelStyle}>بيان القيد</label><input value={manualDescription} onChange={(e) => setManualDescription(e.target.value)} style={inputStyle} /></div>
            <div><label style={labelStyle}>التاريخ</label><input type="date" value={manualDate} onChange={(e) => setManualDate(e.target.value)} style={inputStyle} /></div>
            <div><label style={labelStyle}>العملة</label><select value={manualCurrency} onChange={(e) => setManualCurrency(e.target.value)} style={inputStyle}><option value="YER">YER</option><option value="SAR">SAR</option><option value="USD">USD</option></select></div>
            {manualCurrency !== "YER" && <div><label style={labelStyle}>سعر الصرف إلى YER</label><input type="number" min="0" step="0.000001" value={manualRate} onChange={(e) => setManualRate(e.target.value)} style={inputStyle} /></div>}
          </div>
          <div className="rounded-lg border overflow-hidden" style={{ borderColor: tokens.border }}>
            <div className="grid grid-cols-[1.2fr_1fr_1fr_1.4fr_32px] gap-2 px-3 py-2 text-xs font-medium" style={{ backgroundColor: tokens.cardHover, color: tokens.textSecondary }}><span>الحساب</span><span>مدين</span><span>دائن</span><span>بيان البند</span><span /></div>
            {manualLines.map((line, index) => <div key={index} className="grid grid-cols-[1.2fr_1fr_1fr_1.4fr_32px] gap-2 px-3 py-2 items-center border-t" style={{ borderColor: tokens.border }}>
              <select value={line.accountType} onChange={(e) => updateManualLine(index, { accountType: e.target.value })} style={inputStyle}>{MANUAL_ACCOUNT_OPTIONS.map((type) => <option key={type} value={type}>{ACCOUNT_TYPE_LABELS[type]}</option>)}</select>
              <input type="number" min="0" step="0.01" value={line.debit} onChange={(e) => updateManualLine(index, { debit: e.target.value, credit: e.target.value ? "" : line.credit })} style={inputStyle} />
              <input type="number" min="0" step="0.01" value={line.credit} onChange={(e) => updateManualLine(index, { credit: e.target.value, debit: e.target.value ? "" : line.debit })} style={inputStyle} />
              <input value={line.description} onChange={(e) => updateManualLine(index, { description: e.target.value })} style={inputStyle} />
              <button disabled={manualLines.length <= 2} onClick={() => setManualLines((current) => current.filter((_, i) => i !== index))} className="w-7 h-7 flex items-center justify-center" style={{ color: tokens.dangerBorder, opacity: manualLines.length <= 2 ? .35 : 1 }} title="حذف البند"><Trash2 className="w-3.5 h-3.5" /></button>
            </div>)}
          </div>
          <div className="flex items-center justify-between text-xs"><button onClick={() => setManualLines((current) => [...current, { accountType: "Expense", debit: "", credit: "", description: "" }])} style={btnGhost}><Plus className="w-3.5 h-3.5 inline" /> إضافة بند</button><span style={{ color: manualDebit === manualCredit && manualDebit > 0 ? tokens.successBorder : tokens.dangerBorder }}>مدين {formatMoney(manualDebit, manualCurrency)} | دائن {formatMoney(manualCredit, manualCurrency)}</span></div>
          <div className="flex justify-end gap-2"><button onClick={() => { setShowManualEntry(false); resetManualEntry(); }} style={btnGhost}>إلغاء</button><button onClick={submitManualEntry} disabled={manualSubmitting} className="px-4 py-2 rounded-md text-sm font-semibold" style={{ backgroundColor: tokens.brand, color: tokens.textOnBrand, opacity: manualSubmitting ? .6 : 1 }}>{manualSubmitting ? "جارٍ الترحيل..." : "ترحيل القيد"}</button></div>
        </div>
      </Modal>

      {/* ═══ Detail Modal ═══ */}
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`قيد ${detail?.entryNumber ?? ""}`} wide>
        {detailLoading ? (
          <div className="flex items-center justify-center py-8"><Loader2 className="w-6 h-6 animate-spin" style={{ color: tokens.brand }} /></div>
        ) : detail ? (
          <div className="space-y-4">
            {/* Entry metadata */}
            <div className="grid grid-cols-3 gap-4 text-sm">
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>رقم القيد</span><p className="font-mono font-bold" style={{ color: tokens.brand }}>{detail.entryNumber}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>نوع المستند</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{DOCUMENT_TYPE_LABELS[detail.documentType] ?? detail.documentType}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>التاريخ</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{safeFormatDate(detail.entryDate)}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>الفرع</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.branchName || "—"}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>الخزينة</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.treasuryName || "—"}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>بواسطة</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.performedByName || "—"}</p></div>
            </div>

            {detail.description && (
              <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
                <span className="text-xs font-medium" style={{ color: tokens.infoText }}>البيان: </span>
                <span className="text-xs" style={{ color: tokens.infoText }}>{detail.description}</span>
              </div>
            )}

            {/* Status badges */}
            <div className="flex items-center gap-2">
              <StatusBadge status={detail.isPosted ? "Open" : "Draft"} />
              {detail.isReversal && <StatusBadge status="Cancelled" />}
              {detail.reversalOfEntryNumber && (
                <span className="text-xs" style={{ color: tokens.warningText }}>عكسي لقيد {detail.reversalOfEntryNumber}</span>
              )}
              {detail.reversedByEntryNumber && (
                <span className="text-xs" style={{ color: tokens.dangerText }}>عُكس بقيد {detail.reversedByEntryNumber}</span>
              )}
              {detail.isBalanced && (
                <span className="text-xs px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.successBg, color: tokens.successBorder }}>متوازن</span>
              )}
            </div>

            {/* Lines table */}
            <div className="overflow-x-auto rounded-lg border" style={{ borderColor: tokens.border }}>
              <table className="w-full text-sm">
                <thead>
                  <tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>الحساب</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>البيان</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>مدين</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>دائن</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.lines.map((line) => (
                    <tr key={line.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                      <td className="px-4 py-2.5">
                        <span className="font-medium" style={{ color: tokens.textPrimary }}>{ACCOUNT_TYPE_LABELS[line.accountType] ?? line.accountType}</span>
                      </td>
                      <td className="px-4 py-2.5" style={{ color: tokens.textSecondary }}>{line.description || "—"}</td>
                      <td className="px-4 py-2.5 font-mono" style={{ color: line.debit > 0 ? tokens.successBorder : tokens.textTertiary, fontWeight: line.debit > 0 ? 700 : 400 }}>
                        {line.debit > 0 ? formatYER(line.debit) : "—"}
                      </td>
                      <td className="px-4 py-2.5 font-mono" style={{ color: line.credit > 0 ? tokens.dangerBorder : tokens.textTertiary, fontWeight: line.credit > 0 ? 700 : 400 }}>
                        {line.credit > 0 ? formatYER(line.credit) : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr style={{ borderTop: `2px solid ${tokens.brand}`, backgroundColor: tokens.brandLight }}>
                    <td className="px-4 py-2.5 font-bold" style={{ color: tokens.brand }}>المجموع</td>
                    <td></td>
                    <td className="px-4 py-2.5 font-mono font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.totalDebit)}</td>
                    <td className="px-4 py-2.5 font-mono font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(detail.totalCredit)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setDetail(null)} style={btnGhost}>إغلاق</button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
