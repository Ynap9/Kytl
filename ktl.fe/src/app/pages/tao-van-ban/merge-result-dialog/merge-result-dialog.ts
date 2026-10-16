import { Component, computed, input, model, output } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { IMergeResult } from '../../../models/document-merge.models';

@Component({
  selector: 'app-merge-result-dialog',
  imports: [...SharedImports],
  templateUrl: './merge-result-dialog.html'
})
export class MergeResultDialog {
  visible = model<boolean>(false);
  result = input<IMergeResult | null>(null);
  openFolder = output<string>();

  hasError = computed(() => (this.result()?.failedCount ?? 0) > 0);

  onOpenFolder(): void {
    const folder = this.result()?.outputFolder;
    if (folder) this.openFolder.emit(folder);
  }
}
