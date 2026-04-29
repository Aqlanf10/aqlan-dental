import { NextRequest, NextResponse } from "next/server";
import ZAI from "z-ai-web-dev-sdk";

const SYSTEM_PROMPT = `أنت مساعد ذكي متخصص في تقويم الأسنان. بناءً على البيانات السريرية والسيفالومترية، قدم تحليلاً لقرار الخلع.

القواعد:
1. أعد النتيجة كـ JSON فقط.
2. الصيغة المطلوبة:
{
  "recommendation": "no_extraction|extract_4_premolars|extract_2_upper|extract_2_lower|borderline",
  "confidence": 0.0-1.0,
  "proExtraction": ["سبب1", "سبب2"],
  "conExtraction": ["سبب1", "سبب2"],
  "clinicalReasoning": "التحليل السريري بالعربية",
  "alternativeOptions": ["بديل1", "بديل2"],
  "warnings": ["تحذير1"]
}
3. كل النصوص بالعربية.
4. هذا مقترح استشاري فقط — الطبيب يتخذ القرار النهائي.`;

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { clinicalExam, cephMeasurements, modelAnalysis, patientAge, patientGender, problemList } = body;

    const zai = await ZAI.create();
    const response = await zai.chat.completions.create({
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
        {
          role: "user",
          content: `المريض: عمر ${patientAge || 'غير محدد'}، جنس ${patientGender || 'غير محدد'}\n\nالفحص السريري:\n${JSON.stringify(clinicalExam, null, 2)}\n\nالقياسات السيفالومترية:\n${cephMeasurements ? JSON.stringify(cephMeasurements, null, 2) : 'غير متوفرة'}\n\nتحليل الموديل:\n${modelAnalysis ? JSON.stringify(modelAnalysis, null, 2) : 'غير متوفر'}\n\nقائمة المشاكل:\n${problemList ? JSON.stringify(problemList, null, 2) : 'غير متوفرة'}\n\nقدم تحليل قرار الخلع.`,
        },
      ],
      temperature: 0.3,
    });

    const content = response.choices?.[0]?.message?.content ?? "";
    const jsonMatch = content.match(/\{[\s\S]*\}/);
    if (!jsonMatch) {
      return NextResponse.json({ error: "فشل تحليل استجابة AI", raw: content }, { status: 422 });
    }

    return NextResponse.json(JSON.parse(jsonMatch[0]));
  } catch (error: unknown) {
    console.error("AI extraction decision error:", error);
    const message = error instanceof Error ? error.message : "فشل تحليل قرار الخلع";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
