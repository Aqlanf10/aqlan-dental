$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$docsRoot = Join-Path $repoRoot 'docs/ceph-ai'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)

$requiredFiles = @(
    'WEBCEPH_FUNCTIONAL_BENCHMARK.md',
    'CEPH_AI_GAP_ANALYSIS.md',
    'CEPH_AI_VALIDATION_PROTOCOL.md',
    'CEPH_AI_DATA_GOVERNANCE.md',
    'CEPH_AI_IMPLEMENTATION_ROADMAP.md',
    'CEPH_AI_LANDMARK_DEFINITIONS.md',
    'CEPH_AI_BASELINE_REPORT.md',
    'CEPH_AI_GOLD_STANDARD_TOOLING.md',
    'CEPH_AI_EVALUATION_ENGINE.md',
    'CEPH_AI_MODEL_LINEAGE.md'
)

foreach ($name in $requiredFiles) {
    $path = Join-Path $docsRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing required ceph-AI document: $name"
    }

    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -eq 0) {
        throw "Ceph-AI document is empty: $name"
    }
    [void]$utf8.GetString($bytes)
}

$gap = Get-Content -Raw -Encoding utf8 (Join-Path $docsRoot 'CEPH_AI_GAP_ANALYSIS.md')
$gapHeader = '| Capability | WebCeph Observed | Aqlan Current | Gap | Clinical Risk | Priority | Proposed Fix |'
if (-not $gap.Contains($gapHeader)) {
    throw 'The ceph-AI gap analysis is missing the required capability matrix.'
}

$validation = Get-Content -Raw -Encoding utf8 (Join-Path $docsRoot 'CEPH_AI_VALIDATION_PROTOCOL.md')
foreach ($metric in @('mean radial error', 'SDR at 1.0', 'patient-level', 'confidence intervals', 'repeatability')) {
    if ($validation.IndexOf($metric, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "The validation protocol is missing required concept: $metric"
    }
}

$baseline = Get-Content -Raw -Encoding utf8 (Join-Path $docsRoot 'CEPH_AI_BASELINE_REPORT.md')
if ($baseline.IndexOf('not measured', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw 'The baseline report must explicitly identify unmeasured clinical metrics.'
}

$definitions = Get-Content -Raw -Encoding utf8 (Join-Path $docsRoot 'CEPH_AI_LANDMARK_DEFINITIONS.md')
$landmarkKeys = @(
    'S', 'N', 'Or', 'Po', 'ANS', 'PNS', 'A', 'B', 'Pog', 'Gn', 'Me', 'Go',
    'Co', 'Ar', 'D', 'Pm', 'U1T', 'U1A', 'L1T', 'L1A', 'LS', 'LI', 'Pn',
    'Cm', 'SPog', 'U6', 'L6'
)
foreach ($key in $landmarkKeys) {
    $rowToken = '| `' + $key + '` |'
    if (-not $definitions.Contains($rowToken)) {
        throw "Missing cephalometric landmark definition row: $key"
    }
}

$schemaPath = Join-Path $docsRoot 'schemas/ceph-benchmark-manifest-v1.schema.json'
if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
    throw 'Missing cephalometric benchmark manifest schema.'
}
$schemaText = Get-Content -Raw -Encoding utf8 $schemaPath
[void]($schemaText | ConvertFrom-Json)
foreach ($requiredToken in @(
    '"additionalProperties": false',
    '"patientGroupId"',
    '"imageSha256"',
    '"privateTagsRemoved"',
    '"burnedInIdentifierDetected"',
    '"goldStandard"'
)) {
    if (-not $schemaText.Contains($requiredToken)) {
        throw "The cephalometric benchmark schema is missing required token: $requiredToken"
    }
}
foreach ($prohibitedProperty in @('"patientName"', '"patientId"', '"dateOfBirth"', '"filePath"', '"imageUrl"')) {
    if ($schemaText.IndexOf($prohibitedProperty, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "The cephalometric benchmark schema exposes prohibited property: $prohibitedProperty"
    }
}

$evaluationSchemaPath = Join-Path $docsRoot 'schemas/ceph-landmark-evaluation-v1.schema.json'
if (-not (Test-Path -LiteralPath $evaluationSchemaPath -PathType Leaf)) {
    throw 'Missing cephalometric landmark evaluation schema.'
}
$evaluationSchemaText = Get-Content -Raw -Encoding utf8 $evaluationSchemaPath
[void]($evaluationSchemaText | ConvertFrom-Json)
foreach ($requiredToken in @(
    '"protocolVersion"',
    '"modelVersion"',
    '"preprocessingVersion"',
    '"evaluationSplit"',
    '"bootstrapReplicates"',
    '"predictions"',
    '"NotFound"',
    '"Rejected"'
)) {
    if (-not $evaluationSchemaText.Contains($requiredToken)) {
        throw "The cephalometric evaluation schema is missing required token: $requiredToken"
    }
}

$measurementSchemaPath = Join-Path $docsRoot 'schemas/ceph-measurement-evaluation-v1.schema.json'
if (-not (Test-Path -LiteralPath $measurementSchemaPath -PathType Leaf)) {
    throw 'Missing cephalometric measurement evaluation schema.'
}
$measurementSchemaText = Get-Content -Raw -Encoding utf8 $measurementSchemaPath
[void]($measurementSchemaText | ConvertFrom-Json)
foreach ($requiredToken in @(
    '"ADP-CEPH-MEAS-VAL-v1"',
    '"ADP-CEPH-GEOMETRY-v1"',
    '"toleranceApprovalId"',
    '"tolerancesFrozenBeforeUnblinding"',
    '"candidatePredictions"',
    '"comparatorPredictions"'
)) {
    if (-not $measurementSchemaText.Contains($requiredToken)) {
        throw "The cephalometric measurement schema is missing required token: $requiredToken"
    }
}

$repeatabilitySchemaPath = Join-Path $docsRoot 'schemas/ceph-repeatability-evaluation-v1.schema.json'
if (-not (Test-Path -LiteralPath $repeatabilitySchemaPath -PathType Leaf)) {
    throw 'Missing cephalometric repeatability evaluation schema.'
}
$repeatabilitySchemaText = Get-Content -Raw -Encoding utf8 $repeatabilitySchemaPath
[void]($repeatabilitySchemaText | ConvertFrom-Json)
foreach ($requiredToken in @(
    '"ADP-CEPH-REPEAT-v1"',
    '"ADP-CEPH-GEOMETRY-v1"',
    '"clinicalErrorThresholdMm"',
    '"confidenceThresholds"',
    '"minItems": 3',
    '"maxItems": 3'
)) {
    if (-not $repeatabilitySchemaText.Contains($requiredToken)) {
        throw "The cephalometric repeatability schema is missing required token: $requiredToken"
    }
}

$modelSchemaPath = Join-Path $docsRoot 'schemas/ceph-ai-model-version-v1.schema.json'
if (-not (Test-Path -LiteralPath $modelSchemaPath -PathType Leaf)) {
    throw 'Missing cephalometric AI model-version schema.'
}
$modelSchemaText = Get-Content -Raw -Encoding utf8 $modelSchemaPath
[void]($modelSchemaText | ConvertFrom-Json)
foreach ($requiredToken in @(
    '"additionalProperties": false',
    '"preprocessingVersion"',
    '"datasetVersion"',
    '"landmarkDefinitionVersion"',
    '"artifactSha256"',
    '"^[0-9a-fA-F]{64}$"'
)) {
    if (-not $modelSchemaText.Contains($requiredToken)) {
        throw "The cephalometric model-version schema is missing required token: $requiredToken"
    }
}

Write-Output "Ceph-AI documentation contract passed: $($requiredFiles.Count) documents, $($landmarkKeys.Count) landmarks, and 5 schemas."
