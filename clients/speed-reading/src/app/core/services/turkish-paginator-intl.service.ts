import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

@Injectable()
export class TurkishPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'Sayfa başına öğe:';
  override nextPageLabel = 'Sonraki sayfa';
  override previousPageLabel = 'Önceki sayfa';
  override firstPageLabel = 'İlk sayfa';
  override lastPageLabel = 'Son sayfa';

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0 || pageSize === 0) {
      return `0 / ${length}`;
    }
    length = Math.max(length, 0);
    const startIndex = page * pageSize;
    const endIndex = startIndex < length ?
      Math.min(startIndex + pageSize, length) :
      startIndex + pageSize;
    return `${startIndex + 1} - ${endIndex} / ${length}`;
  };
}
