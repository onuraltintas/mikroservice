export enum ExamType {
    LGS = 0,
    YKS = 1,
    KPSS = 2,
    ALES = 3,
    DGS = 4,
    General = 5
}

export enum QuestionCategory {
    None = 0,
    MainIdea = 1,          // Ana Fikir
    Inference = 2,          // Çıkarım
    VocabularyInContext = 3, // Sözcük Anlamı
    Detail = 4,             // Detay
    Coherence = 5,          // Anlam Bütünlüğü
    Title = 6,              // Başlık
    AuthorPurpose = 7,      // Yazarın Amacı
    NarrativeTechniques = 8 // Anlatım Teknikleri
}

export interface ExamQuestion {
    id: string;
    content: string;
    question: string;
    optionA: string;
    optionB: string;
    optionC: string;
    optionD: string;
    optionE?: string;
    correctOption: string;
    examType: ExamType;
    difficulty: number;
    wordCount: number;
    topic?: string;
    category: QuestionCategory;
    createdAt: string;
}
