import { NextRequest, NextResponse } from "next/server";
import ZAI from "z-ai-web-dev-sdk";

const SYSTEM_PROMPT = `أنت مساعد ذكي متخصص في تقويم الأسنان. بناءً على بيانات الحالة والتحليل السيفالومتري، اقترح تحركات VTO (الهدف العلاجي البصري).

القواعد:
1. أعد النتيجة كـ JSON فقط.
2. الصيغة:
{
  "targetMovements": [
    {
      "landmark": "مفتاح النقطة (مثل A, B, Pog, U1T, L1T)",
      "deltaX": "التغير الأفقي بالمم (موجب = أمامي، سالب = خلفي)",
      "deltaY": "التغير العمودي بالمم (موجب = أسفل، سالب = أعلى)",
      "description": "وصف الحركة بالعربية"
    }
  ],
  "expectedOutcomes": ["نتيجة متوقعة1"],
  "treatmentSequence": ["خطوة1"],
  "estimatedDuration": "المدة التقديرية",
  "warnings": ["تحذير1"]
}
3. كل الأوصاف بالعربية.
4. الحركات يجب أن تكون واقعية ومدعومة بالبيانات السريرية.`;

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { caseData, treatmentPlan } = body;

    const zai = await ZAI.create();
    const response = await zai.chat.completions.create({
      messages: [
        { role: "system", content: SYSTEM_PROMPT },
        {
          role: "user",
          content: `بيانات الحالة:\n${JSON.stringify(caseData, null, 2)}\n\nخطة العلاج:\n${treatmentPlan ? JSON.stringify(treatmentPlan, null, 2) : 'غير محددة'}\n\nاقترح تحركات VTO.`,
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
    console.error("AI VTO suggestion error:", error);
    const message = error instanceof Error ? error.message : "فشل اقتراح VTO";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
