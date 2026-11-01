import { environment } from '../../../environments/environment';

export interface ParentPanelLinks { login: string; register: string; }

/**
 * V6b (decision 01.11.2026): the parent panel lives on the coaching site (`/coaching-portal/parent`). Parents log in there
 * (the login sends a Parent straight to the panel) or register at `/auth/register/parent`. The coaching site is
 * onuraltintas.net (eduivme.com is the management panel), the same origin the parent e-mails link to.
 */
export function parentPanelLinks(siteUrl: string = environment.coachingSiteUrl): ParentPanelLinks {
  return { login: new URL('/auth/login', siteUrl).href, register: new URL('/auth/register/parent', siteUrl).href };
}
