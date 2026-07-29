import { PatientTable } from "@/components/patients/PatientTable";

export default function PatientsPage() {
  return (
    <div className="space-y-5 page-content">
      <div className="flex items-center gap-3">
        <div className="w-1 h-8 rounded-full" style={{ background: "#3d7ab5" }} />
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: "#0d2137" }}>المرضى</h1>
          <p className="text-sm mt-0.5" style={{ color: "#94a3b8" }}>قائمة مرضى المركز</p>
        </div>
      </div>
      <PatientTable />
    </div>
  );
}
