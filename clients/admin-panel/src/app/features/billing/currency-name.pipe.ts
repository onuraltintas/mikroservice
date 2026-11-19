import { Pipe, PipeTransform } from '@angular/core';

/** Live test 19.11.2026 (C1): amounts read "749,00 TL", as students see them, not the ISO code "TRY". */
export function currencyName(code: string | null | undefined): string {
  return !code || code.toUpperCase() === 'TRY' ? 'TL' : code;
}

@Pipe({ name: 'currencyName', standalone: true })
export class CurrencyNamePipe implements PipeTransform {
  transform(code: string | null | undefined): string { return currencyName(code); }
}
