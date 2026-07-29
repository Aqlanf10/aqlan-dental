
import { UserSquare2 } from "lucide-react";
import { PhotoAnalysisShell } from "@/components/ceph/PhotoAnalysisShell";
import { ProfilePhotoAnalyzer } from "@/components/ceph/ProfilePhotoAnalyzer";

export default function ProfilePhotoPage() {
  return (
    <PhotoAnalysisShell
      viewType="profile"
      title="تحليل صورة البروفايل (الأنسجة الرخوة)"
      icon={<UserSquare2 className="w-6 h-6 text-clinic-blue" />}
      uploadLabel="ارفع صورة بروفايل جانبية (JPG/PNG/WEBP)"
      renderAnalyzer={({ imageUrl, initialPoints, onChange }) => (
        <ProfilePhotoAnalyzer imageUrl={imageUrl} initialPoints={initialPoints} onChange={onChange} />
      )}
    />
  );
}
