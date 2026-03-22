---
description: "Generate a GitHub specification from system documentation."
---
Read all .md files in doc/. These files describe the real system architecture, environments, and infrastructure.

Generate a GitHub-ready specification based **only** on documented facts. Output is suitable for a GitHub issue or repository specification.

## Specification Structure
1. **Title** — clear, descriptive
2. **Problem Statement** — what needs to be implemented or changed
3. **Context** — relevant architecture, environments, and infrastructure from the docs
4. **Proposed Solution** — implementation approach aligned with documented system design
5. **Environment Details** — which environments (DEV, TE1, TE2, PROD) are affected
6. **Service Accounts & RBAC** — relevant accounts and access patterns from the docs
7. **Acceptance Criteria** — testable checkboxes
8. **Technical Approach** — step-by-step implementation using documented FQDNs and naming conventions
9. **Security Considerations** — based on documented RBAC, credential policies, and access restrictions
10. **Open Questions** — anything unclear or missing from the documentation

Save as doc/spec.md.
