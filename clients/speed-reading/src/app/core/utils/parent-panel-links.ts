import { environment } from '../../../environments/environment';

export interface ParentPanelLinks { login: string; register: string; }

/**
 * V6b (decision 01.11.2026): the parent panel lives on the coaching site (`/coaching-portal/parent`). Parents log in there
 * (the login sends a Parent straight to the panel) or register at `/auth/register/parent`. Since the alan adı planı
 * (18.11.2026) parents have their own address, veli.maxkoc.com, the same origin the parent e-mails link to.
 */
export function parentPanelLinks(siteUrl: string = environment.parentSiteUrl): ParentPanelLinks {
  return { login: new URL('/auth/login', siteUrl).href, register: new URL('/auth/register/parent', siteUrl).href };
}
