import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PersonalDataService } from './personal-data.service';

// Review H2b (finding 9): a student downloads every record Speed Reading keeps about them.
describe('PersonalDataService', () => {
  let service: PersonalDataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PersonalDataService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('downloads the export as a dated JSON file', async () => {
    const saved: { name: string; blob: Blob }[] = [];
    service.saveFile = (blob, name) => saved.push({ blob, name });

    const done = service.downloadMyData(new Date('2026-10-29T10:00:00Z'));
    const request = http.expectOne('/api/speed-reading/data-privacy/export');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['{"schemaVersion":"speed-reading-1.0"}'], { type: 'application/json' }));
    await done;

    expect(saved.length).toBe(1);
    expect(saved[0].name).toBe('hizli-okuma-kisisel-verilerim-2026-10-29.json');
    expect(await saved[0].blob.text()).toContain('speed-reading-1.0');
  });
});
