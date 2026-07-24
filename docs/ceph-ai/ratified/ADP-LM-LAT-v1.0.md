# ADP-LM-LAT-v1.0 Ratified Lateral Landmark Definitions

Status: Approved for blinded Pilot annotation
Clinical manual release: `ADP-LM-LAT-v1.0-PILOT`
Approval date: 2026-07-16

This is the registered, human-readable index for the approved PDF in this directory. The PDF is the controlling clinical document for placement, contour, visibility, common-error, and measurement guidance. This registration does not rename or reinterpret historic `ADP-LM-LAT-v1` analyses.

## Coordinate and image convention

- Lateral cephalogram, patient facing right.
- Preserve original aspect ratio and source-pixel coordinates.
- Use one verified `mmPerPixel` calibration snapshot for both reviewers.
- Apply the approved receptor-side or constructed-point rule; never average doubled contours automatically.
- Use `Not visible` when the anatomy cannot be placed defensibly.

## Core Pilot landmarks

| Key | Ratified definition summary |
|---|---|
| `S` | Geometric center of the radiographic outline of the sella turcica, not a point on one wall. |
| `N` | Most anterior point of the frontonasal suture in the midsagittal profile. |
| `Or` | Lowest point on the inferior orbital rim used to construct the Frankfort plane. |
| `Po` | Highest point on the anatomical external auditory meatus; the ear-rod center is not Porion. |
| `ANS` | Most anterior bony tip of the anterior nasal spine at the inferior nasal aperture. |
| `PNS` | Most posterior tip of the hard palate/posterior nasal spine. |
| `A` | Deepest point of the anterior maxillary concavity between ANS and the upper-incisor alveolar crest. |
| `B` | Deepest point of the anterior mandibular alveolar concavity between the lower-incisor alveolus and Pogonion. |
| `Pog` | Most anterior point on the external bony cortex of the mandibular symphysis. |
| `Gn` | Constructed chin point located with the bisector of the facial and mandibular-plane directions on the external chin cortex; not a simple coordinate average. |
| `Me` | Most inferior point of the bony mandibular symphysis. |
| `Go` | Constructed mandibular-angle point using the bisector of tangents to the posterior ramus and inferior mandibular body. |
| `Co` | Most posterosuperior point of the condylar head; when both are visible, use the condyle farther from the film consistently. |
| `Ar` | Constructed radiographic intersection of the posterior ramus/condylar contour and inferior cranial-base contour. |
| `D` | Point on the labial symphyseal contour approximately midway between B and the bony chin region; not an internal geometric center. |
| `Pm` | Point where the anterior symphyseal contour changes from concave to convex above Pogonion. |
| `U1T` | Incisal-edge tip of the selected maxillary central incisor. |
| `U1A` | Root apex of the same maxillary central incisor used for U1T. |
| `L1T` | Incisal-edge tip of the selected mandibular central incisor. |
| `L1A` | Root apex of the same mandibular central incisor used for L1T. |
| `LS` | Most anterior point on the upper-lip vermilion profile. |
| `LI` | Most anterior point on the lower-lip vermilion profile. |
| `Pn` | Most anterior point of the nasal tip on the soft-tissue profile. |
| `Cm` | Anterior-inferior tangent point on the columella used with Pn and LS for the nasolabial construction; not Subnasale. |

The first Pilot completeness gate requires exactly these 24 keys:

`S, N, Or, Po, ANS, PNS, A, B, Pog, Gn, Me, Go, Co, Ar, D, Pm, U1T, U1A, L1T, L1A, LS, LI, Pn, Cm`

## Optional landmarks

`SPog`, `U6`, and `L6` remain optional and do not count toward the first Pilot completeness gate.

## Evidence boundary

Ratification approves the definitions and blinded annotation protocol. It does not establish the accuracy of Aqlan AI, WebCeph, or any other model. WebCeph exports are comparator evidence only and never become Gold Standard automatically.

## Pilot foundation runtime contract

- Production must set `CEPH_PILOT_STORAGE_PATH` to a private, persistent server volume that is not exposed by static-file middleware.
- Images are decoded and re-encoded as PNG before storage; the sanitized bytes are the deduplication authority.
- The Pilot API never accepts or links a clinical `PatientId`. `PatientGroupToken` is a caller-generated, salted SHA-256 grouping token and is never returned to reviewers.
- Official account-owned WebCeph landmark-table and measurement-report exports may be staged only by an administrator. They remain hidden from reviewers and are comparator evidence only.
- PR-1 creates the foundation only. It does not browse, download, import, annotate, adjudicate, train, or migrate live cases automatically.
