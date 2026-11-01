import { environment } from '../../../environments/environment';

export interface ParentPanelLinks { login: string; register: string; }

/**
 * V6b (decision 01.11.2026): the parent panel lives on the coaching site (`/coaching-portal/parent`). Parents log in there
 * (the login sends a Parent straight to the panel) or register at `/auth/register/parent`. Both URLs follow the configured
 * coaching-site login URL so a staging build never points at production.
 */
export function parentPanelLinks(loginUrl: string = environment.centralAdminLoginUrl): ParentPanelLinks {
  return { login: loginUrl, register: new URL('/auth/register/parent', loginUrl).href };
}
