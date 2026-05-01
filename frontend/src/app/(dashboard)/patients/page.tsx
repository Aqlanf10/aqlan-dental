import { PatientTable } from "@/components/patients/PatientTable";

export default function PatientsPage() {
  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <div className="w-1 h-8 rounded-full bg-clinic-blue" />
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المرضى</h1>
          <p className="text-sm text-gray-500 mt-0.5">قائمة مرضى المركز</p>
        </div>
      </div>
      <PatientTable />
    </div>
  );
}
