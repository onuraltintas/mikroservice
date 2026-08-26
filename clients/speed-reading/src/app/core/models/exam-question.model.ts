export enum ExamType {
    None = 0,
    LGS = 1,
    YKS = 2,
    KPSS = 3,
    ALES = 4,
    DGS = 5,
    General = 6
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
    NarrativeTechniques = 8, // Anlatım Teknikleri
    AuthorAttitude = 9,
    FlowBreaker = 10,
    SentenceCompletion = 11,
    Philosophy = 12,
    Judgment = 13,
    Logic = 14,
    Ordering = 15,
    CauseAndEffect = 16,
    ThinkingProcess = 17
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
