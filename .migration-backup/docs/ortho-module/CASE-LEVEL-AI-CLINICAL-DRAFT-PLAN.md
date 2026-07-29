# Case-Level Orthodontic Clinical Draft

Goal: add a draft-only assistant for the orthodontic wizard that works at full case level, not ceph only.

Supported sections:
- problems
- diagnosis
- objectives
- strategies
- plan
- mechanotherapy

Safety rules:
- draft only
- no automatic save
- no automatic approval
- doctor review required
- missing data must be reported clearly

First implementation target:
- add backend contract and service skeleton
- keep current wizard working
- integrate frontend only after backend CI is green
