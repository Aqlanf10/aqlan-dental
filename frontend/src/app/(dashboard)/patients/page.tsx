import { PatientTable } from "@/components/patients/PatientTable";

export default function PatientsPage() {
  return (
    <div className="space-y-6" dir="rtl">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold" style={{ color: "#0d2137" }}>
            المرضى
          </h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            قائمة مرضى المركز
          </p>
        </div>
      </div>

      {/* Patient Table Card */}
      <PatientTable />
    </div>
  );
}
