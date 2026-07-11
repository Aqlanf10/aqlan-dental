"use client";

import { useEffect, useRef, useState } from "react";
import { Calendar, GitBranch, Search, User, X } from "lucide-react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";

interface SearchResult {
  patients: { id: string; fullName: string; patientNumber: string; phoneNumber?: string }[];
  appointments: { id: string; patientName: string; appointmentType?: string; appointmentDate: string; doctorName?: string }[];
  orthoCases: { id: string; caseNumber: string; patientName: string; status: string }[];
}

export function TopbarSearch() {
  const router = useRouter();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResult | null>(null);
  const [open, setOpen] = useState(false);
  const [searching, setSearching] = useState(false);
  const [failed, setFailed] = useState(false);

  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const controllerRef = useRef<AbortController | null>(null);
  const requestSequence = useRef(0);

  const invalidateActiveSearch = () => {
    requestSequence.current += 1;
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    controllerRef.current?.abort();
    controllerRef.current = null;
  };

  const resetSearch = () => {
    invalidateActiveSearch();
    setQuery("");
    setResults(null);
    setSearching(false);
    setFailed(false);
    setOpen(false);
  };

  const runSearch = async (value: string, requestId: number) => {
    const controller = new AbortController();
    controllerRef.current = controller;

    try {
      const { data } = await api.get<SearchResult>(
        `/api/search?q=${encodeURIComponent(value)}&limit=4`,
        { signal: controller.signal },
      );

      if (controller.signal.aborted || requestId !== requestSequence.current) return;
      setResults(data);
      setFailed(false);
      setOpen(true);
    } catch {
      if (controller.signal.aborted || requestId !== requestSequence.current) return;
      setResults(null);
      setFailed(true);
      setOpen(true);
    } finally {
      if (requestId === requestSequence.current) {
        setSearching(false);
        if (controllerRef.current === controller) controllerRef.current = null;
      }
    }
  };

  const handleInput = (value: string) => {
    invalidateActiveSearch();
    setQuery(value);
    setResults(null);
    setFailed(false);

    if (value.length < 2) {
      setSearching(false);
      setOpen(false);
      return;
    }

    setSearching(true);
    setOpen(true);
    const requestId = requestSequence.current;
    timerRef.current = setTimeout(() => {
      timerRef.current = null;
      void runSearch(value, requestId);
    }, 300);
  };

  useEffect(() => {
    const handleOutside = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handleOutside);
    return () => document.removeEventListener("mousedown", handleOutside);
  }, []);

  useEffect(() => {
    const handleKey = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
      if (event.key === "Escape") {
        setOpen(false);
        inputRef.current?.blur();
      }
    };
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, []);

  useEffect(() => () => invalidateActiveSearch(), []);

  const hasResults = Boolean(
    results &&
      (results.patients.length > 0 ||
        results.appointments.length > 0 ||
        results.orthoCases.length > 0),
  );

  const navigate = (href: string) => {
    resetSearch();
    router.push(href);
  };

  return (
    <div ref={rootRef} className="relative hidden md:block">
      <input
        ref={inputRef}
        type="search"
        value={query}
        onChange={(event) => handleInput(event.target.value)}
        onFocus={() => {
          if (query.length >= 2 && (searching || failed || results !== null)) setOpen(true);
        }}
        placeholder="بحث سريع... (Ctrl+K)"
        aria-label="البحث السريع"
        className="pe-9 ps-3 py-[7px] text-[13px] rounded-lg outline-none"
        style={{
          width: 220,
          border: "1.5px solid #dce8f5",
          background: "#f7fafd",
          color: "#0d2137",
          direction: "rtl",
          fontFamily: "Tajawal",
        }}
      />
      <span className="absolute left-2.5 top-1/2 -translate-y-1/2" style={{ color: "#94a3b8" }}>
        <Search className="w-[15px] h-[15px]" />
      </span>
      {query && (
        <button
          type="button"
          aria-label="مسح البحث"
          onClick={resetSearch}
          className="absolute right-2.5 top-1/2 -translate-y-1/2"
          style={{ color: "#94a3b8" }}
        >
          <X className="w-3.5 h-3.5" />
        </button>
      )}

      {open && (
        <div
          className="absolute top-full mt-2 end-0 w-80 bg-white rounded-xl z-50 overflow-hidden"
          style={{ boxShadow: "0 8px 30px rgba(0,0,0,0.12)", border: "1px solid #e8f0f9" }}
        >
          {searching ? (
            <div className="p-4 text-center text-sm" style={{ color: "#94a3b8" }}>
              جارٍ البحث...
            </div>
          ) : failed ? (
            <div className="p-4 text-center text-sm" style={{ color: "#dc2626" }}>
              تعذّر البحث، تحقق من الاتصال
            </div>
          ) : !hasResults ? (
            <div className="p-4 text-center text-sm" style={{ color: "#94a3b8" }}>
              لا توجد نتائج لـ &ldquo;{query}&rdquo;
            </div>
          ) : (
            <div className="py-1">
              {results!.patients.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>
                    المرضى
                  </div>
                  {results!.patients.map((patient) => (
                    <button
                      key={patient.id}
                      type="button"
                      onClick={() => navigate(`/patients/${patient.id}`)}
                      className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                      onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                      onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
                    >
                      <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#3d7ab518" }}>
                        <User className="w-3.5 h-3.5" style={{ color: "#3d7ab5" }} />
                      </div>
                      <div>
                        <p className="text-sm font-medium" style={{ color: "#0d2137" }}>{patient.fullName}</p>
                        <p className="text-xs" style={{ color: "#94a3b8" }}>
                          {patient.patientNumber} {patient.phoneNumber && `· ${patient.phoneNumber}`}
                        </p>
                      </div>
                    </button>
                  ))}
                </div>
              )}

              {results!.orthoCases.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>
                    التقويم
                  </div>
                  {results!.orthoCases.map((orthoCase) => (
                    <button
                      key={orthoCase.id}
                      type="button"
                      onClick={() => navigate(`/ortho/${orthoCase.id}`)}
                      className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                      onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                      onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
                    >
                      <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#a855f718" }}>
                        <GitBranch className="w-3.5 h-3.5" style={{ color: "#a855f7" }} />
                      </div>
                      <div>
                        <p className="text-sm font-medium" style={{ color: "#0d2137" }}>
                          {orthoCase.caseNumber} — {orthoCase.patientName}
                        </p>
                        <p className="text-xs" style={{ color: "#94a3b8" }}>{orthoCase.status}</p>
                      </div>
                    </button>
                  ))}
                </div>
              )}

              {results!.appointments.length > 0 && (
                <div>
                  <div className="px-3 py-1.5 text-xs font-semibold" style={{ color: "#94a3b8", background: "#f7fafd" }}>
                    المواعيد
                  </div>
                  {results!.appointments.map((appointment) => (
                    <button
                      key={appointment.id}
                      type="button"
                      onClick={() => navigate(`/appointments/${appointment.id}`)}
                      className="w-full flex items-center gap-3 px-3 py-2.5 transition text-start"
                      onMouseEnter={(event) => (event.currentTarget.style.background = "#f7fafd")}
                      onMouseLeave={(event) => (event.currentTarget.style.background = "transparent")}
                    >
                      <div className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: "#3d7ab518" }}>
                        <Calendar className="w-3.5 h-3.5" style={{ color: "#3d7ab5" }} />
                      </div>
                      <div>
                        <p className="text-sm font-medium" style={{ color: "#0d2137" }}>{appointment.patientName}</p>
                        <p className="text-xs" style={{ color: "#94a3b8" }}>
                          {appointment.appointmentDate} {appointment.doctorName && `· ${appointment.doctorName}`}
                        </p>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
