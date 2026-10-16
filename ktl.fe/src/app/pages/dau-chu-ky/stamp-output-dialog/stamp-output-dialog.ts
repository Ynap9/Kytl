import { Component, computed, input, model, output } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { PathPicker } from '../../../shared/components/path-picker/path-picker';

@Component({
  selector: 'app-stamp-output-dialog',
  imports: [...SharedImports, PathPicker],
  templateUrl: './stamp-output-dialog.html'
})
export class StampOutputDialog {
  visible = model<boolean>(false);
  outputFolder = model<string>('');
  signAfter = model<boolean>(false);
  fileCount = input<number>(0);
  stampCount = input<number>(0);
  signedCount = input<number>(0);
  hasDropped = input<boolean>(false);
  confirmed = output<void>();

  needsOutputFolder = computed(() => this.hasDropped() && !this.outputFolder());

  confirm(): void {
    if (this.needsOutputFolder()) return;
    this.visible.set(false);
    this.confirmed.emit();
  }
}
