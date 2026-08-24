/**
 * Standard API Response wrapper interface
 * Matches backend's ApiResponse<T> format
 */
export interface ApiResponse<T> {
    success: boolean;
    message: string;
    data: T;
    errors: string[] | null;
    meta?: PaginationMeta;
}

/**
 * Pagination metadata for list responses
 */
export interface PaginationMeta {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages?: number;
}

/**
 * Validation error detail
 */
export interface ValidationError {
    field: string;
    message: string;
}

/**
 * Error response from API
 */
export interface ApiErrorResponse {
    success: false;
    message: string;
    errors: string[];
    validationErrors?: ValidationError[];
}
