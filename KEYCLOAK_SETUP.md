# Keycloak Setup Guide

This guide explains how to configure Google Login, Email Verification, and Password Reset flows in your local Keycloak instance.

## 1. Access Admin Console
- URL: http://localhost:8080/admin
- Username: `admin`
- Password: (check `.env` file, default `admin` or see docker-compose)

## 2. Email Configuration (SMTP)
To enable "Forgot Password" and "Email Verification", Keycloak needs an SMTP server. We use MailPit locally.

Since Keycloak and MailPit are running in the same Docker network (`eduplatform-network`), use the container name.

1.  Select your realm (`eduplatform`).
2.  Go to **Realm Settings** > **Email**.
3.  Fill in the details:
    - **Host:** `mailpit`
    - **Port:** `1025`
    - **From:** `noreply@eduplatform.com`
    - **Authentication:** Disabled
4.  Click **Test Connection**. You should see a success message.

## 3. Enable Forgot Password & Email Verification
1.  Go to **Realm Settings** > **Login**.
2.  Toggle **Forgot password** to **ON**.
3.  Toggle **Verify email** to **ON** (if you want mandatory email verification).
4.  Click **Save**.

Now, the login page will show a "Forgot Password?" link.

## 4. Google Login (Social Identity Provider)
You need a Google Cloud Project to get Client ID and Secret.

### A. Google Cloud Console Setup
1.  Go to [Google Cloud Console](https://console.cloud.google.com/).
2.  Create a new project.
3.  Go to **APIs & Services** > **Credentials**.
4.  Create Credentials > **OAuth Client ID**.
5.  Application Type: **Web application**.
6.  **Authorized Javascript Origins:** `http://localhost:8080`
7.  **Authorized Redirect URIs:** `http://localhost:8080/realms/eduplatform/broker/google/endpoint`
8.  Copy the **Client ID** and **Client Secret**.

### B. Keycloak Configuration
1.  In Keycloak Admin Console, go to **Identity Providers**.
2.  Click **Google**.
3.  Paste the **Client ID** and **Client Secret**.
4.  Click **Add**.

Now, the login page will show a "Sign in with Google" button.

## 5. Post-Login Handling
When a user registers via Google, Keycloak creates a user. However, your Identity Service DB (Users table) won't know about this user automatically unless:
1.  You implement a **Webhook** (Keycloak Event Listener) that calls your Identity API.
2.  OR: Your Frontend app, after login, calls an endpoint `POST /api/auth/sync-profile` to ensure the user exists in your DB.

**Current Recommendation:** Use the Sync Profile approach on the frontend (Onboarding/Splash screen).
