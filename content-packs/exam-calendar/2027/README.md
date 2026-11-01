# Exam calendar 2027 (KM1, 01.11.2026)

The coaching product prepares students for TYT/AYT/YDT (YKS), LGS, AGS (+ÖABT), YDS and YÖKDİL (user decision, 01.11.2026).
Since the `20261109090000_ExamSessionsAndNewPurposes` migration an exam has dated **sessions** instead of one `ExamDate`.
This pack adds the exams that are missing and the 2027 sessions from the ÖSYM calendar.

Source: ÖSYM 2027 Yılı Sınav Takvimi, announced 28.10.2026 —
<https://dokuman.osym.gov.tr/web/2026/10/osym-2027-yili-sinav-takvimi-7d09wi-28090734.pdf>
(press coverage of the same announcement: Milli Gazete, A Haber, osymli.com, 28.10.2026).

| Exam (code) | Session | Exam day | Applications |
| --- | --- | --- | --- |
| TYT | 2027 | 19.06.2027 | 04.02–02.03.2027 |
| AYT | 2027 | 20.06.2027 | 04.02–02.03.2027 |
| YDT | 2027 | 20.06.2027 | 04.02–02.03.2027 |
| AGS | 2027 | 08.05.2027 | 19.03–29.03.2027 |
| ÖABT (`OABT`) | 2027 | 09.05.2027 | 19.03–29.03.2027 |
| YDS | 2027/1 | 28.03.2027 | 03.02–11.02.2027 |
| YDS | 2027/2 | 21.11.2027 | 05.10–13.10.2027 |
| YÖKDİL (`YOKDIL`) | 2027/1 | 28.02.2027 | 12.01–20.01.2027 |
| YÖKDİL (`YOKDIL`) | 2027/2 | 25.07.2027 | 08.06–16.06.2027 |
| LGS | — | not announced by MEB yet | — |

Note: MEB-AGS is held on two days in 2027: AGS on Saturday 08.05.2027 and ÖABT on Sunday 09.05.2027 (the calendar and its
press coverage agree). e-YDS and e-YÖKDİL sessions are not part of this pack; add them in the admin panel when needed.

| File | Writes |
| --- | --- |
| `apply.sql` | yes, one guarded transaction |
| `verify.sql` | no (`BEGIN READ ONLY`) |

## Applying (a separate, approved step; not done by the PR)

1. Deploy the release that contains the migration; take the usual snapshot (docs/DB_BACKUP_RUNBOOK.md).
2. Apply on the Coaching database:

   ```sh
   docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < apply.sql
   ```

   It prints `before: exams N, 2027 sessions M` and `after: exams N, 2027 sessions 9, passive (activate in the admin panel): …`.
   Missing exams (AGS, OABT, YDS, YOKDIL on a fresh catalog) are added **passive**; existing exams keep their name and status.
   A session with the same label is set to the official date and window. It stops without changing anything if the migration
   has not run or another session of the same exam already sits on one of these dates. Running it again changes nothing.
3. Check:

   ```sh
   docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < verify.sql
   ```

4. In the admin panel ("Ders ve Müfredat Yönetimi" > Sınav türleri) review AGS, ÖABT, YDS and YÖKDİL and activate them once
   their lessons are ready. Students only see sessions of active exams.

## Rollback

Sessions are ordinary catalog rows: edit or remove them in the admin panel. A session a student aims for can be edited but not
removed (the editor says how many students chose it).

Test: `tests/Integration/Coaching.Curriculum.IntegrationTests/ExamSessionsPostgresTests.cs`
(`CalendarPackAddsThe2027SessionsOnceAndLoadsNewExamsPassive`) applies both files to a freshly migrated database, checks the
counts, idempotency and the conflict guard.
