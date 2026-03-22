![](./media/image1.png)

- 1 Test

1.  2 Test

![](./media/image2.png)

Architecture

# **Environment Configuration Overview**

## **1. Purpose of Environments**

This document describes the different environments used within the system to support development, testing, validation, and production deployment.

The purpose of separating environments is to:

- Ensure **safe development and testing** without impacting production systems

- Enable **controlled validation** of features before release

- Provide **stability and reliability** in production

- Support **different access levels and security constraints**

## **2. Environment Overview**

The system is divided into four main environments:

  -------------------------------------------------------------------------
  Environment   Purpose
  ------------- -----------------------------------------------------------
  DEV           Development and local testing

  TE1           First test environment (integration testing)

  TE2           Second test environment (UAT / pre-production validation)

  PROD          Live production environment
  -------------------------------------------------------------------------

## **3. Environment Details**

### **3.1 DEV (Development Environment)**

**FQDN:**\
dev-app01.internal.example.local

**Purpose:**

- Used by developers for implementing and testing new features

- Allows debugging and frequent deployments

- Data may be synthetic or non-critical

**Accounts:**

- dev_user_app

- dev_user_db

- dev_admin

### **3.2 TE1 (Test Environment 1)**

**FQDN:**\
te1-app01.internal.example.local

**Purpose:**

- First-level integration testing

- Automated test execution (CI/CD pipelines)

- Basic validation of functionality

**Accounts:**

- te1_user_app

- te1_user_db

- te1_service_account

### **3.3 TE2 (Test Environment 2)**

**FQDN:**\
te2-app01.internal.example.local

**Purpose:**

- User Acceptance Testing (UAT)

- Final validation before production release

- Simulates production-like conditions

**Accounts:**

- te2_user_app

- te2_user_uat

- te2_service_account

- te2_readonly_user

### **3.4 PROD (Production Environment)**

**FQDN:**\
prod-app01.internal.example.local

**Purpose:**

- Live system used by end users

- High availability and performance critical

- Strict access control and monitoring

**Accounts:**

- prod_user_app

- prod_user_readonly

- prod_admin

- prod_service_account

## **4. Access & Security Notes**

- Access to environments is restricted based on **role-based access control (RBAC)**

- Production access is limited to authorized personnel only

- Service accounts are used for automated processes and integrations

- All credentials must be stored securely and never hardcoded

## **5. Naming Conventions**

- All environments follow the format:\
  \<env\>-\<service\>-\<instance\>.\<domain\>

- Example:

  - dev-app01.internal.example.local

  - te1-db01.internal.example.local

## **6. Summary**

This environment structure ensures:

- Clear separation between development, testing, and production

- Reduced risk of production issues

- Controlled deployment lifecycle

- Secure and maintainable system architecture
