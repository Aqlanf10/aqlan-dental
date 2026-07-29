"use client";

import { Smile } from "lucide-react";
import { PhotoAnalysisShell } from "@/components/ceph/PhotoAnalysisShell";
import { FrontalPhotoAnalyzer } from "@/components/ceph/FrontalPhotoAnalyzer";

export default function FrontalPhotoPage() {
  return (
    <PhotoAnalysisShell
      viewType="frontal"
      title="تحليل الصورة الأمامية (النسب والتناظر)"
      icon={<Smile className="w-6 h-6 text-clinic-blue" />}
      uploadLabel="ارفع صورة وجه أمامية (JPG/PNG/WEBP)"
      renderAnalyzer={({ imageUrl, initialPoints, onChange }) => (
        <FrontalPhotoAnalyzer imageUrl={imageUrl} initialPoints={initialPoints} onChange={onChange} />
      )}
    />
  );
}
