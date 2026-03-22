---
description: "Generate a spec-kit compatible specification from system docs."
---
Read all .md files in doc/. Generate a spec-kit compatible specification with proper YAML frontmatter and structure.

## Output Format

```
---
title: [Clear, concise feature/change title - max 60 chars]
stage: Exploring
assignees: []
labels: []
---

# [Resource/Feature Name] Specification

## Goals
What are we trying to achieve?

## Problem Statement
What problem does this solve? Reference system docs if applicable.

## Context
Relevant system architecture, environments, infrastructure.
From doc/*.md: environments (DEV, TE1, TE2, PROD), FQDNs, service accounts, RBAC patterns.

## Proposed Solution
Implementation approach. Use exact FQDNs and accounts from system docs.

## Acceptance Criteria
- [ ] Implemented and tested in DEV
- [ ] Integrated and validated in TE1
- [ ] User acceptance tested in TE2
- [ ] Deployed to PROD
- [ ] [Specific testable criteria]

## Security & Access
From system docs: relevant RBAC, service accounts, credential policies, access restrictions.

## Implementation Details
Technical steps, using documented naming conventions and FQDNs.

## Open Questions
- [ ] [Gap 1 from system docs]
- [ ] [Gap 2]
```

## Generation Rules
1. **Title:** Keep under 60 characters, use imperative voice ("Implement...", "Add...", "Fix...")
2. **Stage:** Start with "Exploring" (progresses to "Considering" → "Accepted")
3. **Assignees/Labels:** Leave empty (assign in GitHub UI)
4. **Context:** Always include environment progression (DEV → TE1 → TE2 → PROD)
5. **Proposed Solution:** Use **exact** FQDNs from documentation (e.g., `prod-app01.internal.example.local`)
6. **Security:** Use **exact** service account names from documentation
7. **Open Questions:** Flag anything unclear or missing from doc/*.md

Save as `doc/spec-kit-spec.md`.

## Next Steps
```powershell
# Validate with spec-kit CLI
spec-kit doc/spec-kit-spec.md --output doc/spec-validated.md

# Create GitHub issue
gh issue create --title "[Title]" --body-file doc/spec-validated.md
```
