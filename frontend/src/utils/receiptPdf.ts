// ---------------------------------------------------------------------------
// Receipt PDF Generator — سند قبض
// Professional receipt with Arabic text rendering via offscreen canvas
// ---------------------------------------------------------------------------

import jsPDF from "jspdf";
import type { Payment } from "@/types/finance";

// ─── Arabic text rendering via offscreen canvas ─────────────────────────────
// Same pattern as cephPdf.ts — jsPDF doesn't support Arabic natively,
// so we render Arabic strings onto an offscreen <canvas> using the browser's
// built-in RTL text engine, then embed the result as a PNG image.

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

  canvas.width = w * 2;
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

// ─── Arabic number to words ─────────────────────────────────────────────────

const ARABIC_ONES = [
  "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
  "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
  "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر",
];

const ARABIC_TENS = [
  "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون",
];

const ARABIC_HUNDREDS = [
  "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة",
];

function numberToArabicWords(num: number): string {
  if (num === 0) return "صفر";
  if (num < 0) return "سالب " + numberToArabicWords(-num);
  if (num < 20) return ARABIC_ONES[num];
  if (num < 100) {
    const ones = num % 10;
    const tens = Math.floor(num / 10);
    if (ones === 0) return ARABIC_TENS[tens];
    return ARABIC_ONES[ones] + " و" + ARABIC_TENS[tens];
  }
  if (num < 1000) {
    const remainder = num % 100;
    const hundreds = Math.floor(num / 100);
    if (remainder === 0) return ARABIC_HUNDREDS[hundreds];
    return ARABIC_HUNDREDS[hundreds] + " و" + numberToArabicWords(remainder);
  }
  if (num < 1000000) {
    const remainder = num % 1000;
    const thousands = Math.floor(num / 1000);
    let thousandLabel: string;
    if (thousands === 1) thousandLabel = "ألف";
    else if (thousands === 2) thousandLabel = "ألفان";
    else if (thousands < 11) thousandLabel = numberToArabicWords(thousands) + " آلاف";
    else thousandLabel = numberToArabicWords(thousands) + " ألف";

    if (remainder === 0) return thousandLabel;
    return thousandLabel + " و" + numberToArabicWords(remainder);
  }
  if (num < 1000000000) {
    const remainder = num % 1000000;
    const millions = Math.floor(num / 1000000);
    let millionLabel: string;
    if (millions === 1) millionLabel = "مليون";
    else if (millions === 2) millionLabel = "مليونان";
    else millionLabel = numberToArabicWords(millions) + " مليون";

    if (remainder === 0) return millionLabel;
    return millionLabel + " و" + numberToArabicWords(remainder);
  }
  return num.toLocaleString("ar-YE");
}

function amountToArabicWords(amount: number): string {
  const riyals = Math.floor(amount);
  const fils = Math.round((amount - riyals) * 100);
  let result = numberToArabicWords(riyals) + " ريال";
  if (fils > 0) {
    result += " و" + numberToArabicWords(fils) + " فلس";
  }
  return result + " لا غير";
}

// ─── Payment method labels ──────────────────────────────────────────────────

const METHOD_LABELS: Record<string, string> = {
  cash: "نقداً",
  bank_transfer: "تحويل بنكي",
  card: "بطاقة ائتمانية",
};

// ─── Main PDF generator ─────────────────────────────────────────────────────

export function generateReceiptPdf(
  payment: Payment,
  centerName: string = "مركز د. عقلان الكامل لطب الأسنان",
  centerAddress: string = "تعز - اليمن",
  centerPhones: string = "777123456 / 777789012"
): jsPDF {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
  const pageW = doc.internal.pageSize.getWidth();
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
    const imgW = rendered.width * 0.264583;
    const imgH = rendered.height * 0.264583;
    doc.addImage(rendered.dataUrl, "PNG", x - imgW, yPos, imgW, imgH);
    return imgH;
  };

  // ── Helper: centered Arabic text ──
  const drawArabicCentered = (
    text: string,
    yPos: number,
    fontSize: number,
    fontWeight: string = "normal",
    color: string = "#000000"
  ): number => {
    const rendered = renderArabicText(text, fontSize, fontWeight, color, pageW);
    const imgW = rendered.width * 0.264583;
    const imgH = rendered.height * 0.264583;
    const x = (pageW - imgW) / 2;
    doc.addImage(rendered.dataUrl, "PNG", x, yPos, imgW, imgH);
    return imgH;
  };

  // ── Helper: draw a horizontal line ──
  const drawLine = (yPos: number, color: string = "#0F766E", width: number = 0.8) => {
    doc.setDrawColor(color.replace("#", ""));
    const r = parseInt(color.slice(1, 3), 16);
    const g = parseInt(color.slice(3, 5), 16);
    const b = parseInt(color.slice(5, 7), 16);
    doc.setDrawColor(r, g, b);
    doc.setLineWidth(width);
    doc.line(margin, yPos, pageW - margin, yPos);
  };

  // ── 1. Header: Center Identity ──
  // Decorative top line
  drawLine(y, "#0F766E", 1.5);
  y += 5;

  // Center name (large, bold, teal)
  const nameH = drawArabicCentered(centerName, y, 16, "bold", "#0F766E");
  y += nameH + 2;

  // Center address
  const addrH = drawArabicCentered(centerAddress, y, 10, "normal", "#374151");
  y += addrH + 1;

  // Center phones
  const phoneH = drawArabicCentered("هاتف: " + centerPhones, y, 9, "normal", "#6B7280");
  y += phoneH + 3;

  // Separator
  drawLine(y, "#0F766E", 1.5);
  y += 6;

  // ── 2. Receipt Title ──
  const titleH = drawArabicCentered("سند قبض", y, 14, "bold", "#111827");
  y += titleH + 4;

  // ── 3. Receipt Number & Date ──
  // Light background box
  doc.setFillColor(240, 253, 250);
  doc.roundedRect(margin, y, contentW, 14, 2, 2, "F");

  // Receipt number (right side)
  drawArabic(
    `رقم السند: ${payment.receiptNumber ?? "—"}`,
    pageW - margin - 4,
    y + 2,
    10,
    "bold",
    "#111827"
  );

  // Date (left side)
  const dateStr = payment.paymentDate
    ? new Date(payment.paymentDate).toLocaleDateString("ar-YE", {
        year: "numeric",
        month: "long",
        day: "numeric",
      })
    : "—";
  drawArabic(
    `التاريخ: ${dateStr}`,
    pageW / 2 - 4,
    y + 2,
    10,
    "normal",
    "#374151"
  );
  y += 18;

  // ── 4. Patient & Service Info ──
  // Separator
  drawLine(y, "#D1D5DB", 0.3);
  y += 4;

  // Patient name
  drawArabic("اسم المريض:", pageW - margin - 4, y, 9, "normal", "#6B7280");
  drawArabic(payment.patientName, pageW / 2 - 4, y, 11, "bold", "#111827");
  y += 8;

  // Patient number
  if (payment.patientNumber) {
    drawArabic("رقم المريض:", pageW - margin - 4, y, 9, "normal", "#6B7280");
    drawArabic(payment.patientNumber, pageW / 2 - 4, y, 10, "normal", "#374151");
    y += 7;
  }

  // Service description
  if (payment.serviceDescription) {
    drawArabic("مقابل:", pageW - margin - 4, y, 9, "normal", "#6B7280");
    drawArabic(payment.serviceDescription, pageW / 2 - 4, y, 10, "normal", "#374151");
    y += 7;
  }

  y += 3;

  // ── 5. Amount Section ──
  // Large amount box
  doc.setFillColor(240, 253, 244);
  doc.roundedRect(margin, y, contentW, 24, 3, 3, "F");

  // Amount in numbers (large)
  const amountStr = payment.amount.toLocaleString("ar-YE");
  const amountH = drawArabicCentered(
    `${amountStr} ر.ي`,
    y + 2,
    18,
    "bold",
    "#166534"
  );
  y += amountH + 3;

  // Amount in Arabic words
  const wordsStr = amountToArabicWords(payment.amount);
  drawArabicCentered(wordsStr, y, 9, "normal", "#15803D");
  y += 10;

  // ── 6. Payment Method ──
  drawLine(y, "#D1D5DB", 0.3);
  y += 4;

  drawArabic("طريقة الدفع:", pageW - margin - 4, y, 9, "normal", "#6B7280");
  const methodLabel = METHOD_LABELS[payment.paymentMethod ?? "cash"] ?? payment.paymentMethod ?? "—";
  drawArabic(methodLabel, pageW / 2 - 4, y, 10, "bold", "#111827");
  y += 8;

  // Doctor
  if (payment.doctorName) {
    drawArabic("استلم بواسطة:", pageW - margin - 4, y, 9, "normal", "#6B7280");
    drawArabic(payment.doctorName, pageW / 2 - 4, y, 10, "normal", "#374151");
    y += 8;
  }

  // Notes
  if (payment.notes) {
    drawArabic("ملاحظات:", pageW - margin - 4, y, 9, "normal", "#6B7280");
    drawArabic(payment.notes, pageW / 2 - 4, y, 9, "normal", "#374151");
    y += 8;
  }

  y += 8;

  // ── 7. Signature Area ──
  drawLine(y, "#D1D5DB", 0.3);
  y += 5;

  // Three columns: Patient signature | Center stamp | Receiver signature
  const colW = contentW / 3;

  // Patient signature (right)
  drawArabic("توقيع المريض", margin + colW * 3 - 4, y, 8, "normal", "#6B7280");
  doc.setDrawColor(156, 163, 175);
  doc.setLineWidth(0.2);
  doc.line(margin + colW * 2 + 10, y + 12, margin + colW * 3 - 4, y + 12);

  // Center stamp (center)
  drawArabic("ختم المركز", margin + colW * 2 - colW / 2, y, 8, "normal", "#6B7280");
  doc.setDrawColor(156, 163, 175);
  doc.setLineWidth(0.3);
  doc.setLineDashPattern([2, 2], 0);
  doc.circle(margin + colW * 1.5, y + 14, 8, "S");
  doc.setLineDashPattern([], 0);

  // Receiver signature (left)
  drawArabic("توقيع المستلم", margin + colW - 4, y, 8, "normal", "#6B7280");
  doc.setDrawColor(156, 163, 175);
  doc.setLineWidth(0.2);
  doc.line(margin + 4, y + 12, margin + colW - 10, y + 12);

  y += 28;

  // ── 8. Footer ──
  drawLine(y, "#0F766E", 1.0);
  y += 4;

  drawArabicCentered("شكراً لزيارتكم", y, 12, "bold", "#0F766E");
  y += 7;
  drawArabicCentered(
    `تم الطباعة: ${new Date().toLocaleDateString("ar-YE")} ${new Date().toLocaleTimeString("ar-YE")}`,
    y,
    7,
    "normal",
    "#9CA3AF"
  );

  return doc;
}
