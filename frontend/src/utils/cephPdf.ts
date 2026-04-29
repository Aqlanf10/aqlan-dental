// ---------------------------------------------------------------------------
// Cephalometric PDF Report Generator
// Professional report like Dolphin Imaging
// ---------------------------------------------------------------------------

import jsPDF from "jspdf";
import autoTable from "jspdf-autotable";
import type {
  CephAnalysis,
  CephMeasurement,
  CephDiagnosis,
  MeasurementGroup,
  AnalysisType,
} from "@/types/ceph";
import { ANALYSIS_TYPE_AR, ANALYSIS_GROUPS } from "@/types/ceph";

// ─── Arabic text rendering via offscreen canvas ─────────────────────────────
// jsPDF doesn't support Arabic natively, so we render Arabic strings onto an
// offscreen <canvas> using the browser's built-in RTL text engine, then embed
// the result as a PNG image.  This gives us perfect Arabic rendering with
// zero font-embedding complexity.

const ARABIC_FONT = "Tajawal, Noto Sans Arabic, Arial, sans-serif";

interface RenderedText {
  dataUrl: string;
  width: number;
  height: number;
}

function renderArabicText(
  text: string,
  fontSize: number,
  fontWeight: string = "normal",
  color: string = "#000000",
  maxWidth: number = 700
): RenderedText {
  const canvas = document.createElement("canvas");
  const ctx = canvas.getContext("2d")!;
  ctx.font = `${fontWeight} ${fontSize}px ${ARABIC_FONT}`;

  const metrics = ctx.measureText(text);
  const w = Math.min(Math.ceil(metrics.width) + 8, maxWidth + 8);
  const h = Math.ceil(fontSize * 1.5) + 8;

  canvas.width = w * 2; // 2x for sharpness
  canvas.height = h * 2;
  ctx.scale(2, 2);

  ctx.fillStyle = "transparent";
  ctx.clearRect(0, 0, w, h);

  ctx.font = `${fontWeight} ${fontSize}px ${ARABIC_FONT}`;
  ctx.fillStyle = color;
  ctx.textAlign = "right";
  ctx.direction = "rtl";
  ctx.fillText(text, w - 4, fontSize + 2, maxWidth);

  return {
    dataUrl: canvas.toDataURL("image/png"),
    width: w,
    height: h,
  };
}

// ─── Scientist group labels ─────────────────────────────────────────────────

const GROUP_LABELS: Record<MeasurementGroup, { ar: string; en: string }> = {
  steiner:    { ar: "تحليل ستاينر",         en: "Steiner Analysis" },
  tweed:      { ar: "تحليل تويد",           en: "Tweed Analysis" },
  mcnamara:   { ar: "تحليل ماكنامارا",      en: "McNamara Analysis" },
  ricketts:   { ar: "تحليل ريكتس",          en: "Ricketts Analysis" },
  downs:      { ar: "تحليل داونز",          en: "Downs Analysis" },
  wits:       { ar: "تحليل وتس",            en: "Wits Appraisal" },
  jarabak:    { ar: "تحليل جاراباك",        en: "Jarabak Analysis" },
  softtissue: { ar: "تحليل الأنسجة الرخوة", en: "Soft Tissue Analysis" },
};

const SKELETAL_CLASS_AR: Record<string, string> = {
  "Class I":   "الدرجة الأولى — علاقة هيكلية طبيعية",
  "Class II":  "الدرجة الثانية — تقدم في الفك العلوي أو تراجع في الفك السفلي",
  "Class III": "الدرجة الثالثة — تراجع في الفك العلوي أو تقدم في الفك السفلي",
};

const VERTICAL_AR: Record<string, string> = {
  Normal: "طبيعي",
  Normodivergent: "طبيعي",
  Hyperdivergent: "متشعب (وجه طويل)",
  Hypodivergent: "مضغوط (وجه قصير)",
};

// ─── Severity helpers ────────────────────────────────────────────────────────

function severityIcon(s: CephMeasurement["severity"]): string {
  if (s === "normal") return "✓";
  if (s === "mild") return "⚠";
  return "✗";
}

function severityColor(s: CephMeasurement["severity"]): [number, number, number] {
  if (s === "normal") return [34, 197, 94];    // green
  if (s === "mild") return [245, 158, 11];     // amber
  return [239, 68, 68];                         // red
}

// Used in autoTable cell coloring
void severityColor;

// ─── Main PDF generator ─────────────────────────────────────────────────────

export function generateCephReportPdf(
  analysis: CephAnalysis,
  canvasImage: string | null,
  patientName: string,
  centerName: string = "مركز د. عقلان الكامل لطب وتقويم الأسنان"
): jsPDF {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
  const pageW = doc.internal.pageSize.getWidth();
  const pageH = doc.internal.pageSize.getHeight();
  const margin = 15;
  const contentW = pageW - margin * 2;
  let y = margin;

  // ── Helper: draw Arabic text as image ──
  const drawArabic = (
    text: string,
    x: number,
    yPos: number,
    fontSize: number,
    fontWeight: string = "normal",
    color: string = "#000000",
    maxWidth: number = 700
  ): number => {
    const rendered = renderArabicText(text, fontSize, fontWeight, color, maxWidth);
    const imgW = rendered.width * 0.264583; // px to mm at 96 dpi
    const imgH = rendered.height * 0.264583;
    // x is the RIGHT edge of the text (RTL)
    doc.addImage(rendered.dataUrl, "PNG", x - imgW, yPos, imgW, imgH);
    return imgH;
  };

  // ── Helper: check page overflow ──
  const ensureSpace = (needed: number) => {
    if (y + needed > pageH - 20) {
      doc.addPage();
      y = margin;
    }
  };

  // ── 1. Header ──
  // Center name
  doc.setFontSize(16);
  doc.setFont("helvetica", "bold");
  const headerH = drawArabic(centerName, pageW - margin, y, 18, "bold", "#0F766E");
  y += headerH + 2;

  // Report title
  const titleH = drawArabic(
    "تقرير التحليل السيفالومتري — Cephalometric Analysis Report",
    pageW - margin,
    y,
    12,
    "normal",
    "#374151"
  );
  y += titleH + 4;

  // Separator line
  doc.setDrawColor(15, 118, 110);
  doc.setLineWidth(0.8);
  doc.line(margin, y, pageW - margin, y);
  y += 6;

  // ── 2. Patient Info ──
  // Draw info as a box
  doc.setFillColor(240, 253, 250);
  doc.roundedRect(margin, y, contentW, 28, 2, 2, "F");

  let infoY = y + 3;
  // Row 1
  drawArabic(`المريض: ${patientName}`, pageW / 2 - 2, infoY, 10, "bold", "#111827");
  drawArabic(`رقم الحالة: ${analysis.caseNumber ?? "—"}`, pageW - margin - 2, infoY, 10, "bold", "#111827");
  infoY += 10;
  // Row 2
  drawArabic(
    `تاريخ التحليل: ${analysis.analysisDate ? new Date(analysis.analysisDate).toLocaleDateString("ar-YE") : "—"}`,
    pageW / 2 - 2,
    infoY,
    9,
    "normal",
    "#374151"
  );
  drawArabic(
    `نوع التحليل: ${ANALYSIS_TYPE_AR[analysis.analysisType as AnalysisType] ?? analysis.analysisType}`,
    pageW - margin - 2,
    infoY,
    9,
    "normal",
    "#374151"
  );

  y += 32;

  // ── 3. Annotated Image ──
  if (canvasImage) {
    ensureSpace(80);
    // Draw image with border
    const imgW = contentW;
    const imgH = Math.min(imgW * 0.65, 90);

    doc.setDrawColor(209, 213, 219);
    doc.setLineWidth(0.3);
    doc.roundedRect(margin, y, imgW, imgH, 1, 1, "S");
    doc.addImage(canvasImage, "PNG", margin, y, imgW, imgH);

    y += imgH + 8;
  }

  // ── 4. Measurements Table ──
  const groups = ANALYSIS_GROUPS[analysis.analysisType as AnalysisType] ?? ANALYSIS_GROUPS["full"];

  groups.forEach((group) => {
    const groupMeasurements = analysis.measurements?.filter(
      (m) => m.analysisGroup === group && m.value !== null
    );
    if (!groupMeasurements || groupMeasurements.length === 0) return;

    ensureSpace(40);

    // Group header
    const groupLabel = GROUP_LABELS[group];
    const gHeaderH = drawArabic(
      groupLabel.ar,
      pageW - margin,
      y,
      12,
      "bold",
      "#0F766E"
    );
    y += gHeaderH + 2;

    // Table data
    const tableBody = groupMeasurements.map((m) => [
      severityIcon(m.severity),
      m.nameAr,
      m.value !== null ? `${m.value}${m.unit}` : "—",
      `${m.normal}${m.unit}`,
      m.deviation !== null ? `${m.deviation > 0 ? "+" : ""}${m.deviation}` : "—",
      m.severity === "normal" ? "طبيعي" : m.severity === "mild" ? "خفيف" : "واضح",
    ]);

    autoTable(doc, {
      startY: y,
      margin: { left: margin, right: margin },
      head: [["الحالة", "القياس", "القيمة", "الطبيعي", "الانحراف", "التقييم"]],
      body: tableBody,
      theme: "grid",
      styles: {
        font: "helvetica",
        fontSize: 8,
        halign: "center",
        cellPadding: 2,
      },
      headStyles: {
        fillColor: [15, 118, 110],
        textColor: [255, 255, 255],
        fontStyle: "bold",
        fontSize: 8,
      },
      bodyStyles: {
        textColor: [30, 30, 30],
      },
      didParseCell: (data) => {
        // Color-code rows based on severity
        if (data.section === "body") {
          const m = groupMeasurements[data.row.index];
          if (m) {
            if (m.severity === "normal") {
              data.cell.styles.fillColor = [240, 253, 244];
            } else if (m.severity === "mild") {
              data.cell.styles.fillColor = [255, 251, 235];
            } else {
              data.cell.styles.fillColor = [254, 242, 242];
            }
          }
        }
      },
    });

    // Get the final Y after the table
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    y = (doc as any).lastAutoTable.finalY + 6;
  });

  // ── 5. Diagnosis Summary ──
  const diagnosis: CephDiagnosis | null | undefined = analysis.diagnosis;
  if (diagnosis) {
    ensureSpace(40);

    doc.setFillColor(239, 246, 255);
    doc.roundedRect(margin, y, contentW, 40, 2, 2, "F");

    const diagTitleH = drawArabic(
      "ملخص التشخيص",
      pageW - margin - 4,
      y + 3,
      11,
      "bold",
      "#1E40AF"
    );

    let diagY = y + 3 + diagTitleH + 2;

    if (diagnosis.skeletalClass) {
      const sc = SKELETAL_CLASS_AR[diagnosis.skeletalClass] ?? diagnosis.skeletalClass;
      const h = drawArabic(`الدرجة الهيكلية: ${sc}`, pageW - margin - 4, diagY, 9, "normal", "#1E3A5F");
      diagY += h + 1;
    }
    if (diagnosis.verticalPattern) {
      const vp = VERTICAL_AR[diagnosis.verticalPattern] ?? diagnosis.verticalPattern;
      const h = drawArabic(`النمط الرأسي: ${vp}`, pageW - margin - 4, diagY, 9, "normal", "#1E3A5F");
      diagY += h + 1;
    }
    if (diagnosis.incisorInclination) {
      const h = drawArabic(`ميلان القواطع: ${diagnosis.incisorInclination}`, pageW - margin - 4, diagY, 9, "normal", "#1E3A5F");
      diagY += h + 1;
    }

    y = diagY + 6;
  }

  // ── 6. AI Recommendation ──
  if (diagnosis?.aiRecommendation) {
    ensureSpace(30);

    doc.setFillColor(245, 243, 255);
    doc.roundedRect(margin, y, contentW, 25, 2, 2, "F");

    const aiTitleH = drawArabic(
      "توصية النظام (AI)",
      pageW - margin - 4,
      y + 3,
      10,
      "bold",
      "#6B21A8"
    );

    const recH = drawArabic(
      diagnosis.aiRecommendation.substring(0, 200),
      pageW - margin - 4,
      y + 3 + aiTitleH + 2,
      8,
      "normal",
      "#581C87"
    );

    y += Math.max(25, aiTitleH + recH + 10);
  }

  // ── 7. Doctor's Notes ──
  if (diagnosis?.finalDiagnosis) {
    ensureSpace(25);

    doc.setFillColor(249, 250, 251);
    doc.roundedRect(margin, y, contentW, 20, 2, 2, "F");

    const notesTitleH = drawArabic(
      "ملاحظات الطبيب والتشخيص النهائي",
      pageW - margin - 4,
      y + 3,
      10,
      "bold",
      "#374151"
    );

    drawArabic(
      diagnosis.finalDiagnosis.substring(0, 200),
      pageW - margin - 4,
      y + 3 + notesTitleH + 2,
      8,
      "normal",
      "#4B5563"
    );

    y += 25;
  }

  // Doctor approval status
  if (diagnosis) {
    ensureSpace(10);
    const approved = diagnosis.doctorApproved;
    doc.setFontSize(9);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(approved ? 22 : 185, approved ? 163 : 28, approved ? 74 : 28);
    doc.text(approved ? "✓ Doctor Approved" : "✗ Not Yet Approved", margin, y);
    y += 8;
  }

  // ── 8. Footer (on every page) ──
  const totalPages = doc.getNumberOfPages();
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    const footerY = pageH - 10;

    doc.setDrawColor(209, 213, 219);
    doc.setLineWidth(0.2);
    doc.line(margin, footerY - 3, pageW - margin, footerY - 3);

    doc.setFontSize(7);
    doc.setFont("helvetica", "normal");
    doc.setTextColor(156, 163, 175);

    // Print date
    doc.text(
      `Printed: ${new Date().toLocaleDateString("en-GB")}`,
      margin,
      footerY
    );

    // Page number
    doc.text(
      `Page ${i} / ${totalPages}`,
      pageW - margin - 20,
      footerY
    );

    // Center name
    doc.setFontSize(6);
    doc.text("Aqlan Dental Pro — Cephalometric Report", pageW / 2 - 30, footerY);
  }

  return doc;
}
