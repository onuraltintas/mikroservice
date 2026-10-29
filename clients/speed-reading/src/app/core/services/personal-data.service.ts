import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

/** KVKK access right: the signed-in user downloads every record Speed Reading keeps about them. */
@Injectable({ providedIn: 'root' })
export class PersonalDataService {
  private readonly http = inject(HttpClient);

  async downloadMyData(now: Date = new Date()): Promise<void> {
    const blob = await firstValueFrom(
      this.http.get(`${environment.speedReadingApiUrl}/data-privacy/export`, { responseType: 'blob' }));
    this.saveFile(blob, `hizli-okuma-kisisel-verilerim-${now.toISOString().slice(0, 10)}.json`);
  }

  saveFile(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
