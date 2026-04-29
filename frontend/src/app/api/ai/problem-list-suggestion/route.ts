import { NextRequest, NextResponse } from "next/server";
import ZAI from "z-ai-web-dev-sdk";

const SYSTEM_PROMPT = `أنت مساعد ذكي متخصص في تقويم الأسنان. بناءً على بيانات الفحص السريري التقويمي المقدمة، اقترح قائمة المشاكل المحتملة.

القواعد:
1. أعد النتيجة كـ JSON فقط بدون markdown أو شرح.
2. الصيغة المطلوبة:
{
  "suggestions": [
    {
      "category": "skeletal|dental|soft_tissue|functional|space",
      "description": "وصف المشكلة بالعربية",
      "severity": "mild|moderate|severe",
      "reasoning": "السبب بالعربية"
    }
  ]
}
3. يجب أن تكون الأوصاف بالعربية.
4. رتب المشاكل حسب الأهمية السريرية.
5. كن دقيقاً ومنطقياً في اقتراحاتك — هذه مقترحات فقط والطبيب يتخذ القرار النهائي.`;

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { clinicalExam, cephMeasurements, patientAge, patientGender } = body;

    const zai = await ZAI.create();
    const response = await zai.chat.completions.create({
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
        {
          role: "user",
          content: `بيانات المريض: العمر ${patientAge || 'غير محدد'}، الجنس ${patientGender || 'غير محدد'}\n\nبيانات الفحص السريري:\n${JSON.stringify(clinicalExam, null, 2)}\n\nقياسات سيفالومترية:\n${cephMeasurements ? JSON.stringify(cephMeasurements, null, 2) : 'غير متوفرة'}\n\nاقترح قائمة المشاكل.`,
        },
      ],
      temperature: 0.3,
    });

    const content = response.choices?.[0]?.message?.content ?? "";

    // Extract JSON
    const jsonMatch = content.match(/\{[\s\S]*\}/);
    if (!jsonMatch) {
      return NextResponse.json({ error: "فشل تحليل استجابة AI", raw: content }, { status: 422 });
    }

    const parsed = JSON.parse(jsonMatch[0]);
    return NextResponse.json(parsed);
  } catch (error: unknown) {
    console.error("AI problem list suggestion error:", error);
    const message = error instanceof Error ? error.message : "فشل اقتراح قائمة المشاكل";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
