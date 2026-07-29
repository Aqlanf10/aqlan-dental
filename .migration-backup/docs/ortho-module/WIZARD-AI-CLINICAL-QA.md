# Ortho Wizard AI — Clinical QA and Safety Contract

## Purpose

The AI button inside the orthodontic case wizard is an assistive draft tool only. It helps the doctor start writing clinical text, but it must never become an automatic diagnosis, treatment plan, report section, or PowerPoint content without doctor review.

Current implementation after PR #402:

- Frontend only integration inside `OrthoCaseWizard.tsx`.
- Uses the existing cephalometric draft endpoint:
  - `POST /api/ceph/{latestCephAnalysisId}/ai/draft-diagnosis`
- Requires a saved cephalometric analysis for the orthodontic case.
- Inserts AI output only into the local draft text area.
- Does not save to the database.
- Does not approve diagnosis or treatment plan.
- Does not write directly to the report or generated PowerPoint.
- Does not expose any AI API key in the frontend.

## Hard clinical safety rules

1. The AI output is always a draft.
2. The doctor must review and edit the text before using it.
3. The doctor must copy the accepted text into the official linked editor.
4. The official editor remains the source of truth for reports and PowerPoint.
5. The AI must not invent missing measurements, photos, cast values, Bolton values, or treatment decisions.
6. If no ceph analysis exists, the AI button must remain disabled.
7. If AI is disabled in settings or API key is missing, the UI must show the honest backend error.
8. No patient identifiers should be sent to the AI provider.
9. No AI output should be silently saved.
10. No AI output should bypass the existing diagnosis/treatment approval workflow.

## Manual QA checklist

### A. Case without ceph analysis

1. Open `/ortho/{caseId}` for a case with no saved cephalometric analysis.
2. Open tab: `المعالج`.
3. Open these steps:
   - قائمة المشاكل
   - التشخيص
   - أهداف العلاج
   - استراتيجيات العلاج
   - خطة العلاج
   - الميكانيكا العلاجية
4. Confirm:
   - the manual draft box appears.
   - the AI button is disabled.
   - a clear note says AI requires a saved ceph analysis.
   - the copy button still works.
   - the linked editor button still works.

### B. Case with ceph analysis but AI disabled

1. Open a case with at least one cephalometric analysis.
2. Ensure AI is disabled in admin settings.
3. Click `اقتراح AI` from a draft panel.
4. Confirm:
   - no text is saved automatically.
   - the backend error appears clearly, for example: `مساعد الذكاء الاصطناعي معطل من الإعدادات`.
   - the manual draft remains editable.

### C. Case with ceph analysis and AI configured

1. Open a case with a saved cephalometric analysis.
2. Ensure AI settings are enabled and a provider key is configured.
3. Open a clinical draft panel.
4. Click `اقتراح AI`.
5. Confirm:
   - the button shows a loading state.
   - generated text appears only in the local draft box.
   - the text includes or is accompanied by the review disclaimer.
   - no diagnosis is approved automatically.
   - no treatment plan is approved automatically.
   - PowerPoint generation does not consume this draft unless the doctor copies and saves it in the official editor.

### D. Copy and official approval path

1. Generate or write a draft.
2. Press `نسخ`.
3. Open the linked official editor.
4. Paste the text.
5. Edit and approve manually.
6. Confirm that only approved official data affects reports and final presentations.

## Known current limitation

The current AI draft source is cephalometric-first. It is useful for diagnosis/objective/planning drafts, but it is not yet a full multimodal orthodontic case assistant. It does not yet combine all of the following into one dedicated prompt:

- clinical exam
- intraoral/extraoral photo analysis
- cast/model analysis
- Bolton analysis
- problem list
- treatment stages
- visits/progress
- retention

## Next expansion target

The next major AI sprint should introduce a dedicated case-level endpoint, for example:

`POST /api/ortho-cases/{id}/ai/clinical-draft`

Suggested request body:

```json
{
  "section": "problems | diagnosis | objectives | strategies | plan | mechanotherapy"
}
```

Suggested behavior:

- Aggregate existing case data server-side.
- Exclude patient identifiers from the AI prompt.
- Return structured JSON with:
  - draft
  - evidenceUsed
  - missingData
  - warnings
  - disclaimer
  - modelId
  - generatedAt
- Never save automatically.
- Audit every attempt.
- Reuse the existing AI provider infrastructure and settings.

## Clinical acceptance criteria for future full AI

The feature is clinically acceptable only when it can:

1. Say what evidence it used.
2. Say what data is missing.
3. Refuse to invent information.
4. Separate skeletal, dental, soft tissue, and functional findings.
5. Separate diagnosis from treatment objectives.
6. Present treatment alternatives rather than one unsupported plan.
7. Keep doctor approval mandatory.
