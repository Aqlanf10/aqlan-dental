from pathlib import Path

path = Path("frontend/src/app/(dashboard)/daily-operations/page.tsx")
text = path.read_text(encoding="utf-8")
old = """  const handleFutureOverrideConfirm = useCallback((reason: string) => {
    if (!pendingFutureOverride?.item.appointmentId) return;

    const { type, item, body } = pendingFutureOverride;
    const overrideBody = {"""
new = """  const handleFutureOverrideConfirm = useCallback((reason: string) => {
    const appointmentId = pendingFutureOverride?.item.appointmentId;
    if (!pendingFutureOverride || !appointmentId) return;

    const { type, item, body } = pendingFutureOverride;
    const overrideBody = {"""
if text.count(old) != 1:
    raise SystemExit("future override narrowing marker mismatch")
text = text.replace(old, new, 1)
text = text.replace("{ appointmentId: item.appointmentId, body: overrideBody }", "{ appointmentId, body: overrideBody }")
path.write_text(text, encoding="utf-8")
