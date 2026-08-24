import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { map, catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { ApiResponse, ApiErrorResponse } from '../models/api-response.model';

/**
 * Functional interceptor that automatically unwraps ApiResponse<T> wrapper
 * and handles standardized error responses from backend.
 * Must be registered via withInterceptors([apiResponseInterceptor]) in provideHttpClient().
 */
export const apiResponseInterceptor: HttpInterceptorFn = (req, next) => {
    return next(req).pipe(
        map(event => {
            if (event instanceof HttpResponse) {
                const body = event.body;
                if (isApiResponse(body)) {
                    return event.clone({ body: body.data });
                }
            }
            return event;
        }),
        catchError((error: HttpErrorResponse) => {
            if (error.error && isApiErrorResponse(error.error)) {
                const apiError = error.error as ApiErrorResponse;
                return throwError(() => ({
                    status: error.status,
                    message: apiError.message,
                    errors: apiError.errors || [],
                    validationErrors: apiError.validationErrors || []
                }));
            }
            return throwError(() => error);
        })
    );
};

function isApiResponse(body: any): body is ApiResponse<any> {
    return body &&
        typeof body === 'object' &&
        'success' in body &&
        'data' in body &&
        'message' in body;
}

function isApiErrorResponse(error: any): error is ApiErrorResponse {
    return error &&
        typeof error === 'object' &&
        'success' in error &&
        error.success === false &&
        'message' in error;
}
