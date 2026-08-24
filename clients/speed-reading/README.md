# Eduİvme Hızlı Okuma — bağımsız frontend

Bu Angular uygulaması `HizliOkuma` deposundaki mevcut öğrenci, öğretmen ve
admin deneyiminin EduPlatform monoreposuna taşınmış halidir. Uygulama iki
çalışma biçimini destekler:

- **Bağımsız ürün:** `/api/speed-reading` üzerinden hızlı okuma servisine
  bağlanır.
- **Platform istemcisi:** kimlik, koçluk ve ortak modüller için Gateway'in
  `/api` yollarını kullanır.

Hızlı okuma veritabanı frontend'e bağlanmaz; bağlantı yalnızca
`SpeedReading.API` servisindedir. Böylece mevcut veritabanı şeması ve
kullanıcı verileri korunur.

Modern, responsive web application for speed reading training and comprehension improvement.

## 📖 Overview

The Speed Reading Platform frontend is built with **Angular 20** and **Angular Material 20**, providing an intuitive interface for students to improve their reading speed, teachers to manage assignments, and administrators to oversee the entire platform.

### Key Features

- **Student Dashboard**: Track reading progress, complete exercises, view achievements
- **Teacher Portal**: Create assignments, monitor student progress, generate reports
- **Admin Panel**: User management, system settings, announcements, email campaigns
- **17 Exercise Types**: Including Schulte Table, RSVP, Visual Expansion, and more
- **Gamification**: XP points, badges, daily streaks, leaderboards
- **Reading Comprehension**: 200+ texts with Bloom's Taxonomy questions
- **Responsive Design**: Mobile-friendly interface with Angular Material
- **Real-time Updates**: Progress tracking, notifications, announcements

---

## 🏗️ Project Structure

```
src/
├── app/
│   ├── core/                    # Singleton services, guards, interceptors
│   │   ├── guards/              # Authentication and authorization guards
│   │   │   └── auth.guard.ts    # JWT-based route protection
│   │   ├── interceptors/        # HTTP interceptors
│   │   │   └── error.interceptor.ts  # Global error handling
│   │   ├── models/              # TypeScript interfaces and types
│   │   │   ├── user.model.ts
│   │   │   ├── student.model.ts
│   │   │   ├── exercise.model.ts
│   │   │   └── assignment.model.ts
│   │   └── services/            # Core application services
│   │       ├── auth.service.ts  # Authentication (login, register, JWT)
│   │       ├── students.service.ts
│   │       ├── exercises.service.ts
│   │       └── toaster.service.ts
│   │
│   ├── features/                # Feature modules (lazy-loaded)
│   │   ├── admin/               # Admin dashboard and management
│   │   │   ├── dashboard/       # Admin overview, statistics
│   │   │   ├── users/           # User CRUD operations
│   │   │   ├── settings/        # System settings (email, security, platform)
│   │   │   ├── announcements/   # Announcement management
│   │   │   ├── email-campaigns/ # Bulk email campaigns
│   │   │   └── audit-logs/      # System audit logs viewer
│   │   │
│   │   ├── student/             # Student learning interface
│   │   │   ├── dashboard/       # Progress overview, daily goals
│   │   │   ├── exercises/       # 17 exercise types
│   │   │   │   ├── schulte-table/
│   │   │   │   ├── tachistoscope/
│   │   │   │   ├── visual-expansion/
│   │   │   │   ├── saccade-exercise/
│   │   │   │   ├── fixation-exercise/
│   │   │   │   ├── speed-reading/
│   │   │   │   └── vocabulary-builder/
│   │   │   ├── reading/         # Reading comprehension
│   │   │   │   ├── reading-selection.component.ts
│   │   │   │   ├── reading-questions.component.ts
│   │   │   │   └── reading-result.component.ts
│   │   │   ├── rsvp-reader/     # Rapid Serial Visual Presentation
│   │   │   └── reports/         # Student progress reports
│   │   │
│   │   ├── teacher/             # Teacher management portal
│   │   │   ├── dashboard/       # Class overview, recent activity
│   │   │   ├── assignments/     # Create and manage assignments
│   │   │   │   ├── assignments-list.component.ts
│   │   │   │   ├── create-assignment-dialog.component.ts
│   │   │   │   └── assignment-detail.component.ts
│   │   │   ├── students/        # Student management
│   │   │   └── reports/         # Student analytics and reports
│   │   │       ├── reports.component.ts
│   │   │       ├── teacher-student-detail-report.component.ts
│   │   │       └── teacher-assignment-report.component.ts
│   │   │
│   │   ├── auth/                # Authentication pages
│   │   │   ├── login/           # Login with Google OAuth
│   │   │   └── register/        # User registration
│   │   │
│   │   ├── error/               # Error pages
│   │   │   ├── not-found.component.ts        # 404
│   │   │   ├── forbidden.component.ts        # 403
│   │   │   └── server-error.component.ts     # 500
│   │   │
│   │   ├── legal/               # Legal pages
│   │   │   ├── terms/           # Terms of Service
│   │   │   ├── privacy/         # Privacy Policy
│   │   │   └── kvkk/            # GDPR Compliance (KVKK)
│   │   │
│   │   ├── notifications/       # Notification preferences
│   │   │   └── notification-preferences.component.ts
│   │   │
│   │   └── gamification/        # Achievements and badges
│   │       └── badges.component.ts
│   │
│   └── shared/                  # Reusable components, pipes, directives
│       ├── components/          # Shared UI components
│       │   └── announcement-banner/
│       ├── layouts/             # Layout components
│       │   ├── admin-layout.component.ts
│       │   ├── student-layout.component.ts
│       │   └── teacher-layout.component.ts
│       ├── pipes/               # Custom pipes
│       └── directives/          # Custom directives
│
├── assets/                      # Static assets
│   ├── images/
│   ├── icons/
│   └── i18n/                    # Internationalization files
│
└── environments/                # Environment configurations
    ├── environment.ts           # Development
    └── environment.prod.ts      # Production
```

---

## 🚀 Getting Started

### Prerequisites

- **Node.js**: 18.x or higher
- **npm**: 9.x or higher
- **Angular CLI**: 20.x
- **Backend API**: Running on `https://localhost:7264`

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd speed-reading-frontend
   ```

2. **Install dependencies**
   ```bash
   npm install
   ```

3. **Configure environment**

   Create/edit `src/environments/environment.ts`:
   ```typescript
   export const environment = {
     production: false,
     apiUrl: '/api',
     speedReadingApiUrl: '/api/speed-reading',
     googleClientId: 'YOUR_GOOGLE_CLIENT_ID'
   };
   ```

4. **Start development server**
   ```bash
   npm start
   # or
   ng serve
   ```

5. **Open browser**
   ```
   Navigate to http://localhost:4200
   ```

### Development Proxy

The application uses a proxy to avoid CORS issues during development. The
configuration in `proxy.conf.json` forwards both `/api` and
`/api/speed-reading` to the local Gateway:

```json
{
  "/api": {
    "target": "https://localhost:7264",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

---

## 📦 Module Overview

### Admin Module

**Route**: `/admin`
**Guard**: `authGuard` (role: Admin)

**Features**:
- **Dashboard**: System statistics, user activity, recent registrations
- **User Management**: CRUD operations for users (create, edit, delete, role assignment)
- **System Settings**: Configure email (SMTP), platform settings, security options
- **Announcements**: Create system-wide announcements with priority and targeting
- **Email Campaigns**: Bulk email sending with tracking (sent, opened, clicked)
- **Audit Logs**: View system audit trail (who did what, when)

**Key Components**:
- `DashboardComponent` - Admin overview with charts
- `UsersComponent` - User list with search and filters
- `SettingsComponent` - System configuration (email, platform, security)
- `AnnouncementsComponent` - Announcement management
- `EmailCampaignsComponent` - Email campaign creation and tracking

---

### Student Module

**Route**: `/student`
**Guard**: `authGuard` (role: Student)

**Features**:
- **Dashboard**: Personal progress (WPM, comprehension, XP, streak)
- **Exercises**: 17 exercise types for speed reading improvement
- **Reading Comprehension**: 200+ texts with questions (Bloom's Taxonomy)
- **RSVP Reader**: Rapid Serial Visual Presentation reader
- **Progress Reports**: Personal analytics and growth charts

**Exercise Types**:
1. **Schulte Table** - Visual field expansion
2. **Tachistoscope** - Word recognition speed
3. **Visual Expansion** - Peripheral vision training
4. **Saccade Exercise** - Eye movement optimization
5. **Fixation Exercise** - Reduce subvocalization
6. **Speed Reading** - Timed reading practice
7. **Vocabulary Builder** - Word knowledge expansion
8. **Comprehension Quiz** - Understanding assessment
9. **Scanning Exercise** - Quick information retrieval
10. **Skimming Exercise** - Main idea extraction
11. **Chunking Exercise** - Word grouping
12. **Elimination Exercise** - Remove bad habits
13. **Pacing Exercise** - Consistent speed training
14. **Eye Span Exercise** - Widen visual span
15. **Regression Elimination** - Stop backtracking

**Key Components**:
- `StudentDashboardComponent` - Progress overview
- `SchulteTableComponent` - 5x5 number grid exercise
- `TachistoscopeComponent` - Flash word display
- `ReadingSelectionComponent` - Browse reading texts
- `ReadingQuestionsComponent` - Answer comprehension questions
- `RsvpReaderComponent` - RSVP reading mode

---

### Teacher Module

**Route**: `/teacher`
**Guard**: `authGuard` (role: Teacher)

**Features**:
- **Dashboard**: Class overview, student progress summary
- **Assignments**: Create and assign exercises/readings to students
- **Student Management**: View student profiles and progress
- **Reports**: Class analytics, individual student reports

**Key Components**:
- `TeacherDashboardComponent` - Class overview
- `AssignmentsListComponent` - Assignment list
- `CreateAssignmentDialogComponent` - Assignment creation dialog
- `AssignmentDetailComponent` - Assignment details and submissions
- `TeacherStudentDetailReportComponent` - Individual student analytics
- `TeacherAssignmentReportComponent` - Assignment completion stats

---

### Authentication Module

**Route**: `/auth`
**Guard**: None (public)

**Features**:
- **Login**: Email/password or Google OAuth
- **Register**: New user registration with email confirmation
- **JWT Token**: Access token + refresh token mechanism

**Key Components**:
- `LoginComponent` - Login form with Google Sign-In
- `RegisterComponent` - Registration form with validation

**Authentication Flow**:
1. User enters credentials
2. Backend validates and returns JWT tokens
3. Tokens stored in `localStorage`
4. `authGuard` checks token on route navigation
5. `authInterceptor` adds `Authorization` header to API calls
6. Token refresh on expiration

---

## 🛡️ Authentication & Authorization

### JWT-Based Authentication

**Token Storage**:
```typescript
// After successful login
localStorage.setItem('access_token', response.accessToken);
localStorage.setItem('refresh_token', response.refreshToken);
localStorage.setItem('user', JSON.stringify(response.user));
```

**Auth Guard**:
```typescript
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/auth/login']);
    return false;
  }

  const requiredRole = route.data['role'];
  if (requiredRole && !authService.hasRole(requiredRole)) {
    router.navigate(['/error/403']);
    return false;
  }

  return true;
};
```

**Role-Based Routing**:
```typescript
{
  path: 'student',
  canActivate: [authGuard],
  data: { role: 'Student' },
  loadChildren: () => import('./features/student/student.routes')
}
```

---

## 🌐 API Integration

### Base Service Pattern

```typescript
@Injectable({ providedIn: 'root' })
export class StudentsService {
  private apiUrl = `${environment.apiUrl}/students`;
  private http = inject(HttpClient);

  getCurrentStudent(): Observable<Student> {
    return this.http.get<Student>(`${this.apiUrl}/current`);
  }

  getStudentStatistics(): Observable<StudentStats> {
    return this.http.get<StudentStats>(`${this.apiUrl}/current/statistics`);
  }

  updateProgress(data: ProgressUpdate): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/current/progress`, data);
  }
}
```

### HTTP Interceptors

**Auth Interceptor**: Adds JWT token to requests
```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('access_token');

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  return next(req);
};
```

**Error Interceptor**: Global error handling
```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toaster = inject(ToasterService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        router.navigate(['/auth/login']);
      } else if (error.status === 403) {
        router.navigate(['/error/403']);
      } else if (error.status === 500) {
        toaster.alert('Server error occurred');
      }

      return throwError(() => error);
    })
  );
};
```

---

## 📊 State Management

### Service-Based State with BehaviorSubject

No external state management library (NgRx, Akita) is used. State is managed through services with RxJS `BehaviorSubject`.

**Example: Auth Service**:
```typescript
@Injectable({ providedIn: 'root' })
export class AuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/auth/login', credentials).pipe(
      tap(response => {
        localStorage.setItem('access_token', response.accessToken);
        this.currentUserSubject.next(response.user);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('access_token');
    this.currentUserSubject.next(null);
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('access_token');
  }
}
```

**Component Usage**:
```typescript
export class DashboardComponent {
  private authService = inject(AuthService);

  // Subscribe to user state
  user$ = this.authService.currentUser$;
}
```

**Template (async pipe)**:
```html
<div *ngIf="user$ | async as user">
  Welcome, {{ user.firstName }}!
</div>
```

---

## 🎨 UI/UX

### Angular Material 20

**Installed Modules**:
- MatCardModule
- MatButtonModule
- MatInputModule
- MatFormFieldModule
- MatTableModule
- MatPaginatorModule
- MatSortModule
- MatDialogModule
- MatSnackBarModule
- MatIconModule
- MatToolbarModule
- MatSidenavModule
- MatListModule
- MatTabsModule
- MatSelectModule
- MatCheckboxModule
- MatRadioModule
- MatProgressSpinnerModule

### Responsive Design

- **Mobile-first**: Optimized for mobile devices
- **Breakpoints**:
  - Mobile: < 600px
  - Tablet: 600px - 1024px
  - Desktop: > 1024px

---

## 🧪 Testing

### Unit Tests

```bash
# Run all tests
ng test

# Run with coverage
ng test --code-coverage

# Watch mode
ng test --watch
```

### E2E Tests (Planned)

```bash
# Install Cypress
npm install cypress --save-dev

# Run E2E tests
npm run e2e
```

---

## 🚢 Build & Deployment

### Development Build

```bash
ng build
```

Output: `dist/speed-reading-frontend/`

### Production Build

```bash
ng build --configuration production
```

**Production Optimizations**:
- AOT compilation
- Tree-shaking
- Minification
- Source maps disabled
- Environment variables (production API URL)

### Environment-Specific Builds

```bash
# Development
ng build --configuration development

# Staging
ng build --configuration staging

# Production
ng build --configuration production
```

---

## 📚 Code Conventions

### File Naming

- **Components**: `kebab-case.component.ts` (e.g., `student-dashboard.component.ts`)
- **Services**: `kebab-case.service.ts` (e.g., `auth.service.ts`)
- **Models**: `kebab-case.model.ts` (e.g., `student.model.ts`)
- **Guards**: `kebab-case.guard.ts` (e.g., `auth.guard.ts`)

### Component Structure

```typescript
import { Component, OnInit, inject } from '@angular/core';

@Component({
  selector: 'app-component-name',
  standalone: true,
  imports: [CommonModule, ...],
  templateUrl: './component-name.component.html',
  styleUrls: ['./component-name.component.scss']
})
export class ComponentNameComponent implements OnInit {
  // 1. Services (inject)
  private service = inject(SomeService);

  // 2. Observables
  data$ = this.service.getData();

  // 3. Properties
  loading = false;

  // 4. Lifecycle hooks
  ngOnInit(): void {
    this.loadData();
  }

  // 5. Public methods
  public handleAction(): void { }

  // 6. Private methods
  private loadData(): void { }
}
```

### TypeScript Style

- **2 spaces** indentation
- **Single quotes** for strings
- **Semicolons** required
- **Trailing commas** in multi-line objects/arrays

---

## 🔧 Configuration

### Angular CLI Configuration (`angular.json`)

Key configurations:
- **Budget**: Warning at 500kb, error at 1mb
- **Styles**: Global styles in `src/styles.scss`
- **Assets**: Images, fonts in `src/assets/`
- **Proxy**: `proxy.conf.json` for development

### TSConfig

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "strict": true,
    "esModuleInterop": true
  }
}
```

---

## 🌍 Internationalization (i18n)

Planned support for:
- Turkish (tr-TR)
- English (en-US)

---

## 📖 Additional Documentation

- [Component Architecture Guide](../../docs/frontend/COMPONENT_ARCHITECTURE.md)
- [State Management Guide](../../docs/frontend/STATE_MANAGEMENT.md)
- [API Integration Guide](../../docs/frontend/API_INTEGRATION.md)
- [Main README](../../README.md)

---

## 🤝 Contributing

Please read [CONTRIBUTING.md](../../CONTRIBUTING.md) for development guidelines.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE) file for details.

---

## 🐛 Troubleshooting

### Common Issues

**Issue**: CORS error when calling API
**Solution**: Ensure proxy configuration is correct in `proxy.conf.json`

**Issue**: 401 Unauthorized on API calls
**Solution**: Check if JWT token is stored in localStorage and not expired

**Issue**: Routing doesn't work after refresh
**Solution**: Configure server to redirect all routes to `index.html`

---

## 📞 Support

- Email: support@speedreading.com
- Documentation: https://docs.speedreading.com
- Issues: https://github.com/your-org/speed-reading-platform/issues

---

**Built with ❤️ using Angular 20 and Angular Material**
