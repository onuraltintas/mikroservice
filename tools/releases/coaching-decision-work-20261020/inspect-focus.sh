set -euo pipefail
cd /var/lib/eduivme/releases/coaching-decision-work-20261020
diff -u source/admin-panel/src/app/features/coaching-portal/pages/student-study-plans.component.ts candidate-student-study-plans.ts | head -n 100 || true
