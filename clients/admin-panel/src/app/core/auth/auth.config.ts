import { AuthConfig } from 'angular-oauth2-oidc';
import { environment } from '../../../environments/environment';

// SSR Safe Auth Config
export const authConfig: AuthConfig = {
    issuer: '', // Local service does not provide discovery yet
    loginUrl: `${environment.apiUrl}/auth/login`,
    tokenEndpoint: `${environment.apiUrl}/auth/login`, // We use the same for local simplified flow
    redirectUri: 'http://localhost:4200/dashboard',
    clientId: 'admin-panel',
    responseType: 'code',
    scope: 'openid profile email',
    requireHttps: false,
    showDebugInformation: true,
};
