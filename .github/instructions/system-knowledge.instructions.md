---
applyTo: "**"
---
The `doc/` folder contains source-of-truth system documentation (automatically converted from `.docx`):
- **Environments:** DEV, TE1, TE2, PROD with FQDNs, purposes, service accounts
- **Infrastructure:** Naming conventions, domain structure, account patterns
- **Authentication:** RBAC, role assignments, credential storage policies
- **Architecture:** Deployment topology, environment progression (DEV → TE1 → TE2 → PROD)

When generating code, configs, specs, architecture recommendations, or docs for this system:
1. Always read relevant `doc/*.md` files first
2. Align all outputs with the documented architecture
3. Use exact FQDNs and service account names from the docs
4. Follow documented naming conventions and RBAC patterns
5. Do not invent system details — flag gaps as open questions

This ensures all generated artifacts (SDDs, specs, configs, code) match the real system.
