import { PatientTable } from "@/components/patients/PatientTable";

export default function PatientsPage() {
  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">المرضى</h1>
        <p className="text-sm text-gray-500 mt-1">قائمة مرضى المركز</p>
      </div>
      <PatientTable />
    </div>
  );
}
