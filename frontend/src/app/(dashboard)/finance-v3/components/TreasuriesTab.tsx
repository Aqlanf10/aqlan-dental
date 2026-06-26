"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Plus,
  RefreshCw,
  Landmark,
  ArrowRightLeft,
  Calculator,
  ThumbsUp,
  ThumbsDown,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { Treasury, CreateTreasuryRequest, VaultTransfer, CreateTransferRequest } from "./types";
import { TREASURY_TYPES, DEPOSIT_SOURCES } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDateTime } from "./FinanceHelpers";

const formatMoney = (amount: number, currency?: string | null) =>
  `${(amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${currency ?? "YER"}`;

/* â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
   Tab 7: Treasuries
   â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ */
export function TreasuriesTab() {
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [transfers, setTransfers] = useState<VaultTransfer[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreateTreasury, setShowCreateTreasury] = useState(false);
  const [showCreateTransfer, setShowCreateTransfer] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState<{ id: string; action: "approve" | "reject" } | null>(null);

  // Create treasury form
  const [tName, setTName] = useState("");
  const [tType, setTType] = useState("Vault");
  const [tCurrency, setTCurrency] = useState<"YER" | "SAR" | "USD">("YER");
  const [tBalance, setTBalance] = useState("0");

  // Create transfer form
  const [srcId, setSrcId] = useState("");
  const [dstId, setDstId] = useState("");
  const [trAmount, setTrAmount] = useState("");
  const [trDepositSource, setTrDepositSource] = useState("");
  const [trNotes, setTrNotes] = useState("");
  const sourceTreasury = srcId ? treasuries.find((t) => t.id === srcId) : undefined;
  const destinationTreasury = dstId ? treasuries.find((t) => t.id === dstId) : undefined;
  const isCrossCurrencyTransfer = !!sourceTreasury && !!destinationTreasury && sourceTreasury.currency !== destinationTreasury.currency;

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [tRes, trRes] = await Promise.all([
        api.get<{ data: Treasury[] }>("/api/finance-v3/treasuries"),
        api.get<{ data: VaultTransfer[] }>("/api/finance-v3/vault-transfers"),
      ]);
      setTreasuries(tRes.data.data ?? []);
      setTransfers(trRes.data.data ?? (Array.isArray(trRes.data) ? trRes.data as unknown as VaultTransfer[] : []));
    } catch { toast.error("ظپط´ظ„ ظپظٹ طھط­ظ…ظٹظ„ ط¨ظٹط§ظ†ط§طھ ط§ظ„ط®ط²ط§ط¦ظ†"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateTreasury = async () => {
    if (!tName.trim()) { toast.error("ظٹط±ط¬ظ‰ ط¥ط¯ط®ط§ظ„ ط§ط³ظ… ط§ظ„ط®ط²ظٹظ†ط©"); return; }
    try {
      setSubmitting(true);
      const payload: CreateTreasuryRequest = { name: tName, type: tType, currency: tCurrency, openingBalance: Number(tBalance) || 0 };
      await api.post("/api/finance-v3/treasuries", payload);
      toast.success("طھظ… ط¥ظ†ط´ط§ط، ط§ظ„ط®ط²ظٹظ†ط© ط¨ظ†ط¬ط§ط­");
      setShowCreateTreasury(false);
      setTName(""); setTType("Vault"); setTCurrency("YER"); setTBalance("0");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "ظپط´ظ„ ظپظٹ ط¥ظ†ط´ط§ط، ط§ظ„ط®ط²ظٹظ†ط©")); } finally { setSubmitting(false); }
  };

  const handleCreateTransfer = async () => {
    if (!srcId || !dstId || !trAmount || Number(trAmount) <= 0) {
      toast.error("ظٹط±ط¬ظ‰ ظ…ظ„ط، ط¬ظ…ظٹط¹ ط§ظ„ط­ظ‚ظˆظ„ ط§ظ„ظ…ط·ظ„ظˆط¨ط©");
      return;
    }
    if (srcId === dstId) { toast.error("ظ„ط§ ظٹظ…ظƒظ† ط§ظ„طھط­ظˆظٹظ„ ظ…ظ† ظˆط¥ظ„ظ‰ ظ†ظپط³ ط§ظ„ط®ط²ظٹظ†ط©"); return; }
    if (isCrossCurrencyTransfer) {
      toast.error("لا يمكن التحويل بين خزائن بعملات مختلفة. سجّل عملية مصارفة مستقلة بسعر صرف موثق.");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateTransferRequest = {
        sourceTreasuryId: srcId,
        destinationTreasuryId: dstId,
        amount: Number(trAmount),
        depositSource: trDepositSource || undefined,
        notes: trNotes || undefined,
      };
      await api.post("/api/finance-v3/vault-transfers", payload);
      toast.success("طھظ… ط¥ظ†ط´ط§ط، ط§ظ„طھط­ظˆظٹظ„ ط¨ظ†ط¬ط§ط­");
      setShowCreateTransfer(false);
      setSrcId(""); setDstId(""); setTrAmount(""); setTrDepositSource(""); setTrNotes("");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "ظپط´ظ„ ظپظٹ ط¥ظ†ط´ط§ط، ط§ظ„طھط­ظˆظٹظ„")); } finally { setSubmitting(false); }
  };

  const handleTransferAction = async () => {
    if (!confirmApprove) return;
    try {
      setSubmitting(true);
      const url = confirmApprove.action === "approve"
        ? `/api/vault-transfers/${confirmApprove.id}/approve`
        : `/api/vault-transfers/${confirmApprove.id}/reject`;
      await api.post(url);
      toast.success(confirmApprove.action === "approve" ? "طھظ… ط§ط¹طھظ…ط§ط¯ ط§ظ„طھط­ظˆظٹظ„" : "طھظ… ط±ظپط¶ ط§ظ„طھط­ظˆظٹظ„");
      setConfirmApprove(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "ظپط´ظ„ ظپظٹ طھظ†ظپظٹط° ط§ظ„ط¥ط¬ط±ط§ط،")); } finally { setSubmitting(false); }
  };

  const handleRecalculate = async (id: string) => {
    try {
      await api.post(`/api/finance-v3/treasuries/${id}/recalculate`);
      toast.success("طھظ… ط¥ط¹ط§ط¯ط© ط­ط³ط§ط¨ ط±طµظٹط¯ ط§ظ„ط®ط²ظٹظ†ط©");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "ظپط´ظ„ ظپظٹ ط¥ط¹ط§ط¯ط© ط§ظ„ط­ط³ط§ط¨")); }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Treasuries */}
      <div>
        <SectionHeader title="ط§ظ„ط®ط²ط§ط¦ظ†" action={
          <div className="flex items-center gap-2">
            <button onClick={() => setShowCreateTreasury(true)} style={btnPrimary}><Plus className="w-4 h-4" /> ط®ط²ظٹظ†ط© ط¬ط¯ظٹط¯ط©</button>
            <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="طھط­ط¯ظٹط«"><RefreshCw className="w-4 h-4" /></button>
          </div>
        } />

        {loading ? <LoadingSkeleton /> : treasuries.length === 0 ? <EmptyState icon={Landmark} message="ظ„ط§ طھظˆط¬ط¯ ط®ط²ط§ط¦ظ†" /> : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {treasuries.map((t) => (
              <div key={t.id} className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{t.name}</h4>
                  <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{TREASURY_TYPES.find((x) => x.value === t.type)?.label ?? t.type}</span>
                </div>
                <p className="text-lg font-bold mb-1" style={{ color: tokens.successBorder }}>{formatMoney(t.balance, t.currency)}</p>
                <button onClick={() => handleRecalculate(t.id)} className="text-xs flex items-center gap-1" style={{ color: tokens.brand }}>
                  <Calculator className="w-3 h-3" /> ط¥ط¹ط§ط¯ط© ط­ط³ط§ط¨
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Transfers */}
      <div>
        <SectionHeader title="ط§ظ„طھط­ظˆظٹظ„ط§طھ" action={
          <button onClick={() => setShowCreateTransfer(true)} style={btnPrimary}><ArrowRightLeft className="w-4 h-4" /> طھط­ظˆظٹظ„ ط¬ط¯ظٹط¯</button>
        } />

        {loading ? <LoadingSkeleton /> : transfers.length === 0 ? <EmptyState icon={ArrowRightLeft} message="ظ„ط§ طھظˆط¬ط¯ طھط­ظˆظٹظ„ط§طھ" /> : (
          <DataTable<VaultTransfer>
            keyField="id"
            data={transfers}
            columns={[
              { key: "sourceTreasuryName", label: "ظ…ظ†" },
              { key: "destinationTreasuryName", label: "ط¥ظ„ظ‰" },
              { key: "amount", label: "ط§ظ„ظ…ط¨ظ„ط؛", render: (r) => formatYER(r.amount) },
              { key: "status", label: "ط§ظ„ط­ط§ظ„ط©", render: (r) => <StatusBadge status={r.status} /> },
              { key: "performedBy", label: "ط¨ظˆط§ط³ط·ط©" },
              { key: "transferDate", label: "ط§ظ„طھط§ط±ظٹط®", render: (r) => safeFormatDateTime(r.transferDate) },
              { key: "actions", label: "ط¥ط¬ط±ط§ط،ط§طھ", render: (r) => r.status === "Pending" ? (
                <div className="flex items-center gap-1">
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.id, action: "approve" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.successBorder }} title="ط§ط¹طھظ…ط§ط¯"><ThumbsUp className="w-3.5 h-3.5" /></button>
                  <button onClick={(e) => { e.stopPropagation(); setConfirmApprove({ id: r.id, action: "reject" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="ط±ظپط¶"><ThumbsDown className="w-3.5 h-3.5" /></button>
                </div>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Treasury Modal */}
      <Modal open={showCreateTreasury} onClose={() => setShowCreateTreasury(false)} title="ط®ط²ظٹظ†ط© ط¬ط¯ظٹط¯ط©">
        <div className="space-y-4">
          <div><label style={labelStyle}>ط§ط³ظ… ط§ظ„ط®ط²ظٹظ†ط© <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={tName} onChange={(e) => setTName(e.target.value)} placeholder="ظ…ط«ط§ظ„: ط§ظ„طµظ†ط¯ظˆظ‚ ط§ظ„ط±ط¦ظٹط³ظٹ" style={inputStyle} /></div>
          <div><label style={labelStyle}>ط§ظ„ظ†ظˆط¹</label><select value={tType} onChange={(e) => setTType(e.target.value)} style={inputStyle}>{TREASURY_TYPES.map((t) => (<option key={t.value} value={t.value}>{t.label}</option>))}</select></div>
          <div><label style={labelStyle}>العملة</label><select value={tCurrency} onChange={(e) => setTCurrency(e.target.value as "YER" | "SAR" | "USD")} style={inputStyle}><option value="YER">ريال يمني YER</option><option value="SAR">ريال سعودي SAR</option><option value="USD">دولار USD</option></select></div>
          <div><label style={labelStyle}>ط§ظ„ط±طµظٹط¯ ط§ظ„ط§ظپطھطھط§ط­ظٹ</label><input type="number" min="0" step="0.01" value={tBalance} onChange={(e) => setTBalance(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTreasury(false)} style={btnGhost}>ط¥ظ„ط؛ط§ط،</button>
            <button onClick={handleCreateTreasury} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "ط¬ط§ط±ظچ ط§ظ„ط­ظپط¸..." : "ط¥ظ†ط´ط§ط،"}</button>
          </div>
        </div>
      </Modal>

      {/* Create Transfer Modal */}
      <Modal open={showCreateTransfer} onClose={() => setShowCreateTransfer(false)} title="طھط­ظˆظٹظ„ ط¬ط¯ظٹط¯">
        <div className="space-y-4">
          <div><label style={labelStyle}>ط§ظ„ط®ط²ظٹظ†ط© ط§ظ„ظ…طµط¯ط± <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={srcId} onChange={(e) => setSrcId(e.target.value)} style={inputStyle}><option value="">â€” ط§ط®طھط± â€”</option>{treasuries.map((t) => (<option key={t.id} value={t.id}>{t.name} ({formatMoney(t.balance, t.currency)})</option>))}</select></div>
          <div><label style={labelStyle}>ط§ظ„ط®ط²ظٹظ†ط© ط§ظ„ظˆط¬ظ‡ط© <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={dstId} onChange={(e) => setDstId(e.target.value)} style={inputStyle}><option value="">â€” ط§ط®طھط± â€”</option>{treasuries.map((t) => (<option key={t.id} value={t.id} disabled={!!sourceTreasury && t.currency !== sourceTreasury.currency}>{t.name} ({formatMoney(t.balance, t.currency)})</option>))}</select></div>
          {isCrossCurrencyTransfer && (
            <p className="text-xs rounded-md border px-3 py-2" style={{ color: tokens.dangerBorder, borderColor: tokens.dangerBorder }}>
              لا يمكن التحويل المباشر بين خزائن بعملات مختلفة. استخدم عملية مصارفة مستقلة بسعر صرف موثق.
            </p>
          )}
          <div><label style={labelStyle}>ط§ظ„ظ…ط¨ظ„ط؛ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0.01" max={srcId ? treasuries.find((t) => t.id === srcId)?.balance : undefined} step="0.01" value={trAmount} onChange={(e) => setTrAmount(e.target.value)} dir="ltr" style={inputStyle} />{srcId && Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) && (<p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>âڑ  ط§ظ„ظ…ط¨ظ„ط؛ ظٹطھط¬ط§ظˆط² ط±طµظٹط¯ ط§ظ„ط®ط²ظٹظ†ط© ط§ظ„ظ…طµط¯ط±</p>)}</div>
          {/* Deposit source for external transfers */}
          {(() => { const srcT = treasuries.find((t) => t.id === srcId); return srcT?.type === "External" || (srcId && !treasuries.find((t) => t.id === srcId)); })() ? (
            <div><label style={labelStyle}>ظ…طµط¯ط± ط§ظ„ط¥ظٹط¯ط§ط¹</label><select value={trDepositSource} onChange={(e) => setTrDepositSource(e.target.value)} style={inputStyle}><option value="">â€” ط§ط®طھط± â€”</option>{DEPOSIT_SOURCES.map((d) => (<option key={d.value} value={d.value}>{d.label}</option>))}</select></div>
          ) : null}
          <div><label style={labelStyle}>ظ…ظ„ط§ط­ط¸ط§طھ</label><input value={trNotes} onChange={(e) => setTrNotes(e.target.value)} placeholder="ظ…ظ„ط§ط­ط¸ط§طھ ط§ط®طھظٹط§ط±ظٹط©..." style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateTransfer(false)} style={btnGhost}>ط¥ظ„ط؛ط§ط،</button>
            <button onClick={handleCreateTransfer} disabled={submitting || isCrossCurrencyTransfer || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false)} style={{ ...btnPrimary, opacity: submitting || isCrossCurrencyTransfer || (srcId ? Number(trAmount) > (treasuries.find((t) => t.id === srcId)?.balance ?? 0) : false) ? 0.6 : 1 }}>{submitting ? "ط¬ط§ط±ظچ ط§ظ„ط­ظپط¸..." : "ط¥ظ†ط´ط§ط، طھط­ظˆظٹظ„"}</button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!confirmApprove}
        onClose={() => setConfirmApprove(null)}
        onConfirm={handleTransferAction}
        title={confirmApprove?.action === "approve" ? "ط§ط¹طھظ…ط§ط¯ ط§ظ„طھط­ظˆظٹظ„" : "ط±ظپط¶ ط§ظ„طھط­ظˆظٹظ„"}
        message={confirmApprove?.action === "approve" ? "ظ‡ظ„ ط£ظ†طھ ظ…طھط£ظƒط¯ ظ…ظ† ط§ط¹طھظ…ط§ط¯ ظ‡ط°ط§ ط§ظ„طھط­ظˆظٹظ„طں ط³ظٹطھظ… طھط­ط¯ظٹط« ط£ط±طµط¯ط© ط§ظ„ط®ط²ط§ط¦ظ†." : "ظ‡ظ„ ط£ظ†طھ ظ…طھط£ظƒط¯ ظ…ظ† ط±ظپط¶ ظ‡ط°ط§ ط§ظ„طھط­ظˆظٹظ„طں"}
        confirmLabel={confirmApprove?.action === "approve" ? "ط§ط¹طھظ…ط§ط¯" : "ط±ظپط¶"}
        danger={confirmApprove?.action === "reject"}
      />
    </div>
  );
}
