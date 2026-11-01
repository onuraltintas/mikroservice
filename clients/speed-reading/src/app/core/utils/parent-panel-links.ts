export interface ParentPanelLinks { login: string; register: string; }

export function parentPanelLinks(_loginUrl?: string): ParentPanelLinks {
  return { login: '', register: '' };
}
