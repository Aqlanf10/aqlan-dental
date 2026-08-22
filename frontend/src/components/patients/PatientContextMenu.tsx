"use client";
import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useRouter } from "next/navigation";
import {
  Eye,
  Pencil,
  CalendarPlus,
  MessageSquare,
  MessageCircle,
  Phone,
  Printer,
  Archive,
  RotateCcw,
  ChevronLeft,
  FileText,
  Wallet,
  ImageIcon,
  Copy,
} from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import { formatPhoneForWhatsApp, normalizePhone } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";

export interface ContextMenuPosition {
  x: number;
  y: number;
}

interface Props {
  patient: PatientListItem | null;
  position: ContextMenuPosition | null;
  isAdmin?: boolean;
  canViewFinance?: boolean;
  onClose: () => void;
  onOpen: (id: string) => void;
  onEdit: (id: string) => void;
  onNewAppointment: (id: string) => void;
  onMessage: (id: string) => void;
  onArchive: (patient: PatientListItem) => void;
  onRestore: (patient: PatientListItem) => void;
}

interface MenuItem {
  icon: React.ReactNode;
  label: string;
  action: () => void;
  className?: string;
  divider?: boolean;
  show?: boolean;
  isSubmenu?: boolean;
}

export function PatientContextMenu({
  patient,
  position,
  isAdmin,
  canViewFinance = false,
  onClose,
  onOpen,
  onEdit,
  onNewAppointment,
  onMessage,
  onArchive,
  onRestore,
}: Props) {
  const menuRef = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const [printSubOpen, setPrintSubOpen] = useState(false);

  useEffect(() => {
    const handle = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("mousedown", handle);
    document.addEventListener("keydown", handleKey);
    return () => {
      document.removeEventListener("mousedown", handle);
      document.removeEventListener("keydown", handleKey);
    };
  }, [onClose]);

  if (!patient || !position) return null;

  // Adjust position to avoid overflow
  const menuWidth = 220;
  const menuHeight = 310;
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  let x = position.x;
  let y = position.y;
  if (x + menuWidth > vw) x = vw - menuWidth - 8;
  if (y + menuHeight > vh) y = vh - menuHeight - 8;
  if (x < 8) x = 8;
  if (y < 8) y = 8;

  const isArchived = !patient.isActive;

  const items: MenuItem[] = [
    {
      icon: <Eye className="w-4 h-4" />,
      label: "عرض الملف",
      action: () => { onOpen(patient.id); onClose(); },
    },
    {
      icon: <Pencil className="w-4 h-4" />,
      label: "تعديل البيانات",
      action: () => { onEdit(patient.id); onClose(); },
      show: !isArchived,
    },
    {
      icon: <CalendarPlus className="w-4 h-4" />,
      label: "موعد جديد",
      action: () => { onNewAppointment(patient.id); onClose(); },
      divider: true,
      show: !isArchived,
    },
    {
      icon: <MessageSquare className="w-4 h-4" />,
      label: "رسالة داخلية",
      action: () => { onMessage(patient.id); onClose(); },
      show: !isArchived,
    },
    {
      icon: <MessageCircle className="w-4 h-4" />,
      label: "واتساب",
      action: () => {
        const phone = formatPhoneForWhatsApp(patient.phone);
        if (phone) window.open(`https://wa.me/${phone}`, "_blank");
        onClose();
      },
      show: !!patient.phone && !isArchived,
    },
    {
      icon: <Phone className="w-4 h-4" />,
      label: "اتصال هاتفي",
      action: () => {
        const phone = normalizePhone(patient.phone ?? "");
        if (phone) window.open(`tel:${phone}`, "_self");
        onClose();
      },
      show: !!patient.phone && !isArchived,
    },
    {
      icon: <Copy className="w-4 h-4" />,
      label: "نسخ الرقم",
      action: () => {
        if (patient.phone) {
          navigator.clipboard.writeText(patient.phone).catch(() => {});
        }
        onClose();
      },
      show: !!patient.phone && !isArchived,
    },
    {
      icon: <Printer className="w-4 h-4" />,
      label: "طباعة",
      action: () => { setPrintSubOpen(!printSubOpen); },
      divider: true,
      show: !isArchived,
      isSubmenu: true,
    },
    {
      icon: <Archive className="w-4 h-4" />,
      label: "أرشفة المريض",
      action: () => { onArchive(patient); onClose(); },
      className: "text-red-600 hover:bg-red-50",
      show: !isArchived && isAdmin,
    },
    {
      icon: <RotateCcw className="w-4 h-4" />,
      label: "استعادة المريض",
      action: () => { onRestore(patient); onClose(); },
      className: "text-green-700 hover:bg-green-50",
      show: isArchived && isAdmin,
    },
  ].filter((item) => item.show !== false);

  const menu = (
    <div
      ref={menuRef}
      style={{ position: "fixed", top: y, left: x, zIndex: 9999, minWidth: menuWidth }}
      className="bg-white rounded-xl shadow-2xl border border-[#e8f0f9] py-1 text-sm"
     
    >
      {/* Header */}
      <div className="px-3 py-2 border-b border-gray-100">
        <p className="font-semibold text-gray-800 truncate">{patient.fullName}</p>
        <p className="text-xs text-gray-400">{patient.patientNumber}</p>
      </div>
      {/* Items */}
      {items.map((item, i) => (
        <div key={i}>
          {item.divider && i > 0 && <div className="h-px bg-[#f1f5f9] my-1" />}
          <div className="relative">
            <button
              onClick={item.action}
              className={`w-full flex items-center justify-between gap-3 px-3 py-2 hover:bg-gray-50 transition text-gray-700 ${item.className ?? ""}`}
            >
              <span className="flex items-center gap-3">
                <span className="flex-shrink-0 text-gray-400">{item.icon}</span>
                {item.label}
              </span>
              {item.isSubmenu && (
                <ChevronLeft className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
              )}
            </button>
            {/* Print submenu */}
            {item.isSubmenu && printSubOpen && (
              <div
                className="absolute right-full top-0 ms-0.5 bg-white rounded-xl shadow-2xl border border-[#e8f0f9] py-1 min-w-48 z-10"
               
              >
                <button
                  onClick={(e) => { e.stopPropagation(); router.push(`/patients/${patient.id}/print/summary`); onClose(); }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-sm hover:bg-gray-50 transition text-gray-700"
                >
                  <FileText className="w-4 h-4 text-gray-400" />
                  طباعة ملف المريض
                </button>
                {canViewFinance && (
                  <button
                    onClick={async (e) => {
                      e.stopPropagation();
                      onClose();
                      try {
                        const filename = `financial-statement-${patient.patientNumber || patient.id}.pdf`;
                        await downloadPdfFromApi(`/api/patients/${patient.id}/financial-statement/pdf`, filename);
                      } catch {
                        toast.error("فشل في تحميل كشف الحساب المالي");
                      }
                    }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm hover:bg-gray-50 transition text-gray-700"
                  >
                    <Wallet className="w-4 h-4 text-gray-400" />
                    طباعة التقرير المالي
                  </button>
                )}
                <button
                  onClick={(e) => { e.stopPropagation(); router.push(`/patients/${patient.id}/print/media`); onClose(); }}
                  className="w-full flex items-center gap-2.5 px-3 py-2 text-sm hover:bg-gray-50 transition text-gray-700"
                >
                  <ImageIcon className="w-4 h-4 text-gray-400" />
                  طباعة وسائط المريض
                </button>
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );

  if (typeof document === "undefined") return null;
  return createPortal(menu, document.body);
}
