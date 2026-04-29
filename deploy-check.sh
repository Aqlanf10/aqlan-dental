#!/bin/bash
# ============================================================
# Aqlan Dental Pro — Deployment Script
# This script helps verify the project is ready for deployment
# ============================================================

set -e

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  أقلان دنتال برو — فحص جاهزية النشر"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

ERRORS=0
WARNINGS=0

# ─── Backend Checks ───
echo "── فحص الباك اند (.NET 8) ──"

if [ -f "backend/Dockerfile" ]; then
    echo "  ✅ Dockerfile موجود"
else
    echo "  ❌ Dockerfile غير موجود"
    ERRORS=$((ERRORS + 1))
fi

if [ -f "backend/railway.toml" ]; then
    echo "  ✅ railway.toml موجود"
else
    echo "  ❌ railway.toml غير موجود"
    ERRORS=$((ERRORS + 1))
fi

if [ -f "backend/AqlanDentalPro.sln" ]; then
    echo "  ✅ ملف الحل موجود"
else
    echo "  ❌ ملف الحل غير موجود"
    ERRORS=$((ERRORS + 1))
fi

# Check backend builds
echo "  ⏳ فحص بناء الباك اند..."
cd backend
if dotnet build --verbosity quiet 2>/dev/null; then
    echo "  ✅ الباك اند يبني بنجاح"
else
    echo "  ❌ فشل بناء الباك اند"
    ERRORS=$((ERRORS + 1))
fi
cd ..

echo ""

# ─── Frontend Checks ───
echo "── فحص الواجهة الأمامية (Next.js) ──"

if [ -f "frontend/vercel.json" ]; then
    echo "  ✅ vercel.json موجود"
else
    echo "  ❌ vercel.json غير موجود"
    ERRORS=$((ERRORS + 1))
fi

if [ -f "frontend/package.json" ]; then
    echo "  ✅ package.json موجود"
else
    echo "  ❌ package.json غير موجود"
    ERRORS=$((ERRORS + 1))
fi

if [ -f "frontend/next.config.ts" ]; then
    echo "  ✅ next.config.ts موجود"
else
    echo "  ❌ next.config.ts غير موجود"
    ERRORS=$((ERRORS + 1))
fi

# Check node_modules
if [ -d "frontend/node_modules" ]; then
    echo "  ✅ node_modules موجود"
else
    echo "  ⚠️  node_modules غير موجود (شغّل npm install)"
    WARNINGS=$((WARNINGS + 1))
fi

# Check frontend builds
echo "  ⏳ فحص بناء الواجهة..."
cd frontend
if npm run build 2>/dev/null; then
    echo "  ✅ الواجهة تبني بنجاح"
else
    echo "  ⚠️  فشل بناء الواجهة (قد يحتاج npm install أولاً)"
    WARNINGS=$((WARNINGS + 1))
fi
cd ..

echo ""

# ─── Environment Checks ───
echo "── فحص متغيرات البيئة ──"

if [ -f ".env.production" ]; then
    echo "  ✅ .env.production موجود (قالب المرجع)"
else
    echo "  ⚠️  .env.production غير موجود"
    WARNINGS=$((WARNINGS + 1))
fi

if [ -f ".env.example" ]; then
    echo "  ✅ .env.example موجود"
else
    echo "  ⚠️  .env.example غير موجود"
    WARNINGS=$((WARNINGS + 1))
fi

echo ""

# ─── Summary ───
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  النتيجة: ${ERRORS} أخطاء | ${WARNINGS} تحذيرات"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ $ERRORS -eq 0 ]; then
    echo ""
    echo "  🎉 المشروع جاهز للنشر!"
    echo ""
    echo "  الخطوات التالية:"
    echo "  1. أنشئ مشروعاً جديداً على Railway وأضف خدمات PostgreSQL + Redis"
    echo "  2. اربط مستودع GitHub ونشر الباك اند من مجلد backend/"
    echo "  3. أضف متغيرات البيئة المطلوبة في Railway (انظر .env.production)"
    echo "  4. أنشئ مشروعاً جديداً على Vercel واربط مستودع GitHub"
    echo "  5. حدد المجلد الجذري كـ frontend/"
    echo "  6. أضف BACKEND_URL كمتغير بيئة في Vercel"
    echo ""
else
    echo ""
    echo "  ❌ يرجى إصلاح الأخطاء قبل النشر"
    echo ""
    exit 1
fi
