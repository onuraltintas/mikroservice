import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { ToasterService } from './toaster.service';

describe('ToasterService contract', () => {
  it('opens semantic toasts with the shared options shape', () => {
    const snackBar = { open: vi.fn(), dismiss: vi.fn() };
    const dialog = { open: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        ToasterService,
        { provide: MatSnackBar, useValue: snackBar },
        { provide: MatDialog, useValue: dialog }
      ]
    });

    TestBed.inject(ToasterService).success('Profil kaydedildi.', {
      title: 'Başarılı',
      duration: 2400
    });

    expect(snackBar.open).toHaveBeenCalledWith(
      'Başarılı: Profil kaydedildi.',
      'Kapat',
      expect.objectContaining({ duration: 2400, panelClass: ['ui-toast', 'ui-toast--success'] })
    );
  });

  it('uses the same message-first confirmation contract', async () => {
    const snackBar = { open: vi.fn(), dismiss: vi.fn() };
    const dialog = {
      open: vi.fn().mockReturnValue({ afterClosed: () => of(true) })
    };

    TestBed.configureTestingModule({
      providers: [
        ToasterService,
        { provide: MatSnackBar, useValue: snackBar },
        { provide: MatDialog, useValue: dialog }
      ]
    });

    const confirmed = await TestBed.inject(ToasterService).confirm('Bu işlem uygulansın mı?', {
      title: 'İşlemi onayla',
      confirmText: 'Uygula',
      cancelText: 'Vazgeç'
    });

    expect(confirmed).toBe(true);
    expect(dialog.open).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        data: {
          title: 'İşlemi onayla',
          message: 'Bu işlem uygulansın mı?',
          confirmText: 'Uygula',
          cancelText: 'Vazgeç'
        }
      })
    );
  });

  it('returns text from the shared prompt dialog', async () => {
    const dialog = {
      open: vi.fn().mockReturnValue({ afterClosed: () => of('Yeni yanıt') })
    };

    TestBed.configureTestingModule({
      providers: [
        ToasterService,
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
        { provide: MatDialog, useValue: dialog }
      ]
    });

    const value = await TestBed.inject(ToasterService).prompt('Yanıt metni', 'Eski yanıt', {
      title: 'Mesajı yanıtla',
      multiline: true
    });

    expect(value).toBe('Yeni yanıt');
  });
});
