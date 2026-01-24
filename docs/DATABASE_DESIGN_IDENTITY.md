# 📊 Veritabanı Tasarımı - Identity Service

## 🏗️ Genel Yapı

```
                            ┌─────────────────┐
                            │   INSTITUTION   │
                            │    (Kurum)      │
                            └────────┬────────┘
                                     │ 1
                                     │
                    ┌────────────────┼────────────────┐
                    │ *              │ *              │ *
                    ▼                ▼                ▼
            ┌───────────┐    ┌───────────┐    ┌───────────┐
            │  TEACHER  │    │  STUDENT  │    │   ADMIN   │
            │           │    │           │    │  (Kurum)  │
            └─────┬─────┘    └───────────┘    └───────────┘
                  │ 1
                  │
                  │ *
            ┌─────▼─────┐
            │ TEACHER   │
            │ STUDENT   │
            │ ASSIGNMENT│
            └───────────┘
```

---

## 📋 Tablolar

### 1. Users (Keycloak Sync)
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,                    -- Keycloak UserId ile aynı
    email VARCHAR(255) NOT NULL UNIQUE,
    email_confirmed BOOLEAN DEFAULT FALSE,
    phone_number VARCHAR(20),
    phone_confirmed BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX idx_users_email ON users(email);
```

### 2. User Roles
```sql
CREATE TYPE user_role AS ENUM (
    'Student',
    'Teacher', 
    'Parent',
    'InstitutionAdmin',
    'InstitutionOwner',
    'SystemAdmin'
);

CREATE TABLE user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id),
    role user_role NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(user_id, role)
);

CREATE INDEX idx_user_roles_user_id ON user_roles(user_id);
```

### 3. Institutions (Kurumlar)
```sql
CREATE TYPE institution_type AS ENUM (
    'School',           -- Okul
    'PrivateCourse',    -- Dershane
    'StudyCenter',      -- Etüt Merkezi
    'OnlinePlatform'    -- Online Platform
);

CREATE TYPE license_type AS ENUM (
    'Trial',        -- 14 gün deneme
    'Basic',        -- Temel paket
    'Premium',      -- Gelişmiş paket
    'Enterprise'    -- Kurumsal paket
);

CREATE TABLE institutions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    type institution_type NOT NULL,
    logo_url VARCHAR(500),
    address TEXT,
    city VARCHAR(100),
    district VARCHAR(100),
    phone VARCHAR(20),
    email VARCHAR(255),
    website VARCHAR(255),
    tax_number VARCHAR(20),
    
    -- Lisans Bilgileri
    license_type license_type DEFAULT 'Trial',
    max_students INTEGER DEFAULT 50,
    max_teachers INTEGER DEFAULT 5,
    subscription_start_date TIMESTAMPTZ,
    subscription_end_date TIMESTAMPTZ,
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX idx_institutions_type ON institutions(type);
CREATE INDEX idx_institutions_city ON institutions(city);
```

### 4. Institution Admins (Kurum Yöneticileri)
```sql
CREATE TYPE institution_admin_role AS ENUM (
    'Owner',    -- Kurum sahibi
    'Admin',    -- Yönetici
    'Manager'   -- Müdür/Koordinatör
);

CREATE TABLE institution_admins (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id),
    institution_id UUID NOT NULL REFERENCES institutions(id),
    role institution_admin_role NOT NULL,
    permissions JSONB DEFAULT '[]',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    UNIQUE(user_id, institution_id)
);

CREATE INDEX idx_institution_admins_institution ON institution_admins(institution_id);
```

### 5. Student Profiles (Öğrenci Profilleri)
```sql
CREATE TYPE gender AS ENUM ('Male', 'Female', 'Other', 'PreferNotToSay');
CREATE TYPE learning_style AS ENUM ('Visual', 'Auditory', 'Kinesthetic', 'ReadingWriting');

CREATE TABLE student_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    institution_id UUID REFERENCES institutions(id),  -- NULL = bireysel öğrenci
    parent_id UUID REFERENCES users(id),              -- Veli bağlantısı
    
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    birth_date DATE,
    gender gender,
    
    grade_level SMALLINT CHECK (grade_level BETWEEN 1 AND 12),
    school_name VARCHAR(255),
    school_city VARCHAR(100),
    
    avatar_url VARCHAR(500),
    bio TEXT,
    learning_style learning_style,
    daily_goal_minutes INTEGER DEFAULT 30,
    
    preferences JSONB DEFAULT '{
        "theme": "light",
        "language": "tr",
        "notifications_enabled": true,
        "sound_enabled": true
    }',
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX idx_student_profiles_institution ON student_profiles(institution_id);
CREATE INDEX idx_student_profiles_parent ON student_profiles(parent_id);
CREATE INDEX idx_student_profiles_grade ON student_profiles(grade_level);
```

### 6. Teacher Profiles (Öğretmen Profilleri)
```sql
CREATE TABLE teacher_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    institution_id UUID REFERENCES institutions(id),  -- NULL = bağımsız öğretmen
    
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    title VARCHAR(50),                                -- Öğretmen, Uzman, Prof. Dr.
    
    subjects VARCHAR(100)[] DEFAULT '{}',             -- Matematik, Türkçe, vb.
    experience_years SMALLINT,
    
    avatar_url VARCHAR(500),
    bio TEXT,
    certifications JSONB DEFAULT '[]',
    
    -- Yetkiler
    is_independent BOOLEAN DEFAULT FALSE,             -- Kurumdan bağımsız mı?
    can_view_all_institution_students BOOLEAN DEFAULT FALSE,
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX idx_teacher_profiles_institution ON teacher_profiles(institution_id);
CREATE INDEX idx_teacher_profiles_subjects ON teacher_profiles USING GIN(subjects);
```

### 7. Parent Profiles (Veli Profilleri)
```sql
CREATE TYPE parent_relationship AS ENUM ('Mother', 'Father', 'Guardian', 'Other');

CREATE TABLE parent_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id),
    
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone_number VARCHAR(20),
    relationship parent_relationship,
    
    notification_preferences JSONB DEFAULT '{
        "daily_report": false,
        "weekly_progress": true,
        "exam_results": true,
        "low_activity_alert": true
    }',
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);
```

### 8. Teacher-Student Assignments (Öğretmen-Öğrenci Atamaları)
```sql
CREATE TABLE teacher_student_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    teacher_id UUID NOT NULL REFERENCES teacher_profiles(id),
    student_id UUID NOT NULL REFERENCES student_profiles(id),
    institution_id UUID REFERENCES institutions(id),  -- Hangi kurum üzerinden
    
    subject VARCHAR(100),                             -- Hangi ders için atama
    start_date DATE DEFAULT CURRENT_DATE,
    end_date DATE,                                    -- NULL = devam ediyor
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by UUID REFERENCES users(id),
    
    UNIQUE(teacher_id, student_id, subject)
);

CREATE INDEX idx_assignments_teacher ON teacher_student_assignments(teacher_id);
CREATE INDEX idx_assignments_student ON teacher_student_assignments(student_id);
CREATE INDEX idx_assignments_institution ON teacher_student_assignments(institution_id);
```

---

## 🔐 Yetki Matrisi

| Rol | Öğrenci Görüntüleme | Sonuç Görüntüleme | Kurum Yönetimi |
|-----|---------------------|-------------------|----------------|
| **Student** | Kendisi | Kendi sonuçları | ❌ |
| **Parent** | Çocukları | Çocuklarının sonuçları | ❌ |
| **Teacher (Bağımsız)** | Atanmış öğrencileri | Atanmış öğrencilerin sonuçları | ❌ |
| **Teacher (Kurumlu)** | Atanmış öğrencileri | Atanmış öğrencilerin sonuçları | ❌ |
| **InstitutionAdmin** | Kurumdaki tüm öğrenciler | Kurum öğrencilerinin sonuçları | ✅ |
| **InstitutionOwner** | Kurumdaki tüm öğrenciler | Kurum öğrencilerinin sonuçları | ✅ (tam yetki) |
| **SystemAdmin** | Tüm öğrenciler | Tüm sonuçlar | ✅ (tüm kurumlar) |

---

## 📐 İş Kuralları

1. **Öğretmen - Kurum**: Bir öğretmen en fazla 1 kuruma bağlı olabilir
2. **Öğrenci - Kurum**: Bir öğrenci en fazla 1 kuruma bağlı olabilir
3. **Öğrenci - Öğretmen**: Bir öğrenci birden fazla öğretmene atanabilir (farklı dersler için)
4. **Öğrenci - Veli**: Bir öğrencinin 1 veli bağlantısı olabilir, bir velinin birden fazla çocuğu olabilir
5. **Kurum Limitleri**: Kurum lisansına göre max_students ve max_teachers limiti kontrol edilir

---

## 🏷️ Kurum Tipleri

| Tip | Açıklama | Varsayılan Limitler |
|-----|----------|---------------------|
| **School** | Okul | 500 öğrenci, 50 öğretmen |
| **PrivateCourse** | Dershane | 200 öğrenci, 20 öğretmen |
| **StudyCenter** | Etüt Merkezi | 50 öğrenci, 5 öğretmen |
| **OnlinePlatform** | Online Platform | 1000 öğrenci, 10 öğretmen |

---

*Son Güncelleme: 2024-12-20*
