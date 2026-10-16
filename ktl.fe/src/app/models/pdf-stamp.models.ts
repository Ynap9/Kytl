import { IMergeStamp } from './document-merge.models';
import { PdfStampRowStatus } from '../shared/constants/pdf-stamp.constants';

export interface IPdfStampFile {
  name: string;
  fullPath: string;
  sizeBytes: number;
  pageCount: number;
  readable: boolean;
  alreadySigned: boolean;
}

export interface IPdfStampFileList {
  files: IPdfStampFile[];
}

export interface IPdfStampPreviewRequest {
  filePath: string;
  sealImagePath: string | null;
  wetSignatureImagePath: string | null;
  stamps: IMergeStamp[];
}

export interface IPdfStampPreview {
  filePath: string;
  pageCount: number;
  warnings: string[];
}

export interface IPdfStampRequest {
  sourcePaths: string[];
  outputFolder: string | null;
  sealImagePath: string | null;
  wetSignatureImagePath: string | null;
  stamps: IMergeStamp[];
}

export interface IPdfStampFileResult {
  fileName: string;
  outputPath?: string | null;
  success: boolean;
  reason?: string | null;
  warnings: string[];
}

export interface IPdfStampResult {
  outputFolder: string;
  totalFiles: number;
  successCount: number;
  failedCount: number;
}

export interface IPdfStampProgress {
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
  file?: IPdfStampFileResult;
  summary?: IPdfStampResult;
}

export interface IPdfStampRunRow {
  fileName: string;
  status: PdfStampRowStatus;
  message?: string | null;
  outputPath?: string | null;
  warnings: string[];
}
