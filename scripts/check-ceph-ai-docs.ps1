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
    'CEPH_AI_BASELINE_REPORT.md'
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

Write-Output "Ceph-AI documentation contract passed: $($requiredFiles.Count) documents, $($landmarkKeys.Count) landmarks."
