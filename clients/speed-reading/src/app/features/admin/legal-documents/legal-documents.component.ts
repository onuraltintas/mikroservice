import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LegalDocumentService } from '../../../services/legal-document.service';
import { ToasterService } from '../../../core/services/toaster.service';
import {
    LegalDocument,
    LegalDocumentType,
    CreateLegalDocumentDto
} from '../../../models/legal-document.model';

@Component({
    selector: 'app-legal-documents',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './legal-documents.component.html',
    styleUrls: ['./legal-documents.component.scss']
})
export class LegalDocumentsComponent implements OnInit {
    documents: LegalDocument[] = [];
    filteredDocuments: LegalDocument[] = [];
    loading = false;
    showForm = false;
    editingDocument: LegalDocument | null = null;

    // Form data
    formData: CreateLegalDocumentDto = {
        type: LegalDocumentType.TermsOfService,
        version: '1.0',
        language: 'tr',
        title: '',
        content: '',
        effectiveDate: new Date().toISOString().split('T')[0],
        isActive: false
    };

    // Filter
    filterType: LegalDocumentType | 'all' = 'all';
    filterLanguage: string = 'all';
    filterActive: string = 'all';

    // Enums for template
    documentTypes = Object.values(LegalDocumentType).filter(v => typeof v === 'number');
    LegalDocumentType = LegalDocumentType;

    constructor(
        private legalDocumentService: LegalDocumentService,
        private toaster: ToasterService
    ) { }

    ngOnInit(): void {
        this.loadDocuments();
    }

    loadDocuments(): void {
        this.loading = true;
        this.legalDocumentService.getAllDocuments().subscribe({
            next: (docs) => {
                this.documents = docs;
                this.applyFilters();
                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading documents:', err);
                this.toaster.error('Dokümanlar yüklenirken hata oluştu');
                this.loading = false;
            }
        });
    }

    applyFilters(): void {
        this.filteredDocuments = this.documents.filter(doc => {
            const typeMatch = this.filterType === 'all' || doc.type === this.filterType;
            const langMatch = this.filterLanguage === 'all' || doc.language === this.filterLanguage;
            const activeMatch = this.filterActive === 'all' ||
                (this.filterActive === 'active' && doc.isActive) ||
                (this.filterActive === 'inactive' && !doc.isActive);
            return typeMatch && langMatch && activeMatch;
        });
    }

    openCreateForm(): void {
        this.editingDocument = null;
        this.formData = {
            type: LegalDocumentType.TermsOfService,
            version: '1.0',
            language: 'tr',
            title: '',
            content: '',
            effectiveDate: new Date().toISOString().split('T')[0],
            isActive: false
        };
        this.showForm = true;
    }

    openEditForm(doc: LegalDocument): void {
        this.editingDocument = doc;
        this.formData = {
            type: doc.type,
            version: doc.version,
            language: doc.language,
            title: doc.title,
            content: doc.content,
            effectiveDate: doc.effectiveDate.split('T')[0],
            isActive: doc.isActive
        };
        this.showForm = true;
    }

    closeForm(): void {
        this.showForm = false;
        this.editingDocument = null;
    }

    saveDocument(): void {
        if (!this.formData.title || !this.formData.content) {
            this.toaster.warning('Başlık ve içerik zorunludur');
            return;
        }

        this.loading = true;

        const dto: CreateLegalDocumentDto = {
            ...this.formData,
            effectiveDate: new Date(this.formData.effectiveDate).toISOString()
        };

        if (this.editingDocument) {
            // Update existing
            this.legalDocumentService.updateDocument(this.editingDocument.id, dto).subscribe({
                next: () => {
                    this.toaster.success('Doküman başarıyla güncellendi');
                    this.closeForm();
                    this.loadDocuments();
                },
                error: (err) => {
                    console.error('Error updating document:', err);
                    this.toaster.error('Güncelleme hatası: ' + (err.error?.message || err.message));
                    this.loading = false;
                }
            });
        } else {
            // Create new
            this.legalDocumentService.createDocument(dto).subscribe({
                next: () => {
                    this.toaster.success('Doküman başarıyla oluşturuldu');
                    this.closeForm();
                    this.loadDocuments();
                },
                error: (err) => {
                    console.error('Error creating document:', err);
                    this.toaster.error('Oluşturma hatası: ' + (err.error?.message || err.message));
                    this.loading = false;
                }
            });
        }
    }

    async deleteDocument(doc: LegalDocument): Promise<void> {
        const confirmed = await this.toaster.confirm(
            `"${doc.title}" dokümanını silmek istediğinizden emin misiniz?`,
            'Doküman Sil'
        );

        if (!confirmed) {
            return;
        }

        this.loading = true;
        this.legalDocumentService.deleteDocument(doc.id).subscribe({
            next: () => {
                this.toaster.success('Doküman başarıyla silindi');
                this.loadDocuments();
            },
            error: (err) => {
                console.error('Error deleting document:', err);
                this.toaster.error('Silme hatası: ' + (err.error?.message || err.message));
                this.loading = false;
            }
        });
    }

    toggleActive(doc: LegalDocument): void {
        const updatedDoc: CreateLegalDocumentDto = {
            type: doc.type,
            version: doc.version,
            language: doc.language,
            title: doc.title,
            content: doc.content,
            effectiveDate: doc.effectiveDate,
            isActive: !doc.isActive
        };

        this.loading = true;
        this.legalDocumentService.updateDocument(doc.id, updatedDoc).subscribe({
            next: () => {
                this.toaster.success(`Doküman ${!doc.isActive ? 'aktif' : 'pasif'} edildi`);
                this.loadDocuments();
            },
            error: (err) => {
                console.error('Error toggling active:', err);
                this.toaster.error('Güncelleme hatası: ' + (err.error?.message || err.message));
                this.loading = false;
            }
        });
    }

    getTypeLabel(type: LegalDocumentType): string {
        switch (type) {
            case LegalDocumentType.TermsOfService:
                return 'Kullanım Koşulları';
            case LegalDocumentType.PrivacyPolicy:
                return 'Gizlilik Politikası';
            case LegalDocumentType.KVKK:
                return 'KVKK';
            case LegalDocumentType.CookiePolicy:
                return 'Çerez Politikası';
            default:
                return type;
        }
    }
}
