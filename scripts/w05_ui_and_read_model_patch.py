from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file_path = Path(path)
    text = file_path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}: {old[:120]!r}")
    file_path.write_text(text.replace(old, new, 1), encoding="utf-8")


# ── Canonical clinic clock on the read side + real event timestamps ──────────
journey_path = Path(
    "backend/src/AqlanDentalPro.Infrastructure/Services/PatientJourneyService.cs"
)
journey = journey_path.read_text(encoding="utf-8")
old_ctor = """public class PatientJourneyService(
    AppDbContext db,
    IPatientAccessService patientAccessService,
    IFinanceReadService financeReadService,
    ILogger<PatientJourneyService> logger)
{"""
new_ctor = """public class PatientJourneyService(
    AppDbContext db,
    IPatientAccessService patientAccessService,
    IFinanceReadService financeReadService,
    IClinicClock clinicClock,
    ILogger<PatientJourneyService> logger)
{
    public PatientJourneyService(
        AppDbContext db,
        IPatientAccessService patientAccessService,
        IFinanceReadService financeReadService,
        ILogger<PatientJourneyService> logger)
        : this(db, patientAccessService, financeReadService, new ClinicClock(), logger)
    {
    }"""
if journey.count(old_ctor) != 1:
    raise SystemExit("PatientJourneyService constructor mismatch")
journey = journey.replace(old_ctor, new_ctor, 1)
journey = journey.replace("ClinicTimeProvider.ClinicToday()", "clinicClock.Today()")

appointment_fields = """                    AppointmentId = (Guid?)a.Id,
                    PatientId = a.PatientId,"""
appointment_fields_new = """                    AppointmentId = (Guid?)a.Id,
                    AppointmentDate = (string?)a.AppointmentDate.ToString("yyyy-MM-dd"),
                    ArrivedAt = a.ArrivedAt,
                    QueueAddedAt = queueItem?.CreatedAt,
                    VisitStartedAt = visit?.CreatedAt,
                    PatientId = a.PatientId,"""
if journey.count(appointment_fields) != 1:
    raise SystemExit("Appointment journey fields mismatch")
journey = journey.replace(appointment_fields, appointment_fields_new, 1)

walkin_fields = """                        AppointmentId = (Guid?)null,
                        PatientId = q.PatientId,"""
walkin_fields_new = """                        AppointmentId = (Guid?)null,
                        AppointmentDate = (string?)null,
                        ArrivedAt = (DateTime?)null,
                        QueueAddedAt = (DateTime?)q.CreatedAt,
                        VisitStartedAt = (DateTime?)visit?.CreatedAt,
                        PatientId = q.PatientId,"""
if journey.count(walkin_fields) != 1:
    raise SystemExit("Walk-in journey fields mismatch")
journey = journey.replace(walkin_fields, walkin_fields_new, 1)
journey_path.write_text(journey, encoding="utf-8")

# ── Shared frontend type exposes business date and event timestamps ──────────
types_path = Path("frontend/src/components/shared/journey/types.ts")
types = types_path.read_text(encoding="utf-8")
type_marker = """  appointmentId: string | null; // null for walk-in patients without an appointment
  patientId: string;"""
type_replacement = """  appointmentId: string | null; // null for walk-in patients without an appointment
  appointmentDate?: string | null;
  arrivedAt?: string | null;
  queueAddedAt?: string | null;
  visitStartedAt?: string | null;
  patientId: string;"""
if types.count(type_marker) != 1:
    raise SystemExit("TodayJourneyItem marker mismatch")
types_path.write_text(types.replace(type_marker, type_replacement, 1), encoding="utf-8")

# ── Mutation hooks carry the explicit override payload ───────────────────────
hooks_path = Path("frontend/src/app/(dashboard)/daily-operations/_lib/hooks.ts")
hooks = hooks_path.read_text(encoding="utf-8")
mutation_marker = "// ─── Mutations ───────────────────────────────────────────────────────────────\n"
mutation_types = """// ─── Mutations ───────────────────────────────────────────────────────────────

export type FutureAppointmentOverrideBody = {
  overrideFutureAppointment?: boolean;
  overrideReason?: string;
};
"""
if hooks.count(mutation_marker) != 1:
    raise SystemExit("Hooks mutation marker mismatch")
hooks = hooks.replace(mutation_marker, mutation_types, 1)

old_intake_body = """      body: { serviceId?: string; chiefComplaint?: string; notes?: string; roomId?: string };
"""
new_intake_body = """      body: FutureAppointmentOverrideBody & {
        serviceId?: string;
        chiefComplaint?: string;
        notes?: string;
        roomId?: string;
      };
"""
if hooks.count(old_intake_body) != 1:
    raise SystemExit("useIntake body mismatch")
hooks = hooks.replace(old_intake_body, new_intake_body, 1)

old_queue_sig = """    mutationFn: async (params: { appointmentId: string; body?: { roomId?: string; notes?: string } }) => {"""
new_queue_sig = """    mutationFn: async (params: {
      appointmentId: string;
      body?: FutureAppointmentOverrideBody & { roomId?: string; notes?: string };
    }) => {"""
if hooks.count(old_queue_sig) != 1:
    raise SystemExit("useSendToQueue signature mismatch")
hooks = hooks.replace(old_queue_sig, new_queue_sig, 1)

old_start = """export function useStartVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (appointmentId: string) => {
      const { data } = await api.post(`/api/patient-journey/${appointmentId}/start-visit`);
      return data;
    },"""
new_start = """export function useStartVisit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: {
      appointmentId: string;
      body?: FutureAppointmentOverrideBody;
    }) => {
      const { data } = await api.post(
        `/api/patient-journey/${params.appointmentId}/start-visit`,
        params.body ?? {},
      );
      return data;
    },"""
if hooks.count(old_start) != 1:
    raise SystemExit("useStartVisit implementation mismatch")
hooks = hooks.replace(old_start, new_start, 1)
hooks_path.write_text(hooks, encoding="utf-8")

# ── Daily Operations: detect backend code, prompt admin for reason, retry ─────
page_path = Path("frontend/src/app/(dashboard)/daily-operations/page.tsx")
page = page_path.read_text(encoding="utf-8")

old_modal_import = """  DirectPaymentModal,
  OverrideDialog,
} from "./_components/Modals";"""
new_modal_import = """  DirectPaymentModal,
  OverrideDialog,
  FutureAppointmentOverrideDialog,
  type FutureAppointmentOverrideOperation,
} from "./_components/Modals";"""
if page.count(old_modal_import) != 1:
    raise SystemExit("Page modal import mismatch")
page = page.replace(old_modal_import, new_modal_import, 1)

animation_end = """.animate-panel-slide { animation: panelSlideIn 0.25s ease-out; }
`;
"""
helpers = """.animate-panel-slide { animation: panelSlideIn 0.25s ease-out; }
`;

function journeyBusinessDateErrorCode(error: unknown): string | undefined {
  if (!error || typeof error !== "object" || !("response" in error)) return undefined;
  return (error as { response?: { data?: { code?: string } } }).response?.data?.code;
}

function isFutureAppointmentBlock(error: unknown): boolean {
  return journeyBusinessDateErrorCode(error) === "future_appointment";
}
"""
if page.count(animation_end) != 1:
    raise SystemExit("Page helper insertion mismatch")
page = page.replace(animation_end, helpers, 1)

state_marker = """  const [overrideOpen, setOverrideOpen] = useState(false);
  const [pendingOverrideAction, setPendingOverrideAction] = useState<{
    type: "Intake" | "SendToQueue";
    item: TodayJourneyItem;
    overdueAmount: number;
  } | null>(null);"""
state_replacement = """  const [overrideOpen, setOverrideOpen] = useState(false);
  const [pendingOverrideAction, setPendingOverrideAction] = useState<{
    type: "Intake" | "SendToQueue";
    item: TodayJourneyItem;
    overdueAmount: number;
  } | null>(null);
  const [futureOverrideOpen, setFutureOverrideOpen] = useState(false);
  const [pendingFutureOverride, setPendingFutureOverride] = useState<{
    type: FutureAppointmentOverrideOperation;
    item: TodayJourneyItem;
    body?: { notes?: string; roomId?: string };
  } | null>(null);"""
if page.count(state_marker) != 1:
    raise SystemExit("Page override state mismatch")
page = page.replace(state_marker, state_replacement, 1)

handler_marker = """  // ── Action handlers ──
  const triggerIntakeOrQueue = useCallback(async (item: TodayJourneyItem, actionType: "Intake" | "SendToQueue", overrideManager?: string) => {"""
handler_insert = """  // ── Action handlers ──
  const openFutureOverrideIfAllowed = useCallback((
    error: unknown,
    type: FutureAppointmentOverrideOperation,
    item: TodayJourneyItem,
    body?: { notes?: string; roomId?: string },
  ) => {
    if (!isFutureAppointmentBlock(error)) return false;
    if (userRole !== "Admin") return false;

    setPendingFutureOverride({ type, item, body });
    setFutureOverrideOpen(true);
    return true;
  }, [userRole]);

  const triggerIntakeOrQueue = useCallback(async (item: TodayJourneyItem, actionType: "Intake" | "SendToQueue", overrideManager?: string) => {"""
if page.count(handler_marker) != 1:
    raise SystemExit("Page action handler marker mismatch")
page = page.replace(handler_marker, handler_insert, 1)

old_action_block = """      const notesSuffix = overrideManager ? `[تجاوز متأخرات: بواسطة المدير ${overrideManager}]` : "";

      if (actionType === "Intake") {
        if (!item.appointmentId) { toast.error("لا يمكن تسجيل الوصول بدون موعد"); return; }
        intakeMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تم تسجيل وصول المريض"), onError: (err) => toast.error(extractErrorMessage(err, "فشل تسجيل الوصول")) }
        );
      } else {
        if (!item.appointmentId) { toast.error("لا يمكن إضافة المريض للانتظار بدون موعد"); return; }
        sendToQueueMutation.mutate(
          { appointmentId: item.appointmentId, body: notesSuffix ? { notes: notesSuffix } : {} },
          { onSuccess: () => toast.success("تمت إضافة المريض للانتظار"), onError: (err) => toast.error(extractErrorMessage(err, "فشل إضافة المريض للانتظار")) }
        );
      }
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إتمام العملية"));
    }
  }, [intakeMutation, sendToQueueMutation]);"""
new_action_block = """      const notesSuffix = overrideManager ? `[تجاوز متأخرات: بواسطة المدير ${overrideManager}]` : "";
      const actionBody = notesSuffix ? { notes: notesSuffix } : {};

      if (actionType === "Intake") {
        if (!item.appointmentId) { toast.error("لا يمكن تسجيل الوصول بدون موعد"); return; }
        intakeMutation.mutate(
          { appointmentId: item.appointmentId, body: actionBody },
          {
            onSuccess: () => toast.success("تم تسجيل وصول المريض"),
            onError: (err) => {
              if (openFutureOverrideIfAllowed(err, "Intake", item, actionBody)) return;
              toast.error(extractErrorMessage(err, "فشل تسجيل الوصول"));
            },
          },
        );
      } else {
        if (!item.appointmentId) { toast.error("لا يمكن إضافة المريض للانتظار بدون موعد"); return; }
        sendToQueueMutation.mutate(
          { appointmentId: item.appointmentId, body: actionBody },
          {
            onSuccess: () => toast.success("تمت إضافة المريض للانتظار"),
            onError: (err) => {
              if (openFutureOverrideIfAllowed(err, "SendToQueue", item, actionBody)) return;
              toast.error(extractErrorMessage(err, "فشل إضافة المريض للانتظار"));
            },
          },
        );
      }
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل إتمام العملية"));
    }
  }, [intakeMutation, sendToQueueMutation, openFutureOverrideIfAllowed]);"""
if page.count(old_action_block) != 1:
    raise SystemExit("Page intake/queue action block mismatch")
page = page.replace(old_action_block, new_action_block, 1)

old_start_handler = """  const handleStartVisit = useCallback((item: TodayJourneyItem) => {
    if (!item.appointmentId) {
      toast.error("لا يمكن بدء الزيارة دون موعد مرتبط");
      return;
    }
    startVisitMutation.mutate(
      item.appointmentId,
      {
        onSuccess: () => toast.success("تم بدء الزيارة بنجاح"),
        onError: (err) => toast.error(extractErrorMessage(err, "فشل بدء الزيارة")),
      },
    );
  }, [startVisitMutation]);"""
new_start_handler = """  const handleStartVisit = useCallback((item: TodayJourneyItem) => {
    if (!item.appointmentId) {
      toast.error("لا يمكن بدء الزيارة دون موعد مرتبط");
      return;
    }
    startVisitMutation.mutate(
      { appointmentId: item.appointmentId },
      {
        onSuccess: () => toast.success("تم بدء الزيارة بنجاح"),
        onError: (err) => {
          if (openFutureOverrideIfAllowed(err, "StartVisit", item)) return;
          toast.error(extractErrorMessage(err, "فشل بدء الزيارة"));
        },
      },
    );
  }, [startVisitMutation, openFutureOverrideIfAllowed]);

  const handleFutureOverrideConfirm = useCallback((reason: string) => {
    if (!pendingFutureOverride?.item.appointmentId) return;

    const { type, item, body } = pendingFutureOverride;
    const overrideBody = {
      ...body,
      overrideFutureAppointment: true,
      overrideReason: reason,
    };
    const callbacks = {
      onSuccess: () => {
        setFutureOverrideOpen(false);
        setPendingFutureOverride(null);
        toast.success("تم تنفيذ الإجراء وتسجيل التجاوز الإداري");
      },
      onError: (error: unknown) =>
        toast.error(extractErrorMessage(error, "فشل تنفيذ التجاوز الإداري")),
    };

    if (type === "Intake") {
      intakeMutation.mutate(
        { appointmentId: item.appointmentId, body: overrideBody },
        callbacks,
      );
    } else if (type === "SendToQueue") {
      sendToQueueMutation.mutate(
        { appointmentId: item.appointmentId, body: overrideBody },
        callbacks,
      );
    } else {
      startVisitMutation.mutate(
        { appointmentId: item.appointmentId, body: overrideBody },
        callbacks,
      );
    }
  }, [pendingFutureOverride, intakeMutation, sendToQueueMutation, startVisitMutation]);"""
if page.count(old_start_handler) != 1:
    raise SystemExit("Page start visit handler mismatch")
page = page.replace(old_start_handler, new_start_handler, 1)

modal_marker = """      <OverrideDialog
        open={overrideOpen}
        onClose={() => setOverrideOpen(false)}
        patientName={pendingOverrideAction?.item?.patientName ?? ""}
        overdueAmount={pendingOverrideAction?.overdueAmount ?? 0}
        onConfirm={handleOverrideConfirm}
      />
"""
modal_replacement = """      <OverrideDialog
        open={overrideOpen}
        onClose={() => setOverrideOpen(false)}
        patientName={pendingOverrideAction?.item?.patientName ?? ""}
        overdueAmount={pendingOverrideAction?.overdueAmount ?? 0}
        onConfirm={handleOverrideConfirm}
      />

      <FutureAppointmentOverrideDialog
        open={futureOverrideOpen}
        onClose={() => {
          if (intakeMutation.isPending || sendToQueueMutation.isPending || startVisitMutation.isPending) return;
          setFutureOverrideOpen(false);
          setPendingFutureOverride(null);
        }}
        patientName={pendingFutureOverride?.item.patientName ?? ""}
        appointmentDate={pendingFutureOverride?.item.appointmentDate ?? undefined}
        operation={pendingFutureOverride?.type ?? "Intake"}
        isPending={intakeMutation.isPending || sendToQueueMutation.isPending || startVisitMutation.isPending}
        onConfirm={handleFutureOverrideConfirm}
      />
"""
if page.count(modal_marker) != 1:
    raise SystemExit("Page override modal render mismatch")
page = page.replace(modal_marker, modal_replacement, 1)
page_path.write_text(page, encoding="utf-8")
