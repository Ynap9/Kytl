import { Component, computed, input, model, output } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { PathPicker } from '../../../shared/components/path-picker/path-picker';
import { Utils } from '../../../shared/utils';
import { IMergeMapping } from '../../../models/document-merge.models';
import { MERGE_DATA_EXTENSION, MERGE_TEMPLATE_EXTENSION } from '../../../shared/constants/document-merge.constants';

@Component({
  selector: 'app-merge-source-step',
  imports: [...SharedImports, PathPicker],
  templateUrl: './merge-source-step.html',
  host: { class: 'flex min-h-0 flex-1 flex-col' }
})
export class MergeSourceStep {
  templatePath = model<string>('');
  dataPath = model<string>('');
  mapping = input<IMergeMapping | null>(null);
  mappingFresh = input<boolean>(false);
  reading = input<boolean>(false);
  canRead = input<boolean>(false);
  read = output<void>();

  protected readonly templateExtension = MERGE_TEMPLATE_EXTENSION;
  protected readonly dataExtension = MERGE_DATA_EXTENSION;

  templateInvalid = computed(() => this.hasWrongExtension(this.templatePath(), MERGE_TEMPLATE_EXTENSION));
  dataInvalid = computed(() => this.hasWrongExtension(this.dataPath(), MERGE_DATA_EXTENSION));
  stale = computed(() => !!this.mapping() && !this.mappingFresh());

  private hasWrongExtension(path: string, extension: string): boolean {
    const value = Utils.normalizeFolderPath(path).toLowerCase();
    return !!value && !value.endsWith(extension);
  }
}
