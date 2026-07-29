# Cephalometric AI Data Governance

Policy ID: `ADP-CEPH-DATA-v1`
Status: mandatory before dataset creation, training, or external validation

## Core rules

1. Clinical records are not training data by default.
2. A doctor correction does not automatically authorize training use.
3. No patient image, identifier, WebCeph export, or derived coordinate file enters Git, issue attachments, CI artifacts, analytics, or general application logs.
4. Every dataset item must have documented legal basis/consent, permitted purposes, retention, geography, and responsible owner.
5. WebCeph output may be used as a functional comparator only. It may not be used as training labels unless the applicable contract/license explicitly permits it and approval is recorded.
6. Data are split by patient, never by image, and all longitudinal records of one patient stay together.

## Roles and separation of duties

| Role | Responsibility |
|---|---|
| Data Controller/Clinic Owner | Approves lawful purpose, notices/consent, retention, and processors |
| Clinical Data Steward | Verifies eligibility, de-identification, linkage, and release manifest |
| Orthodontist Reviewer | Annotates/adjudicates under a frozen definition version |
| ML Engineer | Receives only approved, de-identified dataset versions; cannot self-approve release |
| Validation Lead | Locks test sets, controls unblinding, signs metric report |
| Security/Privacy Reviewer | Reviews storage, access, transfer, deletion, and incident controls |
| Release Approver | Authorizes shadow/canary/production based on complete evidence |

## Intake gate

Before copying any image into a dataset workspace, record:

- source system/site and data owner;
- lawful basis or consent version and allowed purposes;
- whether commercial model development and external processing are permitted;
- date range, inclusion/exclusion criteria, and expected strata;
- retention/deletion obligation and withdrawal mechanism;
- license for third-party images, annotations, code, and model weights;
- ethics/IRB approval where required by jurisdiction or study design.

If any item is unknown, quarantine the candidate record and do not train on it.

## De-identification

- Remove names, record numbers, dates of birth, contact details, addresses, account IDs, embedded filenames, burned-in annotations, QR/barcodes, and private URLs.
- For DICOM, apply an approved profile to headers and private tags; retain only fields explicitly required for calibration/device strata and transform dates consistently.
- Inspect pixels for burned-in identifiers. Automated detection must be followed by sampled human QA.
- Replace linkage with salted, access-controlled patient group IDs. Store the re-identification key outside the ML environment under clinic control.
- Hash images for integrity and duplicate detection. A hash is still controlled metadata and is not published.
- Record de-identification tool/version, operator, timestamp, findings, and QA result.

## Storage and access

- Use encrypted, access-controlled clinical/ML storage; never developer laptops or source-control history as the system of record.
- Apply least privilege, MFA, short-lived credentials, audit logging, environment separation, and approved regional transfer controls.
- Training workers receive immutable read-only dataset versions. Test labels remain inaccessible to training/tuning roles until authorized evaluation.
- Backups, caches, notebooks, experiment trackers, and temporary exports follow the same classification and deletion policy.

## Dataset version manifest

Every immutable version records:

- dataset ID/version, creation time, owner, purpose, and approval IDs;
- parent version and exact added/removed case hashes;
- patient-level split manifest and duplicate/leakage check result;
- source/site/device/quality/age/skeletal distributions;
- consent/legal/license summary and prohibited uses;
- de-identification version and QA result;
- landmark-definition and annotation-tool versions;
- reviewer/adjudication coverage and agreement statistics;
- known limitations, exclusions, retention date, and withdrawal tombstones;
- manifest digest and storage object digests.

Changing one item creates a new version. Training and validation reports reference the exact digest, not `latest`.

## Annotation and correction workflow

1. Doctor correction is stored in the clinical record for care.
2. A separate eligibility job proposes only records with valid data-use authority.
3. Data steward performs de-identification and quality review.
4. Two independent orthodontists annotate; a third adjudicates disagreements according to `ADP-CEPH-VAL-v1`.
5. Dataset review approves or rejects each case and creates an immutable dataset version.
6. Only that version can enter a documented training experiment.

Clinical edits never flow directly into continuous training. High-frequency correction patterns may be monitored in aggregate to prioritize review, but the underlying records still pass the full gate.

## Partition and leakage controls

- Group by patient before randomization/stratification.
- Detect exact and near duplicates, re-exports, resized copies, and longitudinal views.
- Keep external sites/devices out of training and validation when designated for external testing.
- Prohibit benchmark test images and their labels from prompt examples, model selection, threshold tuning, or post-hoc preprocessing changes.
- Re-run leakage checks for every dataset version and store the signed result.

## Retention, withdrawal, and deletion

- Retention follows the narrowest applicable consent, law, contract, and clinical-record obligation.
- Withdrawal creates a tombstone and removes the item from future versions/experiments. Whether an already trained model must be retrained is decided and documented by legal/privacy and clinical owners.
- Deletion propagates to derivatives, caches, notebooks, experiment artifacts, and scheduled backups according to policy, with an auditable completion record.
- Published aggregate metrics remain only if re-identification risk and consent terms permit.

## Third-party controls

- Record processor, destination, purpose, fields, retention, and contract before uploading any clinical data.
- General AI APIs are not approved for patient images merely because an API key exists. Provider terms, data retention/training behavior, region, security, and health-data agreements must be approved.
- WebCeph partner API access requires the official agreement/key and supported plan. The [official API contract](https://doc.webceph.com/en/api/) covers patient/record/image interoperability and must not be treated as permission to scrape sessions or obtain undocumented landmark/diagnostic data.
- Open datasets/models require documented provenance and commercial-use-compatible licenses; research-only assets stay outside production.

## Incidents and audit

Stop affected processing, preserve audit evidence, notify the designated privacy/security owner, assess required notifications, rotate exposed credentials, and document corrective action. Quarterly audits cover access, stale exports, retention, dataset manifests, split leakage, license changes, and production model lineage.

## Release gate

Training or deployment is blocked unless all are true:

- approved immutable dataset and definition versions exist;
- de-identification and leakage checks pass;
- licenses and provider contracts permit the intended use;
- the validation lead can reproduce metrics from hashes;
- external clinical validation and clinical sign-off are complete for the claimed use;
- model registry, monitoring, rollback, and incident owners are assigned.
