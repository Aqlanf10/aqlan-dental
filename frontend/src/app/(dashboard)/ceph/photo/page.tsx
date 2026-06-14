"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { ArrowRight, ImagePlus, Loader2, UserSquare2 } from "lucide-react";
import api from "@/lib/api";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { ProfilePhotoAnalyzer } from "@/components/ceph/ProfilePhotoAnalyzer";

const MAX_BYTES = 10 * 1024 * 1024;
const ACCEPTED = ["image/jpeg", "image/png", "image/webp"];

export default function ProfilePhotoPage() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onPick = async (file: File) => {
    setError(null);
    if (!ACCEPTED.includes(file.type)) {
      setError("نوع الصورة غير مدعوم — استخدم JPG أو PNG أو WEBP.");
      return;
    }
    if (file.size > MAX_BYTES) {
      setError("حجم الصورة يتجاوز 10 ميجابايت.");
      return;
    }
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const { data } = await api.post<{ url: string }>("/api/uploads", form);
      setImageUrl(resolveImageUrl(data.url) || data.url);
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "تعذّر رفع الصورة — حاول مرة أخرى.");
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="space-y-5 max-w-5xl" dir="rtl">
      <div className="no-print flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ceph" className="hover:text-clinic-blue transition">السيفالومتري</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">تحليل صورة البروفايل</span>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href="/ceph"
            className="no-print p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <UserSquare2 className="w-6 h-6 text-clinic-blue" />
            تحليل صورة البروفايل (الأنسجة الرخوة)
          </h1>
        </div>
        {imageUrl && (
          <button
            onClick={() => inputRef.current?.click()}
            className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
            <ImagePlus className="w-3.5 h-3.5" />صورة أخرى
          </button>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        className="hidden"
        onChange={(e) => { const f = e.target.files?.[0]; if (f) onPick(f); e.target.value = ""; }}
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-2.5 text-xs">{error}</div>
      )}

      {!imageUrl ? (
        <button
          onClick={() => inputRef.current?.click()}
          disabled={uploading}
          className="w-full border-2 border-dashed border-gray-300 rounded-xl py-16 flex flex-col items-center gap-3 text-gray-400 hover:border-clinic-blue hover:text-clinic-blue transition disabled:opacity-60">
          {uploading ? <Loader2 className="w-8 h-8 animate-spin" /> : <ImagePlus className="w-8 h-8" />}
          <span className="text-sm font-medium">
            {uploading ? "جارٍ رفع الصورة..." : "ارفع صورة بروفايل جانبية (JPG/PNG/WEBP)"}
          </span>
          <span className="text-[11px] text-gray-300">القياسات زاويّة لا تتطلب معايرة</span>
        </button>
      ) : (
        <ProfilePhotoAnalyzer imageUrl={imageUrl} />
      )}
    </div>
  );
}
