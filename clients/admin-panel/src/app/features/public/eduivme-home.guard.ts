import { DOCUMENT } from '@angular/common';
import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';

/** eduivme.com and www.eduivme.com open the parent brand's page at "/"; admin.eduivme.com and the rest do not. */
export const eduivmeHomeHost: CanMatchFn = () =>
  /^(www\.)?eduivme\.com$/i.test((inject(DOCUMENT).location?.hostname ?? '').replace(/\.$/, ''));
