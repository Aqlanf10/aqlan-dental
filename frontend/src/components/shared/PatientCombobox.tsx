"use client";
import { useEffect, useRef, useState } from "react";
import { Search, Loader2 } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface Props {
  defaultDisplayValue?: string;
  onSelect: (patient: PatientListItem) => void;
  error?: string;
  readOnly?: boolean;
  placeholder?: string;
}

export function PatientCombobox({
  defaultDisplayValue = "",
  onSelect,
  error,
  readOnly = false,
  placeholder = "ابحث بالاسم أو رقم المريض...",
}: Props) {
  const [query, setQuery] = useState(defaultDisplayValue);
  const [results, setResults] = useState<PatientListItem[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => { setQuery(defaultDisplayValue); }, [defaultDisplayValue]);

  useEffect(() => {
    if (query.length < 2) { setResults([]); setFailed(false); return; }
    setLoading(true);
    setFailed(false);
    const t = setTimeout(() => {
      api
        .get<PaginatedResponse<PatientListItem>>(
          `/api/patients?search=${encodeURIComponent(query)}&pageSize=8`
        )
        .then((r) => { setResults(r.data.data ?? []); setLoading(false); })
        .catch(() => { setLoading(false); setFailed(true); });
    }, 300);
    return () => clearTimeout(t);
  }, [query]);

  useEffect(() => {
    if (!open) return;
    const h = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", h);
    return () => document.removeEventListener("mousedown", h);
  }, [open]);

  const showMenu = open && !readOnly && query.length >= 2;

  return (
    <div ref={ref} className="relative">
      <Search className="absolute end-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
      {loading && (
        <Loader2 className="absolute start-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-clinic-teal animate-spin pointer-events-none" />
      )}
      <input
        type="text"
        value={query}
        onChange={(e) => { if (!readOnly) { setQuery(e.target.value); setOpen(true); } }}
        onFocus={() => { if (!readOnly && query.length >= 2) setOpen(true); }}
        placeholder={placeholder}
        readOnly={readOnly}
        autoComplete="off"
        className={cn(
          "w-full px-3 py-2 text-sm rounded-lg border bg-white",
          "focus:outline-none focus:ring-2 focus:ring-clinic-teal",
          "pe-9", loading && "ps-8",
          error ? "border-red-400" : "border-gray-300",
          readOnly && "bg-gray-50 cursor-not-allowed"
        )}
      />

      {showMenu && (
        <div className="absolute z-50 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-xl max-h-52 overflow-y-auto">
          {loading && (
            <p className="px-3 py-3 text-sm text-gray-400 text-center">جارٍ البحث...</p>
          )}
          {!loading && failed && (
            <p className="px-3 py-3 text-sm text-red-500 text-center">تعذّر البحث، تحقق من الاتصال</p>
          )}
          {!loading && !failed && results.length === 0 && (
            <p className="px-3 py-3 text-sm text-gray-400 text-center">
              لا توجد نتائج لـ &quot;{query}&quot;
            </p>
          )}
          {!loading && results.map((p) => (
            <button
              key={p.id}
              type="button"
              onMouseDown={() => { onSelect(p); setQuery(`${p.fullName} (${p.patientNumber})`); setOpen(false); }}
              className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between transition"
            >
              <span className="font-medium text-gray-900">{p.fullName}</span>
              <span className="text-xs text-gray-400 font-mono">{p.patientNumber}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
