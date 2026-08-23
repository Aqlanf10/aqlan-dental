import { useSession } from "@/auth/SessionProvider";
import { FormField, SelectList } from "@/components/forms";
import { Card, PrimaryButton, Screen, SectionTitle, StateMessage } from "@/components/ui";
import { apiRequest } from "@/lib/api";
import {
  SURGEON_REVIEW_DECISIONS,
  VTO_DISCLAIMER_AR,
  orthoSurgicalStatusLabel,
  type JointPlan,
  type OrthoSurgicalCaseDetail,
  type OrthoSurgicalComment,
  type OrthoSurgicalReadiness,
  type OrthoSurgicalStatus,
  type OrthoSurgicalVto,
  type SurgeryExecutionSummary
} from "@/lib/orthognathic";
import { colors, radius, spacing } from "@/theme";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View
} from "react-native";

type ListResponse<T> = { data: T[] };

type JointPlanForm = {
  procedureType: string;
  timing: string;
  orthodonticObjectives: string;
  surgicalObjectives: string;
  preSurgicalRequirements: string;
  postSurgicalPlan: string;
  risks: string;
  patientExplanation: string;
};

type ReviewForm = {
  decision: string | null;
  proposedProcedure: string;
  requiredRecords: string;
  risks: string;
  notes: string;
};

type VtoForm = {
  maxillaMoveMm: string;
  mandibleMoveMm: string;
  chinMoveMm: string;
  rotationDegree: string;
  notes: string;
};

const EMPTY_PLAN: JointPlanForm = {
  procedureType: "",
  timing: "",
  orthodonticObjectives: "",
  surgicalObjectives: "",
  preSurgicalRequirements: "",
  postSurgicalPlan: "",
  risks: "",
  patientExplanation: ""
};

const EMPTY_REVIEW: ReviewForm = {
  decision: null,
  proposedProcedure: "",
  requiredRecords: "",
  risks: "",
  notes: ""
};

const EMPTY_VTO: VtoForm = {
  maxillaMoveMm: "",
  mandibleMoveMm: "",
  chinMoveMm: "",
  rotationDegree: "",
  notes: ""
};

export default function OrthoSurgicalCaseScreen() {
  const { user, can } = useSession();
  const params = useLocalSearchParams<{ id: string }>();
  const id = first(params.id);
  const role = user?.role.toLowerCase() ?? "";
  const canView = can("ortho_surgical.view");
  const canEdit = can("ortho_surgical.edit");
  const canApprove = can("ortho_surgical.approve");
  const isOrthodontist = role === "admin" || role === "orthodontist";
  const isSurgeon = role === "admin" || role === "oralsurgeon";

  const [item, setItem] = useState<OrthoSurgicalCaseDetail | null>(null);
  const [readiness, setReadiness] = useState<OrthoSurgicalReadiness | null>(null);
  const [comments, setComments] = useState<OrthoSurgicalComment[]>([]);
  const [vtos, setVtos] = useState<OrthoSurgicalVto[]>([]);
  const [surgery, setSurgery] = useState<SurgeryExecutionSummary | null>(null);
  const [plan, setPlan] = useState<JointPlanForm>(EMPTY_PLAN);
  const [review, setReview] = useState<ReviewForm>(EMPTY_REVIEW);
  const [vto, setVto] = useState<VtoForm>(EMPTY_VTO);
  const [comment, setComment] = useState("");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sectionWarning, setSectionWarning] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id || !canView) {
      setLoading(false);
      return;
    }
    setError(null);
    setSectionWarning(null);
    const results = await Promise.allSettled([
      apiRequest<OrthoSurgicalCaseDetail>(`/api/ortho-surgical-cases/${id}`),
      apiRequest<OrthoSurgicalReadiness>(`/api/ortho-surgical-cases/${id}/readiness`),
      apiRequest<ListResponse<OrthoSurgicalComment>>(`/api/ortho-surgical-cases/${id}/comments`),
      apiRequest<ListResponse<OrthoSurgicalVto>>(`/api/ortho-surgical-cases/${id}/vto`),
      apiRequest<SurgeryExecutionSummary>(`/api/ortho-surgical-cases/${id}/surgery-summary`)
    ]);

    const [detailResult, readinessResult, commentsResult, vtosResult, surgeryResult] = results;
    if (detailResult.status === "rejected") {
      setItem(null);
      setError(errorMessage(detailResult.reason, "تعذر تحميل الخطة الجراحية التقويمية"));
      setLoading(false);
      return;
    }

    setItem(detailResult.value);
    if (readinessResult.status === "fulfilled") setReadiness(readinessResult.value);
    if (commentsResult.status === "fulfilled") setComments(commentsResult.value.data ?? []);
    if (vtosResult.status === "fulfilled") setVtos(vtosResult.value.data ?? []);
    if (surgeryResult.status === "fulfilled") setSurgery(surgeryResult.value);

    const optionalFailures = [readinessResult, commentsResult, vtosResult, surgeryResult]
      .filter((result) => result.status === "rejected").length;
    if (optionalFailures > 0) {
      setSectionWarning(`تعذر تحديث ${optionalFailures} من أقسام الخطة. اسحب للأسفل لإعادة المحاولة.`);
    }
    setLoading(false);
  }, [canView, id]);

  useFocusEffect(useCallback(() => {
    setLoading(true);
    void load();
  }, [load]));

  useEffect(() => {
    if (!item) return;
    setPlan(planFrom(item.jointPlan));
    setReview(reviewFrom(item.surgeonReview));
  }, [item]);

  async function refresh() {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  }

  async function runAction(key: string, action: () => Promise<unknown>) {
    if (busy) return;
    setBusy(key);
    setError(null);
    try {
      await action();
      await load();
    } catch (err) {
      setError(errorMessage(err, "تعذر تنفيذ الإجراء"));
    } finally {
      setBusy(null);
    }
  }

  async function updateStatus(status: OrthoSurgicalStatus) {
    await runAction(`status-${status}`, () => apiRequest(`/api/ortho-surgical-cases/${id}/status`, {
      method: "PUT",
      body: JSON.stringify({ status })
    }));
  }

  async function saveJointPlan() {
    await runAction("plan", () => apiRequest(`/api/ortho-surgical-cases/${id}/joint-plan`, {
      method: "PUT",
      body: JSON.stringify(nullableFields(plan))
    }));
  }

  async function saveReview() {
    if (!review.decision) {
      setError("اختر قرار مراجعة الجراح أولًا.");
      return;
    }
    await runAction("review", () => apiRequest(`/api/ortho-surgical-cases/${id}/surgeon-review`, {
      method: "POST",
      body: JSON.stringify({ ...nullableFields(review), decision: review.decision })
    }));
  }

  async function addComment() {
    const body = comment.trim();
    if (!body) return;
    await runAction("comment", async () => {
      await apiRequest(`/api/ortho-surgical-cases/${id}/comments`, {
        method: "POST",
        body: JSON.stringify({ body })
      });
      setComment("");
    });
  }

  async function createVto() {
    const payload = {
      maxillaMoveMm: optionalNumber(vto.maxillaMoveMm),
      mandibleMoveMm: optionalNumber(vto.mandibleMoveMm),
      chinMoveMm: optionalNumber(vto.chinMoveMm),
      rotationDegree: optionalNumber(vto.rotationDegree),
      notes: vto.notes.trim() || null
    };
    if ([payload.maxillaMoveMm, payload.mandibleMoveMm, payload.chinMoveMm, payload.rotationDegree].every((value) => value === null)) {
      setError("أدخل حركة واحدة على الأقل لإنشاء سيناريو VTO.");
      return;
    }
    await runAction("vto", async () => {
      await apiRequest(`/api/ortho-surgical-cases/${id}/vto`, {
        method: "POST",
        body: JSON.stringify(payload)
      });
      setVto(EMPTY_VTO);
    });
  }

  if (!canView) {
    return <Screen><StateMessage title="غير مصرح" message="حسابك لا يملك صلاحية عرض التخطيط الجراحي التقويمي." /></Screen>;
  }

  if (loading && !item) {
    return <Screen><ActivityIndicator size="large" color={colors.primary} /></Screen>;
  }

  if (!item) {
    return (
      <Screen>
        <StateMessage title="تعذر فتح الخطة" message={error ?? "الخطة غير موجودة"} action={<PrimaryButton title="إعادة المحاولة" onPress={() => void load()} />} />
      </Screen>
    );
  }

  const planLocked = Boolean(item.jointPlan?.lockedAt);
  const canSend = canEdit && isOrthodontist && item.allowedTransitions.includes("SentToSurgeon");
  const canApproveNow = item.status === "SurgeonReviewPending" || item.status === "JointPlanApproved";

  return (
    <Screen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refresh()} />}>
      <View>
        <Text style={styles.title}>{item.caseNumber}</Text>
        <Text style={styles.subtitle}>{item.patientName} · {item.patientNumber}</Text>
      </View>

      {error ? <StateMessage title="تنبيه" message={error} /> : null}
      {sectionWarning ? <StateMessage title="بعض البيانات لم تتحدث" message={sectionWarning} /> : null}

      <Card>
        <View style={styles.header}>
          <Text style={styles.status}>{item.statusLabel || orthoSurgicalStatusLabel(item.status)}</Text>
          <View style={{ flex: 1 }}>
            <Text style={styles.itemTitle}>التخطيط المشترك بين التقويم والجراحة</Text>
            <Text style={styles.meta}>المسؤول الآن: {item.responsibleParty || "—"}</Text>
          </View>
        </View>
        <Row label="أخصائي التقويم" value={item.orthodontistName || "—"} />
        <Row label="الجراح" value={item.surgeonName || "غير محدد"} />
        <Row label="اعتماد التقويم" value={item.orthodontistApprovedAt ? "تم الاعتماد" : "بانتظار الاعتماد"} />
        <Row label="اعتماد الجراحة" value={item.surgeonApprovedAt ? "تم الاعتماد" : "بانتظار الاعتماد"} />
        <Row label="تاريخ الفتح" value={dateText(item.createdAt)} last />
        {item.diagnosisSummary ? <Text style={styles.body}>{item.diagnosisSummary}</Text> : null}
      </Card>

      <SectionTitle>جاهزية السجلات</SectionTitle>
      {readiness ? (
        <Card>
          <ReadinessRow label="السجلات الأساسية" ready={readiness.recordsReady} />
          <ReadinessRow label="السيفالو المعتمد" ready={readiness.cephReady} />
          <ReadinessRow label="التشخيص المعتمد" ready={readiness.diagnosisReady} />
          <ReadinessRow label="جاهز لمراجعة الجراح" ready={readiness.surgeonReviewReady} last />
          {readiness.missing.length > 0 ? (
            <View style={styles.missingList}>
              {readiness.missing.map((entry) => <Text key={entry} style={styles.warning}>• {entry}</Text>)}
            </View>
          ) : null}
        </Card>
      ) : <StateMessage title="تعذر تحميل الجاهزية" message="أعد المحاولة بالسحب للأسفل." />}

      {canEdit || canApprove ? <SectionTitle>إجراءات سير العمل</SectionTitle> : null}
      <View style={styles.actions}>
        {canSend ? <ActionButton title="إرسال للجراح" loading={busy === "send"} disabled={busy !== null} onPress={() => void runAction("send", () => apiRequest(`/api/ortho-surgical-cases/${id}/send-to-surgeon`, { method: "POST" }))} /> : null}
        {canApprove && isOrthodontist && canApproveNow && !item.orthodontistApprovedAt ? <ActionButton title="اعتماد التقويم" loading={busy === "approve-ortho"} disabled={busy !== null} onPress={() => void runAction("approve-ortho", () => apiRequest(`/api/ortho-surgical-cases/${id}/approve-orthodontist`, { method: "POST" }))} /> : null}
        {canApprove && isSurgeon && canApproveNow && !item.surgeonApprovedAt ? <ActionButton title="اعتماد الجراحة" loading={busy === "approve-surgeon"} disabled={busy !== null} onPress={() => void runAction("approve-surgeon", () => apiRequest(`/api/ortho-surgical-cases/${id}/approve-surgeon`, { method: "POST" }))} /> : null}
        {canEdit ? item.allowedTransitions.filter((status) => status !== "SentToSurgeon").map((status) => (
          <ActionButton key={status} title={orthoSurgicalStatusLabel(status)} loading={busy === `status-${status}`} disabled={busy !== null} onPress={() => void updateStatus(status)} />
        )) : null}
      </View>

      <SectionTitle>الخطة المشتركة</SectionTitle>
      <Card>
        {planLocked ? (
          <>
            <Text style={styles.success}>الخطة معتمدة ومقفلة ولا يمكن تعديلها.</Text>
            <PlanRows plan={plan} />
          </>
        ) : canEdit ? (
          <View style={styles.form}>
            <FormField label="نوع الإجراء" value={plan.procedureType} onChangeText={(value) => setPlan((current) => ({ ...current, procedureType: value }))} />
            <FormField label="التوقيت المتوقع" value={plan.timing} onChangeText={(value) => setPlan((current) => ({ ...current, timing: value }))} />
            <FormField label="أهداف التقويم" value={plan.orthodonticObjectives} onChangeText={(value) => setPlan((current) => ({ ...current, orthodonticObjectives: value }))} multiline />
            <FormField label="أهداف الجراحة" value={plan.surgicalObjectives} onChangeText={(value) => setPlan((current) => ({ ...current, surgicalObjectives: value }))} multiline />
            <FormField label="متطلبات ما قبل الجراحة" value={plan.preSurgicalRequirements} onChangeText={(value) => setPlan((current) => ({ ...current, preSurgicalRequirements: value }))} multiline />
            <FormField label="خطة ما بعد الجراحة" value={plan.postSurgicalPlan} onChangeText={(value) => setPlan((current) => ({ ...current, postSurgicalPlan: value }))} multiline />
            <FormField label="المخاطر والحدود" value={plan.risks} onChangeText={(value) => setPlan((current) => ({ ...current, risks: value }))} multiline />
            <FormField label="شرح مبسط للمريض" value={plan.patientExplanation} onChangeText={(value) => setPlan((current) => ({ ...current, patientExplanation: value }))} multiline />
            <PrimaryButton title="حفظ الخطة المشتركة" loading={busy === "plan"} disabled={busy !== null} onPress={() => void saveJointPlan()} />
          </View>
        ) : <PlanRows plan={plan} />}
      </Card>

      <SectionTitle>مراجعة الجراح</SectionTitle>
      <Card>
        {canApprove && isSurgeon ? (
          <View style={styles.form}>
            <SelectList label="قرار الجراح" value={review.decision} onChange={(value) => setReview((current) => ({ ...current, decision: value }))} options={[...SURGEON_REVIEW_DECISIONS]} emptyLabel="اختر القرار" />
            <FormField label="الإجراء المقترح" value={review.proposedProcedure} onChangeText={(value) => setReview((current) => ({ ...current, proposedProcedure: value }))} multiline />
            <FormField label="السجلات المطلوبة" value={review.requiredRecords} onChangeText={(value) => setReview((current) => ({ ...current, requiredRecords: value }))} multiline />
            <FormField label="المخاطر" value={review.risks} onChangeText={(value) => setReview((current) => ({ ...current, risks: value }))} multiline />
            <FormField label="ملاحظات الجراح" value={review.notes} onChangeText={(value) => setReview((current) => ({ ...current, notes: value }))} multiline />
            <PrimaryButton title="حفظ مراجعة الجراح" loading={busy === "review"} disabled={busy !== null} onPress={() => void saveReview()} />
          </View>
        ) : item.surgeonReview ? (
          <>
            <Row label="القرار" value={reviewDecisionLabel(item.surgeonReview.decision)} />
            <Row label="الإجراء المقترح" value={item.surgeonReview.proposedProcedure || "—"} />
            <Row label="السجلات المطلوبة" value={item.surgeonReview.requiredRecords || "—"} />
            <Row label="المخاطر" value={item.surgeonReview.risks || "—"} />
            <Row label="الملاحظات" value={item.surgeonReview.notes || "—"} last />
          </>
        ) : <Text style={styles.empty}>لم تُسجل مراجعة الجراح بعد.</Text>}
      </Card>

      <SectionTitle>المحاكاة الجراحية VTO</SectionTitle>
      <StateMessage title="تنبيه سريري إلزامي" message={VTO_DISCLAIMER_AR} />
      {canEdit ? (
        <Card>
          <View style={styles.form}>
            <FormField label="حركة الفك العلوي (mm)" value={vto.maxillaMoveMm} onChangeText={(value) => setVto((current) => ({ ...current, maxillaMoveMm: value }))} keyboardType="numbers-and-punctuation" />
            <FormField label="حركة الفك السفلي (mm)" value={vto.mandibleMoveMm} onChangeText={(value) => setVto((current) => ({ ...current, mandibleMoveMm: value }))} keyboardType="numbers-and-punctuation" />
            <FormField label="حركة الذقن (mm)" value={vto.chinMoveMm} onChangeText={(value) => setVto((current) => ({ ...current, chinMoveMm: value }))} keyboardType="numbers-and-punctuation" />
            <FormField label="الدوران (°)" value={vto.rotationDegree} onChangeText={(value) => setVto((current) => ({ ...current, rotationDegree: value }))} keyboardType="numbers-and-punctuation" />
            <FormField label="ملاحظات السيناريو" value={vto.notes} onChangeText={(value) => setVto((current) => ({ ...current, notes: value }))} multiline maxLength={4000} />
            <PrimaryButton title="إنشاء سيناريو VTO" loading={busy === "vto"} disabled={busy !== null} onPress={() => void createVto()} />
          </View>
        </Card>
      ) : null}
      {vtos.length === 0 ? <StateMessage title="لا توجد سيناريوهات VTO" message={VTO_DISCLAIMER_AR} /> : vtos.map((scenario, index) => (
        <VtoCard
          key={scenario.id}
          scenario={scenario}
          number={vtos.length - index}
          canApprove={canApprove && isOrthodontist && !scenario.isApprovedByOrthodontist}
          loading={busy === `approve-vto-${scenario.id}`}
          disabled={busy !== null}
          onApprove={() => void runAction(`approve-vto-${scenario.id}`, () => apiRequest(`/api/ortho-surgical-cases/${id}/vto/${scenario.id}/approve`, { method: "POST" }))}
        />
      ))}

      <SectionTitle>التنفيذ الجراحي</SectionTitle>
      <Card>
        {surgery?.linked && surgery.id ? (
          <>
            <Row label="رقم الحالة الجراحية" value={surgery.caseNumber || "—"} />
            <Row label="نوع الجراحة" value={surgery.surgeryType || "—"} />
            <Row label="الحالة" value={surgery.status || "—"} />
            <Row label="الجراح" value={surgery.doctorName || "—"} last />
            <PrimaryButton title="فتح سجل الجراحة" onPress={() => router.push({ pathname: "/(app)/surgery-case", params: { id: surgery.id, patientName: item.patientName } })} />
          </>
        ) : item.status === "ReadyForSurgery" && canEdit ? (
          <PrimaryButton title="فتح الحالة الجراحية للتنفيذ" loading={busy === "create-surgery"} disabled={busy !== null} onPress={() => void runAction("create-surgery", () => apiRequest(`/api/ortho-surgical-cases/${id}/create-surgery-case`, { method: "POST", body: "{}" }))} />
        ) : <Text style={styles.empty}>لن يُفتح سجل الجراحة الحقيقي حتى تصبح الخطة Ready for Surgery.</Text>}
      </Card>

      <SectionTitle>مناقشة التقويم والجراحة</SectionTitle>
      {comments.length === 0 ? <StateMessage title="لا توجد تعليقات" message="ابدأ مناقشة الحالة بين أخصائي التقويم والجراح." /> : comments.map((entry) => (
        <Card key={entry.id}>
          <Text style={styles.commentRole}>{roleLabel(entry.authorRole)}</Text>
          <Text style={styles.body}>{entry.body}</Text>
          <Text style={styles.commentDate}>{dateText(entry.createdAt)}</Text>
        </Card>
      ))}
      {canEdit ? (
        <Card>
          <View style={styles.form}>
            <FormField label="تعليق جديد" value={comment} onChangeText={setComment} multiline maxLength={2000} placeholder="ملاحظة لأخصائي التقويم أو الجراحة" />
            <PrimaryButton title="إرسال التعليق" loading={busy === "comment"} disabled={busy !== null || !comment.trim()} onPress={() => void addComment()} />
          </View>
        </Card>
      ) : null}

      <Text style={styles.disclaimer}>هذه مساحة تخطيط مشتركة. القرار الجراحي النهائي يعتمد على مراجعة أخصائي جراحة الفم والفكين وموافقة المريض.</Text>
    </Screen>
  );
}

function ActionButton({ title, onPress, loading, disabled }: { title: string; onPress: () => void; loading: boolean; disabled: boolean }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={title} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.actionButton, disabled && styles.disabled, pressed && !disabled && styles.pressed]}>
      {loading ? <ActivityIndicator color={colors.primary} /> : <Text style={styles.actionText}>{title}</Text>}
    </Pressable>
  );
}

function Row({ label, value, last = false }: { label: string; value: string; last?: boolean }) {
  return <View style={[styles.row, last && styles.lastRow]}><Text style={styles.value}>{value}</Text><Text style={styles.label}>{label}</Text></View>;
}

function ReadinessRow({ label, ready, last = false }: { label: string; ready: boolean; last?: boolean }) {
  return <View style={[styles.row, last && styles.lastRow]}><Text style={ready ? styles.ready : styles.notReady}>{ready ? "جاهز" : "ناقص"}</Text><Text style={styles.label}>{label}</Text></View>;
}

function PlanRows({ plan }: { plan: JointPlanForm }) {
  const rows: Array<[string, string]> = [
    ["نوع الإجراء", plan.procedureType],
    ["التوقيت", plan.timing],
    ["أهداف التقويم", plan.orthodonticObjectives],
    ["أهداف الجراحة", plan.surgicalObjectives],
    ["متطلبات ما قبل الجراحة", plan.preSurgicalRequirements],
    ["خطة ما بعد الجراحة", plan.postSurgicalPlan],
    ["المخاطر", plan.risks],
    ["شرح المريض", plan.patientExplanation]
  ];
  return <>{rows.map(([label, value], index) => <Row key={label} label={label} value={value || "—"} last={index === rows.length - 1} />)}</>;
}

function VtoCard({ scenario, number, canApprove, loading, disabled, onApprove }: { scenario: OrthoSurgicalVto; number: number; canApprove: boolean; loading: boolean; disabled: boolean; onApprove: () => void }) {
  return (
    <Card style={scenario.isApprovedByOrthodontist ? styles.approvedCard : undefined}>
      <Text style={styles.itemTitle}>سيناريو VTO #{number}</Text>
      <Text style={scenario.isApprovedByOrthodontist ? styles.success : styles.meta}>{scenario.isApprovedByOrthodontist ? "معتمد من أخصائي التقويم" : "مسودة غير معتمدة"}</Text>
      <Row label="الفك العلوي" value={movement(scenario.maxillaMoveMm, "mm")} />
      <Row label="الفك السفلي" value={movement(scenario.mandibleMoveMm, "mm")} />
      <Row label="الذقن" value={movement(scenario.chinMoveMm, "mm")} />
      <Row label="الدوران" value={movement(scenario.rotationDegree, "°")} />
      <Row label="SNA متوقع" value={measurement(scenario.predictedSNA)} />
      <Row label="SNB متوقع" value={measurement(scenario.predictedSNB)} />
      <Row label="ANB متوقع" value={measurement(scenario.predictedANB)} />
      <Row label="Wits متوقع" value={measurement(scenario.predictedWits)} />
      <Row label="Overjet متوقع" value={measurement(scenario.predictedOverjet)} last />
      {scenario.notes ? <Text style={styles.body}>{scenario.notes}</Text> : null}
      <Text style={styles.vtoDisclaimer}>{scenario.disclaimer || VTO_DISCLAIMER_AR}</Text>
      {canApprove ? <PrimaryButton title="اعتماد هذا السيناريو" loading={loading} disabled={disabled} onPress={onApprove} /> : null}
    </Card>
  );
}

function first(value?: string | string[]): string { return Array.isArray(value) ? value[0] ?? "" : value ?? ""; }
function errorMessage(value: unknown, fallback: string): string { return value instanceof Error ? value.message : fallback; }
function optionalNumber(value: string): number | null { const parsed = Number(value.trim()); return value.trim() && Number.isFinite(parsed) ? parsed : null; }
function nullableFields<T extends Record<string, unknown>>(value: T): T { return Object.fromEntries(Object.entries(value).map(([key, entry]) => [key, typeof entry === "string" ? entry.trim() || null : entry])) as T; }
function dateText(value?: string | null): string { if (!value) return "—"; const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString("ar-YE"); }
function measurement(value?: number | null): string { return value == null ? "—" : Number(value).toFixed(2); }
function movement(value: number | null | undefined, unit: string): string { return value == null ? "—" : `${Number(value) > 0 ? "+" : ""}${Number(value).toFixed(1)} ${unit}`; }
function roleLabel(role?: string | null): string { if (role === "Orthodontist") return "أخصائي التقويم"; if (role === "OralSurgeon") return "أخصائي الجراحة"; if (role === "Admin") return "الإدارة"; return role || "المستخدم"; }
function reviewDecisionLabel(value: string): string { return SURGEON_REVIEW_DECISIONS.find((entry) => entry.value === value)?.label ?? value; }
function planFrom(value?: JointPlan | null): JointPlanForm { return { procedureType: value?.procedureType ?? "", timing: value?.timing ?? "", orthodonticObjectives: value?.orthodonticObjectives ?? "", surgicalObjectives: value?.surgicalObjectives ?? "", preSurgicalRequirements: value?.preSurgicalRequirements ?? "", postSurgicalPlan: value?.postSurgicalPlan ?? "", risks: value?.risks ?? "", patientExplanation: value?.patientExplanation ?? "" }; }
function reviewFrom(value?: OrthoSurgicalCaseDetail["surgeonReview"]): ReviewForm { return { decision: value?.decision ?? null, proposedProcedure: value?.proposedProcedure ?? "", requiredRecords: value?.requiredRecords ?? "", risks: value?.risks ?? "", notes: value?.notes ?? "" }; }

const styles = StyleSheet.create({
  title: { color: colors.text, fontSize: 25, fontWeight: "800", textAlign: "right" },
  subtitle: { color: colors.primary, marginTop: 4, fontWeight: "700", textAlign: "right" },
  header: { flexDirection: "row", gap: spacing.sm, alignItems: "flex-start", marginBottom: spacing.sm },
  status: { color: colors.primary, backgroundColor: colors.primarySoft, paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: 999, fontSize: 11, fontWeight: "800" },
  itemTitle: { color: colors.text, fontSize: 17, fontWeight: "800", textAlign: "right" },
  meta: { color: colors.muted, marginTop: 4, textAlign: "right" },
  row: { minHeight: 44, flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  lastRow: { borderBottomWidth: 0 },
  label: { color: colors.muted, textAlign: "right", maxWidth: "45%" },
  value: { color: colors.text, flex: 1, textAlign: "right", fontWeight: "600" },
  body: { color: colors.text, lineHeight: 23, textAlign: "right", marginTop: spacing.sm },
  empty: { color: colors.muted, lineHeight: 22, textAlign: "right" },
  warning: { color: colors.warning, lineHeight: 22, textAlign: "right" },
  success: { color: colors.success, fontWeight: "700", textAlign: "right", marginBottom: spacing.sm },
  ready: { color: colors.success, fontWeight: "800" },
  notReady: { color: colors.warning, fontWeight: "800" },
  missingList: { marginTop: spacing.sm, paddingTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.border },
  actions: { gap: spacing.sm },
  actionButton: { minHeight: 46, alignItems: "center", justifyContent: "center", borderWidth: 1, borderColor: colors.primary, backgroundColor: colors.surface, borderRadius: radius.sm, paddingHorizontal: spacing.md },
  actionText: { color: colors.primary, fontWeight: "800", textAlign: "center" },
  disabled: { opacity: 0.55 },
  pressed: { opacity: 0.8 },
  form: { gap: spacing.md },
  approvedCard: { borderColor: colors.success, backgroundColor: colors.successSoft },
  vtoDisclaimer: { color: colors.warning, backgroundColor: colors.warningSoft, borderRadius: radius.sm, padding: spacing.sm, lineHeight: 21, textAlign: "right", marginTop: spacing.sm },
  commentRole: { color: colors.primary, fontWeight: "800", textAlign: "right" },
  commentDate: { color: colors.muted, fontSize: 11, marginTop: spacing.sm, textAlign: "right" },
  disclaimer: { color: colors.warning, backgroundColor: colors.warningSoft, borderRadius: radius.sm, padding: spacing.md, lineHeight: 22, textAlign: "right" }
});
