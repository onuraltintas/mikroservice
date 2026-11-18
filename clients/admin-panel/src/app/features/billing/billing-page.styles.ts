/** Shared look of the "Abonelik ve Ödeme" pages; the same building blocks as the other admin pages. */
export const BILLING_PAGE_STYLES = `
  .data-card, .form-card { border: 1px solid var(--ui-border); border-radius: .75rem; background: var(--ui-surface); padding: 1rem; }
  .form-card h3 { margin-bottom: .75rem; font-weight: 600; color: var(--ui-text); }
  .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
  .form-grid label { display: flex; flex-direction: column; gap: .25rem; font-size: .875rem; color: var(--ui-text); }
  .form-grid .wide { grid-column: 1 / -1; }
  .form-grid .check, .check { flex-direction: row; align-items: center; gap: .5rem; }
  .form-grid .check { padding-top: 1.5rem; }
  .check { display: flex; font-size: .875rem; color: var(--ui-text); }
  input:not([type=checkbox]), select, textarea { min-width: 0; border: 1px solid var(--ui-border-strong); border-radius: .5rem; background: transparent; padding: .5rem .75rem; color: inherit; }
  textarea { min-height: 4rem; }
  .form-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: .5rem; margin-top: 1rem; }
  .primary, .secondary, .actions button { border-radius: .5rem; padding: .5rem .75rem; font-size: .875rem; }
  .primary { background: var(--ui-brand); color: var(--ui-brand-contrast); }
  .primary:disabled, .secondary:disabled, .actions button:disabled { opacity: .5; }
  .secondary, .actions button { border: 1px solid var(--ui-border-strong); color: var(--ui-text); }
  .data-table { width: 100%; text-align: left; font-size: .875rem; }
  .data-table th { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; font-size: .7rem; color: var(--ui-text-muted); }
  .data-table td { border-bottom: 1px solid var(--ui-border); padding: .625rem .75rem; color: var(--ui-text); vertical-align: top; }
  .actions { white-space: nowrap; text-align: right; }
  .actions button + button { margin-left: .35rem; }
  .actions .danger { color: #b91c1c; }
  .muted { color: var(--ui-text-muted); font-size: .8rem; }
  .empty { padding: 2rem; text-align: center; color: var(--ui-text-muted); }
  .badge { display: inline-block; border-radius: 999px; border: 1px solid var(--ui-border-strong); padding: .1rem .5rem; font-size: .75rem; color: var(--ui-text); }
  .badge.on { border-color: #15803d; color: #15803d; }
  .badge.off { color: var(--ui-text-muted); }
  .product-head { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: .75rem; }
  .settings-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; margin-top: 1rem; }
  .settings-row fieldset { border: 1px solid var(--ui-border); border-radius: .5rem; padding: .75rem; }
  .settings-row legend { padding: 0 .25rem; font-size: .8rem; font-weight: 600; color: var(--ui-text); }
  .entitlement-grid { display: grid; gap: .5rem; }
  .entitlement-row { display: grid; grid-template-columns: minmax(0, 1fr) repeat(2, auto) 7rem; align-items: center; gap: .75rem; border: 1px solid var(--ui-border); border-radius: .5rem; padding: .5rem .75rem; font-size: .875rem; color: var(--ui-text); }
  @media (max-width: 640px) {
    .form-grid, .settings-row { grid-template-columns: 1fr; }
    .form-grid .wide { grid-column: auto; }
    .form-grid .check { padding-top: 0; }
    .entitlement-row { grid-template-columns: 1fr; }
    .actions { white-space: normal; text-align: left; }
  }
`;
