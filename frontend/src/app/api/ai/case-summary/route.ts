import { NextRequest, NextResponse } from "next/server";
import ZAI from "z-ai-web-dev-sdk";

const SYSTEM_PROMPT = `أنت مساعد ذكي متخصص في تقويم الأسنان. بناءً على جميع بيانات الحالة التقويمية، أنشئ ملخصاً شاملاً.

القواعد:
1. أعد النتيجة كـ JSON فقط.
2. الصيغة:
{
  "executiveSummary": "ملخص تنفيذي موجز بالعربية",
  "skeletalAnalysis": "تحليل الهيكل العظمي بالعربية",
  "dentalAnalysis": "تحليل الأسنان بالعربية",
  "softTissueAnalysis": "تحليل الأنسجة الرخوة بالعربية",
  "treatmentProgress": "تقدم العلاج بالعربية",
  "recommendations": ["توصية1", "توصية2"],
  "riskFactors": ["عامل خطر1"]
}
3. كل النصوص بالعربية.
4. كن دقيقاً ومبنياً على البيانات — لا تخترع معلومات.`;

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { caseData } = body;

    const zai = await ZAI.create();
    const response = await zai.chat.completions.create({
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
        {
          role: "user",
          content: `بيانات الحالة التقويمية:\n${JSON.stringify(caseData, null, 2)}\n\nأنشئ ملخص الحالة.`,
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
    console.error("AI case summary error:", error);
    const message = error instanceof Error ? error.message : "فشل إنشاء ملخص الحالة";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
