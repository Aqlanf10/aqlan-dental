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
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { SectionHeader, LoadingSkeleton, EmptyState, Modal, StatusBadge, tokens, inputStyle, labelStyle, btnGhost } from "./FinanceSharedUI";
import { formatYER, safeFormatDate } from "./FinanceHelpers";

/* â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
   Types for Journal Entries
   â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ */

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

/* â”€â”€ Arabic labels for document types â”€â”€ */
const DOCUMENT_TYPE_LABELS: Record<string, string> = {
  Payment: "ط¯ظپط¹ط© ظ…ط±ظٹط¶",
  Refund: "ط§ط³طھط±ط¯ط§ط¯",
  Invoice: "ظپط§طھظˆط±ط©",
  Expense: "ظ…طµط±ظˆظپ طھط´ط؛ظٹظ„ظٹ",
  SalaryPayment: "طµط±ظپ ط±ط§طھط¨",
  AdvancePayment: "ط³ظ„ظپط© ظ…ظˆط¸ظپ",
  CommissionPayment: "طµط±ظپ ط¹ظ…ظˆظ„ط©",
  SupplierPayment: "ط¯ظپط¹ ظ…ظˆط±ط¯",
  CreditNoteRefund: "ط§ط³طھط±ط¯ط§ط¯ ط¥ط´ط¹ط§ط± ط¯ط§ط¦ظ†",
  VaultTransfer: "طھط±ط­ظٹظ„ ط³ظٹظˆظ„ط©",
  ContractCancellation: "ط¥ظ„ط؛ط§ط، ط¹ظ‚ط¯",
  PaymentDeletion: "ط­ط°ظپ ط¯ظپط¹ط©",
  Other: "ط£ط®ط±ظ‰",
};

/* â”€â”€ Arabic labels for account types â”€â”€ */
const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Treasury: "ط®ط²ظٹظ†ط©/طµظ†ط¯ظˆظ‚",
  PatientReceivable: "ط°ظ…ظ… ظ…ط±ط¶ظ‰ ظ…ط¯ظٹظ†ط©",
  PatientAdvance: "ط¯ظپط¹ط§طھ ظ…ظ‚ط¯ظ…ط© ظ…ط±ط¶ظ‰",
  Payable: "ط°ظ…ظ… ط¯ط§ط¦ظ†ط©",
  Revenue: "ط¥ظٹط±ط§ط¯ط§طھ",
  Expense: "ظ…طµط±ظˆظپط§طھ",
  OwnerEquity: "ط­ظ‚ظˆظ‚ ط§ظ„ظ…ظ„ظƒظٹط©",
  OtherReceivable: "ط°ظ…ظ… ظ…ط¯ظٹظ†ط© ط£ط®ط±ظ‰",
  ContraRevenue: "ط¥ظٹط±ط§ط¯ط§طھ ظ…ظ‚ط§ط¨ظ„ط©",
  ContraExpense: "ظ…طµط±ظˆظپط§طھ ظ…ظ‚ط§ط¨ظ„ط©",
  AccountsPayable: "ط°ظ…ظ… ط¯ط§ط¦ظ†ط© (ظ…ظˆط±ط¯ظٹظ†)",
  SalesReturns: "ظ…ط±طھط¬ط¹ط§طھ ظ…ط¨ظٹط¹ط§طھ",
};

/* â”€â”€ Document type filter options â”€â”€ */
const DOCUMENT_TYPE_OPTIONS = [
  { value: "", label: "ط§ظ„ظƒظ„" },
  { value: "Payment", label: "ط¯ظپط¹ط© ظ…ط±ظٹط¶" },
  { value: "Refund", label: "ط§ط³طھط±ط¯ط§ط¯" },
  { value: "Invoice", label: "ظپط§طھظˆط±ط©" },
  { value: "Expense", label: "ظ…طµط±ظˆظپ طھط´ط؛ظٹظ„ظٹ" },
  { value: "SupplierPayment", label: "ط¯ظپط¹ ظ…ظˆط±ط¯" },
  { value: "CreditNoteRefund", label: "ط§ط³طھط±ط¯ط§ط¯ ط¥ط´ط¹ط§ط± ط¯ط§ط¦ظ†" },
  { value: "VaultTransfer", label: "طھط±ط­ظٹظ„ ط³ظٹظˆظ„ط©" },
  { value: "ContractCancellation", label: "ط¥ظ„ط؛ط§ط، ط¹ظ‚ط¯" },
  { value: "PaymentDeletion", label: "ط­ط°ظپ ط¯ظپط¹ط©" },
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
/* â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
   Tab 10: Journal Entries â€” ظ‚ظٹظˆط¯ ط§ظ„ظٹظˆظ…ظٹط©
   â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ */
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
      toast.error("ظپط´ظ„ ظپظٹ طھط­ظ…ظٹظ„ ظ‚ظٹظˆط¯ ط§ظ„ظٹظˆظ…ظٹط©");
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
      toast.error("ظپط´ظ„ ظپظٹ طھط­ظ…ظٹظ„ طھظپط§طµظٹظ„ ط§ظ„ظ‚ظٹط¯");
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

  const toggleRow = (id: string) => {
    setExpandedRows((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="ظ‚ظٹظˆط¯ ط§ظ„ظٹظˆظ…ظٹط©" action={
        <div className="flex items-center gap-2">
          <button onClick={() => setShowFilters(!showFilters)} className="flex items-center gap-1 px-3 py-1.5 rounded-md text-xs font-medium" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }}>
            <Filter className="w-3.5 h-3.5" /> طھطµظپظٹط©
          </button>
          <button onClick={fetchEntries} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="طھط­ط¯ظٹط«"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {/* â”€â”€ Filters â”€â”€ */}
      {showFilters && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label style={labelStyle}>ظ†ظˆط¹ ط§ظ„ظ…ط³طھظ†ط¯</label>
              <select value={filterDocType} onChange={(e) => { setFilterDocType(e.target.value); setPage(1); }} style={inputStyle}>
                {DOCUMENT_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label style={labelStyle}>ظ…ظ† طھط§ط±ظٹط®</label>
              <input type="date" value={filterFromDate} onChange={(e) => { setFilterFromDate(e.target.value); setPage(1); }} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>ط¥ظ„ظ‰ طھط§ط±ظٹط®</label>
              <input type="date" value={filterToDate} onChange={(e) => { setFilterToDate(e.target.value); setPage(1); }} style={inputStyle} />
            </div>
          </div>
          <div className="flex gap-2 mt-3">
            <button onClick={() => { setFilterDocType(""); setFilterFromDate(""); setFilterToDate(""); setPage(1); }} style={btnGhost}>ظ…ط³ط­ ط§ظ„طھطµظپظٹط©</button>
          </div>
        </div>
      )}

      {/* â”€â”€ KPI summary â”€â”€ */}
      <div className="grid grid-cols-4 gap-3">
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>ط¥ط¬ظ…ط§ظ„ظٹ ط§ظ„ظ‚ظٹظˆط¯</span>
          <p className="text-lg font-bold" style={{ color: tokens.textPrimary }}>{total.toLocaleString("ar-EG")}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>ظ…ظڈط±ط­ظ‘ظ„ط©</span>
          <p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{entries.filter((e) => e.isPosted).length}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>ط¹ظƒط³ظٹط©</span>
          <p className="text-lg font-bold" style={{ color: tokens.warningText }}>{entries.filter((e) => e.isReversal).length}</p>
        </div>
        <div className="rounded-lg border p-3" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <span className="text-xs" style={{ color: tokens.textTertiary }}>ط¥ط¬ظ…ط§ظ„ظٹ ط§ظ„ظ…ط¯ظٹظ†</span>
          <p className="text-lg font-bold" style={{ color: tokens.brand }}>{formatYER(entries.reduce((s, e) => s + e.totalDebit, 0))}</p>
        </div>
      </div>

      {loading ? <LoadingSkeleton /> : entries.length === 0 ? <EmptyState icon={BookOpen} message="ظ„ط§ طھظˆط¬ط¯ ظ‚ظٹظˆط¯ ظٹظˆظ…ظٹط©" /> : (
        <div className="space-y-2">
          {entries.map((entry) => {
            const isExpanded = expandedRows.has(entry.id);
            return (
              <div key={entry.id} className="rounded-lg border" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                {/* â”€â”€ Entry header row â”€â”€ */}
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
                      <span className="text-xs px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.warningBg, color: tokens.warningText }}>ظ‚ظٹط¯ ط¹ظƒط³ظٹ</span>
                    )}
                    <span className="text-xs truncate" style={{ color: tokens.textSecondary }}>{entry.description}</span>
                  </div>
                  <div className="flex items-center gap-4 text-xs flex-shrink-0">
                    <span style={{ color: tokens.textTertiary }}>{safeFormatDate(entry.entryDate)}</span>
                    <span className="font-mono" style={{ color: tokens.successBorder }}>{formatYER(entry.totalDebit)}</span>
                    <span className="font-mono" style={{ color: tokens.dangerBorder }}>{formatYER(entry.totalCredit)}</span>
                    <span className="text-xs" style={{ color: tokens.textTertiary }}>{entry.lineCount} ط¨ظ†ظˆط¯</span>
                    <button onClick={(e) => { e.stopPropagation(); fetchDetail(entry.id); }} className="w-6 h-6 rounded flex items-center justify-center" style={{ color: tokens.brand }} title="طھظپط§طµظٹظ„">
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

                {/* â”€â”€ Expanded lines â”€â”€ */}
                {isExpanded && entry.lines.length > 0 && (
                  <div className="border-t px-4 py-2" style={{ borderColor: tokens.border, backgroundColor: tokens.bg }}>
                    <table className="w-full text-xs">
                      <thead>
                        <tr style={{ color: tokens.textTertiary }}>
                          <th className="text-right py-1 px-2 font-medium">ط§ظ„ط­ط³ط§ط¨</th>
                          <th className="text-right py-1 px-2 font-medium">ط§ظ„ط¨ظٹط§ظ†</th>
                          <th className="text-right py-1 px-2 font-medium">ظ…ط¯ظٹظ†</th>
                          <th className="text-right py-1 px-2 font-medium">ط¯ط§ط¦ظ†</th>
                        </tr>
                      </thead>
                      <tbody>
                        {entry.lines.map((line) => (
                          <tr key={line.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                            <td className="py-1.5 px-2">
                              <span className="font-medium" style={{ color: tokens.textPrimary }}>{ACCOUNT_TYPE_LABELS[line.accountType] ?? line.accountType}</span>
                            </td>
                            <td className="py-1.5 px-2" style={{ color: tokens.textSecondary }}>{line.description || "â€”"}</td>
                            <td className="py-1.5 px-2 font-mono" style={{ color: line.debit > 0 ? tokens.successBorder : tokens.textTertiary }}>
                              {line.debit > 0 ? formatYER(line.debit) : "â€”"}
                            </td>
                            <td className="py-1.5 px-2 font-mono" style={{ color: line.credit > 0 ? tokens.dangerBorder : tokens.textTertiary }}>
                              {line.credit > 0 ? formatYER(line.credit) : "â€”"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr className="font-bold" style={{ borderTop: `2px solid ${tokens.border}` }}>
                          <td className="py-1.5 px-2" style={{ color: tokens.textPrimary }}>ط§ظ„ظ…ط¬ظ…ظˆط¹</td>
                          <td></td>
                          <td className="py-1.5 px-2 font-mono" style={{ color: tokens.successBorder }}>{formatYER(entry.totalDebit)}</td>
                          <td className="py-1.5 px-2 font-mono" style={{ color: tokens.dangerBorder }}>{formatYER(entry.totalCredit)}</td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                )}
              </div>
            );
          })}

          {/* â”€â”€ Pagination â”€â”€ */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-4">
              <button onClick={() => setPage(Math.max(1, page - 1))} disabled={page <= 1} style={{ ...btnGhost, opacity: page <= 1 ? 0.4 : 1 }}>ط§ظ„ط³ط§ط¨ظ‚</button>
              <span className="text-xs" style={{ color: tokens.textSecondary }}>طµظپط­ط© {page} ظ…ظ† {totalPages} ({total} ظ‚ظٹط¯)</span>
              <button onClick={() => setPage(Math.min(totalPages, page + 1))} disabled={page >= totalPages} style={{ ...btnGhost, opacity: page >= totalPages ? 0.4 : 1 }}>ط§ظ„طھط§ظ„ظٹ</button>
            </div>
          )}
        </div>
      )}

      {/* â•گâ•گâ•گ Detail Modal â•گâ•گâ•گ */}
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`ظ‚ظٹط¯ ${detail?.entryNumber ?? ""}`} wide>
        {detailLoading ? (
          <div className="flex items-center justify-center py-8"><Loader2 className="w-6 h-6 animate-spin" style={{ color: tokens.brand }} /></div>
        ) : detail ? (
          <div className="space-y-4">
            {/* Entry metadata */}
            <div className="grid grid-cols-3 gap-4 text-sm">
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ط±ظ‚ظ… ط§ظ„ظ‚ظٹط¯</span><p className="font-mono font-bold" style={{ color: tokens.brand }}>{detail.entryNumber}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ظ†ظˆط¹ ط§ظ„ظ…ط³طھظ†ط¯</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{DOCUMENT_TYPE_LABELS[detail.documentType] ?? detail.documentType}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ط§ظ„طھط§ط±ظٹط®</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{safeFormatDate(detail.entryDate)}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ط§ظ„ظپط±ط¹</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.branchName || "â€”"}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ط§ظ„ط®ط²ظٹظ†ط©</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.treasuryName || "â€”"}</p></div>
              <div><span className="text-xs" style={{ color: tokens.textTertiary }}>ط¨ظˆط§ط³ط·ط©</span><p className="font-medium" style={{ color: tokens.textPrimary }}>{detail.performedByName || "â€”"}</p></div>
            </div>

            {detail.description && (
              <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
                <span className="text-xs font-medium" style={{ color: tokens.infoText }}>ط§ظ„ط¨ظٹط§ظ†: </span>
                <span className="text-xs" style={{ color: tokens.infoText }}>{detail.description}</span>
              </div>
            )}

            {/* Status badges */}
            <div className="flex items-center gap-2">
              <StatusBadge status={detail.isPosted ? "Open" : "Draft"} />
              {detail.isReversal && <StatusBadge status="Cancelled" />}
              {detail.reversalOfEntryNumber && (
                <span className="text-xs" style={{ color: tokens.warningText }}>ط¹ظƒط³ظٹ ظ„ظ‚ظٹط¯ {detail.reversalOfEntryNumber}</span>
              )}
              {detail.reversedByEntryNumber && (
                <span className="text-xs" style={{ color: tokens.dangerText }}>ط¹ظڈظƒط³ ط¨ظ‚ظٹط¯ {detail.reversedByEntryNumber}</span>
              )}
              {detail.isBalanced && (
                <span className="text-xs px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.successBg, color: tokens.successBorder }}>ظ…طھظˆط§ط²ظ†</span>
              )}
            </div>

            {/* Lines table */}
            <div className="overflow-x-auto rounded-lg border" style={{ borderColor: tokens.border }}>
              <table className="w-full text-sm">
                <thead>
                  <tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>ط§ظ„ط­ط³ط§ط¨</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>ط§ظ„ط¨ظٹط§ظ†</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>ظ…ط¯ظٹظ†</th>
                    <th className="text-right px-4 py-2 font-semibold text-xs" style={{ color: tokens.textSecondary }}>ط¯ط§ط¦ظ†</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.lines.map((line) => (
                    <tr key={line.id} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                      <td className="px-4 py-2.5">
                        <span className="font-medium" style={{ color: tokens.textPrimary }}>{ACCOUNT_TYPE_LABELS[line.accountType] ?? line.accountType}</span>
                      </td>
                      <td className="px-4 py-2.5" style={{ color: tokens.textSecondary }}>{line.description || "â€”"}</td>
                      <td className="px-4 py-2.5 font-mono" style={{ color: line.debit > 0 ? tokens.successBorder : tokens.textTertiary, fontWeight: line.debit > 0 ? 700 : 400 }}>
                        {line.debit > 0 ? formatYER(line.debit) : "â€”"}
                      </td>
                      <td className="px-4 py-2.5 font-mono" style={{ color: line.credit > 0 ? tokens.dangerBorder : tokens.textTertiary, fontWeight: line.credit > 0 ? 700 : 400 }}>
                        {line.credit > 0 ? formatYER(line.credit) : "â€”"}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr style={{ borderTop: `2px solid ${tokens.brand}`, backgroundColor: tokens.brandLight }}>
                    <td className="px-4 py-2.5 font-bold" style={{ color: tokens.brand }}>ط§ظ„ظ…ط¬ظ…ظˆط¹</td>
                    <td></td>
                    <td className="px-4 py-2.5 font-mono font-bold" style={{ color: tokens.successBorder }}>{formatYER(detail.totalDebit)}</td>
                    <td className="px-4 py-2.5 font-mono font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(detail.totalCredit)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setDetail(null)} style={btnGhost}>ط¥ط؛ظ„ط§ظ‚</button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}
