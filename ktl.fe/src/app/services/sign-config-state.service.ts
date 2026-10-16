import { Injectable, signal } from '@angular/core';

import { ISignConfig } from '../models/sign.models';
import { SIGN_CONFIG_STORAGE_KEY, SIGN_DEFAULT_CONFIG } from '../shared/constants/sign.constants';
import { Utils } from '../shared/utils';

@Injectable({ providedIn: 'root' })
export class SignConfigStateService {
  readonly config = signal<ISignConfig>({
    ...SIGN_DEFAULT_CONFIG,
    ...Utils.getLocalStorage(SIGN_CONFIG_STORAGE_KEY)
  });

  save(config: ISignConfig): void {
    this.config.set(config);
    Utils.setLocalStorage(SIGN_CONFIG_STORAGE_KEY, config);
  }
}
