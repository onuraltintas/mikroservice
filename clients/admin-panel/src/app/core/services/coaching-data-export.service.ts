import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Roadmap O12: the signed-in user's Coaching data (KVKK) as a downloadable JSON file. */
@Injectable({ providedIn: 'root' })
export class CoachingDataExportService {
  private readonly http = inject(HttpClient);
  download() { return this.http.get(`${environment.apiUrl}/data-privacy/export`, { responseType: 'blob' }); }
}

export function saveCoachingExport(blob: Blob, today = new Date()): void {
  const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(today);
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url; link.download = `kocluk-verilerim-${day}.json`; link.rel = 'noopener';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
