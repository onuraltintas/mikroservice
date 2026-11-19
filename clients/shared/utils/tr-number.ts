/**
 * Live test 19.11.2026 (C1): Turkish number formatting that does not depend on the runtime's ICU data. A server
 * render without Turkish locale data printed "₺0.00"; these always give "0,00 TL", "1.234,5" and "%86".
 */
export function trNumber(value: number | null | undefined, maximumDecimals = 0, minimumDecimals = 0): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  const factor = 10 ** maximumDecimals;
  const rounded = Math.round((Math.abs(value) + Number.EPSILON) * factor) / factor;
  let [whole, fraction = ''] = rounded.toFixed(maximumDecimals).split('.');
  fraction = fraction.replace(/0+$/, '').padEnd(minimumDecimals, '0');
  whole = whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  const sign = value < 0 && (Number(whole.replace(/\./g, '')) > 0 || Number(fraction) > 0) ? '−' : '';
  return sign + whole + (fraction ? ',' + fraction : '');
}

/** "749,00 TL" */
export function trMoney(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? '—' : `${trNumber(value, 2, 2)} TL`;
}

/** "%86" or "%87,5" (the sign comes first in Turkish). */
export function trPercent(value: number | null | undefined, maximumDecimals = 0): string {
  return value === null || value === undefined || !Number.isFinite(value) ? '—' : `%${trNumber(value, maximumDecimals)}`;
}
