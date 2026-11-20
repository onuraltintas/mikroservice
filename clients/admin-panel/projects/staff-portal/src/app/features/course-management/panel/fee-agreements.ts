import { CourseManagementService } from '../course-management.service';

/** The fee models of Tahsilat (1 term in instalments, 2 monthly, 3 per lesson or hour, 4 lesson package). */
export const FEE_MODELS = [
  { value: 1, label: 'Dönemlik, taksitli', short: 'Taksitli', hint: 'Toplam ücret, eşit taksitlere bölünür.' },
  { value: 2, label: 'Aylık sabit', short: 'Aylık', hint: 'Her ay aynı tutar; seçtiğiniz ay sayısı kadar taksit açılır.' },
  { value: 3, label: 'Ders ya da saat başı', short: 'Ders başı', hint: 'Ders tamamlandıkça borç yazılır; yoklamada "gelmedi" olsa da yazılır.' },
  { value: 4, label: 'Ders paketi', short: 'Paket', hint: 'Belirli sayıda ders hakkı; her tamamlanan ders bir hak düşer.' }
] as const;

export interface FeeAgreementForm {
  studentId: string; courseId: string; model: number; amount: number;
  /** Instalments (model 1) or months (model 2). */
  count: number;
  /** First due day, YYYY-MM-DD. */
  firstDue: string;
  /** Model 3: 1 per lesson, 2 per hour. */
  unit: number;
  /** Model 4: lessons in the package and the last day to use them (YYYY-MM-DD, may be empty). */
  sessions: number; expiresOn: string;
}

const monthName = (date: Date) => new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date);

/**
 * Saves a fee agreement and opens its instalments, the same way for Tahsilat and the setup guide.
 * Returns how many instalments were opened.
 */
export async function createFeeAgreementWithInstallments(service: CourseManagementService, workspaceId: string, form: FeeAgreementForm,
  today: string): Promise<number> {
  const amount = Math.round(form.amount * 100) / 100;
  const created = await service.createFeeAgreement(workspaceId, { id: crypto.randomUUID(), studentId: form.studentId, courseId: form.courseId,
    model: form.model as 1 | 2 | 3 | 4, amount, billingUnit: form.model === 3 ? form.unit as 1 | 2 : null,
    packageSessions: form.model === 4 ? form.sessions : null,
    packageExpiresAt: form.model === 4 && form.expiresOn ? new Date(`${form.expiresOn}T23:59:00+03:00`).toISOString() : null });
  const installments: { label: string; amount: number; dueOn: string }[] = [];
  if (form.model === 1 || form.model === 2) {
    const count = Math.max(1, Math.min(24, Math.floor(form.count)));
    const each = form.model === 1 ? Math.floor(amount / count * 100) / 100 : amount;
    for (let index = 0; index < count; index++) {
      const due = new Date(`${form.firstDue || today}T12:00:00Z`);
      due.setUTCMonth(due.getUTCMonth() + index);
      const value = form.model === 1 && index === count - 1 ? Math.round((amount - each * (count - 1)) * 100) / 100 : each;
      installments.push({ label: form.model === 1 ? `${index + 1}. taksit` : monthName(due), amount: value, dueOn: due.toISOString().slice(0, 10) });
    }
  } else if (form.model === 4) {
    installments.push({ label: `${form.sessions} derslik paket`, amount, dueOn: form.firstDue || today });
  }
  for (const item of installments) await service.createInstallment(workspaceId, created.id, { id: crypto.randomUUID(), ...item });
  return installments.length;
}
