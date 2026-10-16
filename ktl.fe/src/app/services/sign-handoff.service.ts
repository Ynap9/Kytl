import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';

import { Utils } from '../shared/utils';
import { SIGN_PAGE_URL } from '../shared/constants/sign.constants';

@Injectable({ providedIn: 'root' })
export class SignHandoffService {
  private router = inject(Router);

  private readonly pendingSources = signal<string[]>([]);

  open(paths: string[]): void {
    if (paths.length === 0) return;
    this.pendingSources.set(paths.map((path) => Utils.normalizeFolderPath(path)));
    this.router.navigateByUrl(SIGN_PAGE_URL);
  }

  take(): string[] {
    const paths = this.pendingSources();
    this.pendingSources.set([]);
    return paths;
  }
}
