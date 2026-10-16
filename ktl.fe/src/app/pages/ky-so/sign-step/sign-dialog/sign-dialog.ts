import { Component, effect, input, model, output, signal, untracked } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';

@Component({
  selector: 'app-sign-dialog',
  imports: [...SharedImports],
  templateUrl: './sign-dialog.html',
  styleUrl: './sign-dialog.scss'
})
export class SignDialog extends BaseComponent {
  visible = model<boolean>(false);
  start = output<{ reason: string; location: string }>();
  defaultReason = input<string>('');
  defaultLocation = input<string>('');

  reason = signal<string>('');
  location = signal<string>('');

  constructor() {
    super();
    effect(() => {
      if (!this.visible()) return;
      untracked(() => {
        this.reason.set(this.defaultReason());
        this.location.set(this.defaultLocation());
      });
    });
  }

  onCancel(): void {
    this.visible.set(false);
  }

  onStart(): void {
    this.start.emit({ reason: this.reason().trim(), location: this.location().trim() });
    this.visible.set(false);
  }
}
