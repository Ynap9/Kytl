import { Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { Utils } from '../../shared/utils';
import { DocumentMergeService } from '../../services/document-merge.service';
import {
  IMergeFieldMapping,
  IMergeMapping,
  IMergeRequest,
  IMergeStamp,
  IMergeValidation
} from '../../models/document-merge.models';
import {
  EMergeStep,
  MERGE_DATA_EXTENSION,
  MERGE_PAGE_TITLE,
  MERGE_STEPS,
  MERGE_TEMPLATE_EXTENSION
} from '../../shared/constants/document-merge.constants';
import { MergeSourceStep } from './merge-source-step/merge-source-step';
import { MergeMappingStep } from './merge-mapping-step/merge-mapping-step';
import { MergeStampStep } from './merge-stamp-step/merge-stamp-step';
import { MergeRunStep } from './merge-run-step/merge-run-step';

@Component({
  selector: 'app-tao-van-ban',
  imports: [...SharedImports, MergeSourceStep, MergeMappingStep, MergeStampStep, MergeRunStep],
  templateUrl: './tao-van-ban.html',
  styleUrl: './tao-van-ban.scss'
})
export class TaoVanBan extends BaseComponent {
  private documentMergeService = inject(DocumentMergeService);

  protected readonly title = MERGE_PAGE_TITLE;
  protected readonly steps = MERGE_STEPS;
  protected readonly EStep = EMergeStep;

  private runStep = viewChild(MergeRunStep);

  step = signal<EMergeStep>(EMergeStep.Source);
  reading = signal<boolean>(false);
  validating = signal<boolean>(false);
  running = signal<boolean>(false);

  templatePath = signal<string>('');
  dataPath = signal<string>('');
  outputFolder = signal<string>('');

  mapping = signal<IMergeMapping | null>(null);
  fields = signal<IMergeFieldMapping[]>([]);
  fileNameColumnNumber = signal<number | null>(null);
  validation = signal<IMergeValidation | null>(null);

  stampEnabled = signal<boolean>(false);
  sealImagePath = signal<string>('');
  wetSignatureImagePath = signal<string>('');
  stamps = signal<IMergeStamp[]>([]);

  busy = computed(() => this.reading() || this.validating() || this.running());

  templateName = computed(() =>
    (Utils.normalizeFolderPath(this.templatePath()).split('/').pop() ?? '').replace(/\.docx$/i, '')
  );

  fileNameHeader = computed(
    () => this.mapping()?.headers.find((header) => header.columnNumber === this.fileNameColumnNumber())?.name ?? null
  );

  sourceReady = computed(() => {
    const template = Utils.normalizeFolderPath(this.templatePath()).toLowerCase();
    const data = Utils.normalizeFolderPath(this.dataPath()).toLowerCase();
    return template.endsWith(MERGE_TEMPLATE_EXTENSION) && data.endsWith(MERGE_DATA_EXTENSION);
  });

  mappingFresh = computed(() => {
    const mapping = this.mapping();
    return (
      !!mapping &&
      mapping.templatePath === Utils.normalizeFolderPath(this.templatePath()) &&
      mapping.dataPath === Utils.normalizeFolderPath(this.dataPath())
    );
  });

  baseRequest = computed<IMergeRequest>(() => ({
    templatePath: Utils.normalizeFolderPath(this.templatePath()),
    dataPath: Utils.normalizeFolderPath(this.dataPath()),
    mappings: this.fields().map(({ fieldName, columnNumber }) => ({ fieldName, columnNumber })),
    fileNameColumnNumber: this.fileNameColumnNumber(),
    outputFolder: Utils.normalizeFolderPath(this.outputFolder()) || null,
    sealImagePath: null,
    wetSignatureImagePath: null,
    stamps: []
  }));

  request = computed<IMergeRequest>(() =>
    this.stampEnabled()
      ? {
          ...this.baseRequest(),
          sealImagePath: this.sealImagePath() || null,
          wetSignatureImagePath: this.wetSignatureImagePath() || null,
          stamps: this.stamps()
        }
      : this.baseRequest()
  );

  canNext = computed(() => {
    if (this.busy()) return false;
    switch (this.step()) {
      case EMergeStep.Source:
        return this.sourceReady();
      case EMergeStep.Mapping:
        return this.fields().length > 0;
      case EMergeStep.Stamp:
        return !this.stampEnabled() || this.stamps().length > 0;
      default:
        return false;
    }
  });

  canStart = computed(() => this.runStep()?.canStart() ?? false);
  runFinished = computed(() => this.runStep()?.finished() ?? false);

  private readVersion = 0;

  private autoRead = effect(() => {
    this.templatePath();
    this.dataPath();
    untracked(() => {
      if (this.sourceReady() && !this.mappingFresh()) this.readData();
    });
  });

  private validationReset = effect(() => {
    this.baseRequest();
    untracked(() => this.validation.set(null));
  });

  isStepDone(step: EMergeStep): boolean {
    return step < this.step();
  }

  goToStep(step: EMergeStep): void {
    if (step >= this.step() || this.busy()) return;
    this.step.set(step);
  }

  back(): void {
    this.goToStep(this.step() - 1);
  }

  next(): void {
    if (!this.canNext()) return;
    switch (this.step()) {
      case EMergeStep.Source:
        if (this.mappingFresh()) this.step.set(EMergeStep.Mapping);
        else this.readData(true);
        break;
      case EMergeStep.Mapping:
        if (this.validation()) this.step.set(EMergeStep.Stamp);
        else this.validate(true);
        break;
      case EMergeStep.Stamp:
        this.step.set(EMergeStep.Run);
        break;
    }
  }

  startRun(): void {
    this.runStep()?.start();
  }

  stopRun(): void {
    this.runStep()?.stop();
  }

  readData(goNext = false): void {
    if (!this.sourceReady()) return;
    const request = this.baseRequest();
    const version = ++this.readVersion;

    this.reading.set(true);
    this.documentMergeService.autoMap(request.templatePath, request.dataPath).subscribe({
      next: (res) => {
        if (version !== this.readVersion) return;
        this.reading.set(false);
        if (!this.isResponseSucceed(res)) return;
        this.mapping.set(res.data);
        this.fields.set(res.data.fields);
        this.fileNameColumnNumber.set(null);
        this.stamps.set([]);
        if (goNext) this.step.set(EMergeStep.Mapping);
      },
      error: () => {
        if (version !== this.readVersion) return;
        this.reading.set(false);
        this.messageError('Không kết nối được tới dịch vụ tạo văn bản. Vui lòng thử lại sau.');
      }
    });
  }

  validate(goNext = false): void {
    if (this.validating()) return;
    this.validating.set(true);
    this.documentMergeService.validate(this.baseRequest()).subscribe({
      next: (res) => {
        this.validating.set(false);
        if (!this.isResponseSucceed(res)) return;
        this.validation.set(res.data);
        if (goNext) this.step.set(EMergeStep.Stamp);
      },
      error: () => {
        this.validating.set(false);
        this.messageError('Không kết nối được tới dịch vụ tạo văn bản. Vui lòng thử lại sau.');
      }
    });
  }
}
