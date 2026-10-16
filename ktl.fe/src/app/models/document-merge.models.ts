import {
  EMergeStampKind,
  EMergeStampRepeatMode,
  EMergeStampScope,
  MergeRowStatus
} from '../shared/constants/document-merge.constants';

export interface IExcelHeader {
  columnNumber: number;
  name: string;
}

export interface IMergeFieldMapping {
  fieldName: string;
  columnNumber: number | null;
  headerName?: string | null;
  autoMapped?: boolean;
}

export interface IMergeMapping {
  templatePath: string;
  dataPath: string;
  totalRows: number;
  headers: IExcelHeader[];
  fields: IMergeFieldMapping[];
}

export interface IMergeStampArea {
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
}

export interface IMergeStampPosition extends IMergeStampArea {
  pageNumber: number;
}

export interface IMergeStamp {
  kind: EMergeStampKind;
  scope: EMergeStampScope;
  repeatMode: EMergeStampRepeatMode;
  customPages: string | null;
  pageNumber: number;
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
  pagePositions: IMergeStampPosition[];
}

export interface IMergeRequest {
  templatePath: string;
  dataPath: string;
  mappings: IMergeFieldMapping[];
  fileNameColumnNumber: number | null;
  outputFolder: string | null;
  sealImagePath: string | null;
  wetSignatureImagePath: string | null;
  stamps: IMergeStamp[];
}

export interface IMergePreviewRequest extends IMergeRequest {
  rowNumber: number | null;
}

export interface IMergePreview {
  rowNumber: number;
  fileName: string;
  filePath: string;
  pageCount: number;
  warnings: string[];
}

export interface IMergeRowCheck {
  rowNumber: number;
  fileName: string;
  warnings: string[];
}

export interface IMergeValidation {
  totalRows: number;
  mappedFieldCount: number;
  unmappedFields: string[];
  warnings: string[];
  warningRowCount: number;
  rows: IMergeRowCheck[];
}

export interface IMergeFileResult {
  rowNumber: number;
  fileName: string;
  outputPath?: string | null;
  success: boolean;
  reason?: string | null;
  warnings: string[];
}

export interface IMergeResult {
  outputFolder: string;
  totalFiles: number;
  successCount: number;
  failedCount: number;
}

export interface IMergeProgress {
  targetType: string;
  target: string;
  status: number;
  code: number;
  message: string;
  completed: boolean;
  current: number;
  total: number;
  percent: number;
  files?: string[];
  file?: IMergeFileResult;
  summary?: IMergeResult;
}

export interface IMergeRunRow extends IMergeRowCheck {
  status: MergeRowStatus;
  message?: string | null;
  outputPath?: string | null;
}
