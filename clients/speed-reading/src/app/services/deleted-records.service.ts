import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { DeletedRecord, RestoreRecordRequest, HardDeleteRecordRequest } from '../models/deleted-record.model';

export interface DeletedRecordsPage {
    items: DeletedRecord[];
    totalCount: number;
    page: number;
    pageSize: number;
}

@Injectable({
    providedIn: 'root'
})
export class DeletedRecordsService {
    private baseUrl = '/api/v1/admin';

    constructor(private http: HttpClient) { }

    getDeletedRecords(options: { page: number; pageSize: number; entityType?: string }): Observable<DeletedRecordsPage> {
        let params = new HttpParams()
            .set('page', options.page)
            .set('pageSize', options.pageSize);
        if (options.entityType) {
            params = params.set('entityType', options.entityType);
        }

        return this.http.get<any>(`${this.baseUrl}/deleted`, { params }).pipe(
            map(response => {
                const items: DeletedRecord[] = (response?.items ?? []).map((r: any) => ({
                    id: r.id,
                    entityType: r.entityType,
                    displayName: r.entityName ?? r.displayName ?? r.entityId ?? '',
                    deletedAt: r.deletedAt,
                    deletedBy: r.deletedBy ?? r.userName,
                    metadata: r.oldValues ?? r.metadata ?? {}
                }));
                return {
                    items,
                    totalCount: response?.totalCount ?? 0,
                    page: response?.page ?? options.page,
                    pageSize: response?.pageSize ?? options.pageSize
                };
            })
        );
    }

    restoreRecord(request: RestoreRecordRequest): Observable<boolean> {
        if (request.entityType === 'User') {
            return this.http.post<void>(`/api/v1/users/${request.id}/restore`, {}).pipe(
                map(() => true)
            );
        }
        return this.http.post<void>(`${this.baseUrl}/restore/${request.id}`, {}).pipe(
            map(() => true)
        );
    }

    hardDeleteRecord(request: HardDeleteRecordRequest): Observable<boolean> {
        return this.http.delete<void>(`${this.baseUrl}/permanent/${request.id}`).pipe(
            map(() => true)
        );
    }
}
