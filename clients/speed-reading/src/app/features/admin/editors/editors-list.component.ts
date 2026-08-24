import { Component, inject, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { debounceTime, distinctUntilChanged, takeUntil, finalize } from 'rxjs/operators';

import { BaseComponent } from '../../../core/components/base.component';
import { UsersService } from '../../../core/services/users.service';
import { UserDto, UserDetailDto } from '../../../core/models/user.model';
import { EditorDialogComponent } from './editor-dialog.component';

@Component({
    selector: 'app-editors-list',
    standalone: true,
    imports: [
        CommonModule,
        MatTableModule,
        MatPaginatorModule,
        MatSortModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatMenuModule,
        MatInputModule,
        MatFormFieldModule,
        MatSelectModule,
        MatTooltipModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatDialogModule,
        ReactiveFormsModule,
        MatMenuModule,
        MatDividerModule
    ],
    templateUrl: './editors-list.component.html',
    styleUrls: ['./editors-list.component.scss']
})
export class EditorsListComponent extends BaseComponent implements OnInit, AfterViewInit {
    private usersService = inject(UsersService);
    private dialog = inject(MatDialog);

    dataSource = new MatTableDataSource<UserDto>([]);
    displayedColumns: string[] = ['index', 'name', 'email', 'status', 'createdAt', 'actions'];

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    searchControl = new FormControl<string>('');
    statusControl = new FormControl<boolean | null>(null);

    totalItems = 0;
    pageSize = 10;
    pageIndex = 0;

    constructor() {
        super();
    }

    ngOnInit() {
        this.setupFilters();
        this.loadEditors();
    }

    ngAfterViewInit() {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    setupFilters() {
        this.searchControl.valueChanges.pipe(
            debounceTime(300),
            distinctUntilChanged(),
            takeUntil(this.destroy$)
        ).subscribe(() => {
            this.pageIndex = 0;
            this.loadEditors();
        });

        this.statusControl.valueChanges.pipe(
            takeUntil(this.destroy$)
        ).subscribe(() => {
            this.pageIndex = 0;
            this.loadEditors();
        });
    }

    loadEditors() {
        this.loading.set(true);

        this.usersService.getUsers(
            this.searchControl.value || '',
            'Editor', // Role
            this.statusControl.value !== null ? this.statusControl.value : undefined
        ).pipe(
            takeUntil(this.destroy$),
            finalize(() => this.loading.set(false))
        ).subscribe({
            next: (items: UserDto[]) => {
                this.dataSource.data = items;
                this.totalItems = items.length;
            },
            error: (error: any) => {
                this.toaster.error('Editörler yüklenirken bir hata oluştu');
            }
        });
    }

    onPageChange(event: any) {
        this.pageIndex = event.pageIndex;
        this.pageSize = event.pageSize;
    }

    openDialog(user?: UserDto) {
        if (user) {
            // Fetch full user details first
            this.loading.set(true);
            this.usersService.getUserById(user.id).subscribe({
                next: (detail: UserDetailDto) => {
                    this.loading.set(false);
                    this.openDialogInternal(detail);
                },
                error: (err: any) => {
                    this.loading.set(false);
                    this.toaster.error('Kullanıcı detayları alınamadı. Lütfen tekrar deneyin.');
                }
            });
        } else {
            this.openDialogInternal(null);
        }
    }

    private openDialogInternal(data: UserDetailDto | null) {
        const dialogRef = this.dialog.open(EditorDialogComponent, {
            width: '500px',
            data: data
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.loadEditors();
            }
        });
    }

    async deleteEditor(user: UserDto) {
        const confirmed = await this.confirm(
            `"${user.firstName} ${user.lastName}" adlı editörü silmek istediğinize emin misiniz?`
        );

        if (confirmed) {
            this.usersService.deleteUser(user.id).subscribe({
                next: () => {
                    this.handleSuccess('Editör başarıyla silindi');
                    this.loadEditors();
                },
                error: (err: any) => this.toaster.error('Silme işlemi başarısız oldu. Lütfen tekrar deneyin.')
            });
        }
    }

    async toggleStatus(user: UserDto) {
        const actionText = user.isActive ? 'pasif duruma getirmek' : 'aktif etmek';

        const confirmed = await this.confirm(
            `"${user.firstName} ${user.lastName}" adlı editörü ${actionText} istediğinize emin misiniz?`
        );

        if (confirmed) {
            const obs = user.isActive
                ? this.usersService.deactivateUser(user.id)
                : this.usersService.activateUser(user.id);

            obs.subscribe({
                next: () => {
                    this.handleSuccess('Durum güncellendi');
                    this.loadEditors();
                },
                error: (err: any) => this.toaster.error('Durum güncelleme işlemi başarısız oldu.')
            });
        }
    }
}

