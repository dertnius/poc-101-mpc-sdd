---
title: Implement Payments API - Get Payment State by ID
stage: Exploring
assignees: []
labels: []
---

# Payments API Specification

## Goals
- Provide a reliable API endpoint to retrieve payment state information by payment ID
- Enable clients to query transaction status across all environments
- Ensure secure, RBAC-controlled access to payment data

## Problem Statement
The system lacks a standardized way to retrieve payment state information. Clients need a dedicated API endpoint that accepts a `paymentId` and returns detailed payment state data with proper access controls across all environments (DEV, TE1, TE2, PROD).

## Context
**System Environments** (from doc/*.md):
- `dev-payment-api01.internal.example.local` (DEV - development)
- `te1-payment-api01.internal.example.local` (TE1 - integration testing)
- `te2-payment-api01.internal.example.local` (TE2 - UAT, mirrors PROD)
- `prod-payment-api01.internal.example.local` (PROD - production)

**Service Accounts** (RBAC):
- DEV: `dev_payment_app`, `dev_user_app`
- TE1: `te1_payment_app`, `te1_service_account`
- TE2: `te2_payment_app`, `te2_payment_service_account`, `te2_payment_readonly`
- PROD: `prod_payment_app`, `prod_payment_service_account`, `prod_payment_readonly`, `prod_payment_admin`

**Access Pattern**: Read-only access for auditing required in TE2 and PROD.

## Proposed Solution
Implement a `GET /api/v1/payments/{paymentId}` endpoint that:
1. Accepts a `paymentId` path parameter
2. Returns payment state object with: `id`, `status`, `amount`, `currency`, `timestamp`, `metadata`
3. Authenticates via service account credentials (per environment)
4. Enforces RBAC: `payment_readonly` can query; `payment_app` can create/update
5. Deployed across all 4 environments using naming convention
6. Includes request/response validation and error handling

**API Response Example:**
```json
{
  "id": "paymentId",
  "status": "completed|pending|failed|refunded",
  "amount": 100.00,
  "currency": "USD",
  "timestamp": "2026-03-22T10:00:00Z",
  "metadata": {...}
}