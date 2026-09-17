import { HttpContextToken } from '@angular/common/http';

/** Prevents an optional authenticated read from recursively starting refresh. */
export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);
