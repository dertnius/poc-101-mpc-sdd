---
description: "Generate deployment configurations from system documentation."
---
Read all .md files in doc/. These files document the real system environments, FQDNs, and service accounts.

Generate deployment configuration files for all environments (DEV, TE1, TE2, PROD) based **only** on documented facts.

## Configuration Generation Rules
1. Use exact FQDNs from the docs (e.g., `dev-app01.internal.example.local`)
2. Use exact service account names for each environment
3. Follow the documented naming convention: `<env>-<service>-<instance>.<domain>`
4. Include RBAC and access control rules as documented
5. Replicate environment-specific details (TE2 should mirror PROD; DEV can be relaxed)
6. Do not invent new FQDNs, accounts, or services

## Output Formats
Generate in one or more of:
- YAML (Kubernetes manifests, infrastructure-as-code)
- JSON (config files, environment variables)
- HCL (Terraform)
- Shell scripts (provisioning)

## Environments to Configure
- **DEV** — development relaxed settings
- **TE1** — integration testing with CI/CD
- **TE2** — UAT, mirrors production conditions
- **PROD** — strict access, high availability, monitoring

Save outputs as:
- `doc/config-dev.yaml`, `doc/config-te1.yaml`, `doc/config-te2.yaml`, `doc/config-prod.yaml`
- Or as a single parameterized `doc/deployment-config.yaml`
