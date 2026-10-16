import { ECertSource } from '../shared/constants/sign.constants';

export interface ISignatureDetail {
  signedBy?: string;
  verdict: number;
  reason?: string;
  trustedTime?: string;
  signingTime?: string;
}

export interface ISignCert {
  subject: string;
  commonName: string;
  issuer: string;
  issuerCommonName: string;
  serialNumber: string;
  thumbprint: string;
  source: ECertSource;
  keyProvider?: string | null;
  validFrom: string;
  validTo: string;
  hasPrivateKey: boolean;
  isExpired: boolean;
  allowsSigning: boolean;
  isTrusted: boolean;
  canSign: boolean;
  reason?: string | null;
}

export interface ITokenVerify {
  valid: boolean;
  thumbprint: string;
  commonName?: string | null;
  issuerCommonName?: string | null;
  foundInStore: boolean;
  hasPrivateKey: boolean;
  notExpired: boolean;
  allowsSigning: boolean;
  canSignTest: boolean;
  trustedChain: boolean;
  failedCheck?: string | null;
  reason?: string | null;
}

export interface ISignFileResult {
  fileName: string;
  outputPath?: string | null;
  success: boolean;
  reason?: string | null;
  verdict: number;
  verifyReason?: string | null;
  trustedTime?: string | null;
  signedTime?: string | null;
  signatureCount?: number;
  validSignatureCount?: number;
  signatures?: ISignatureDetail[];
}

export interface ISignResult {
  outputFolder: string;
  totalFiles: number;
  successCount: number;
  failedCount: number;
}

export interface ISignProgress {
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
  file?: ISignFileResult;
  summary?: ISignResult;
}

export interface ISignFileItem {
  name: string;
  fullPath: string;
  sizeBytes: number;
  alreadySigned: boolean;
}

export interface ISignFileList {
  files: ISignFileItem[];
  total: number;
}

export interface ISignPreview {
  index: number;
  total: number;
  hasPrev: boolean;
  hasNext: boolean;
  files: ISignFileItem[];
}

export interface ISignPosition {
  pageNumber: number;
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
  kind?: number;
}

export interface ISignFileConfig {
  path: string;
  positions: ISignPosition[];
}

export interface ISignConfig {
  useCustomPosition: boolean;
  allowOverlapContent: boolean;
  positions?: ISignPosition[] | null;
  fileOverrides?: ISignFileConfig[] | null;
  fontFamily?: string | null;
  fontSize?: number | null;
  textColor?: string | null;
  sealImagePath?: string | null;
  wetSignatureImagePath?: string | null;
}

export interface ISignPositionCheckRequest {
  sourcePaths: string[];
  useCustomPosition: boolean;
  positions?: ISignPosition[] | null;
  fileOverrides?: ISignFileConfig[] | null;
}

export interface ISignPositionFile {
  fileName: string;
  fullPath: string;
  pageCount: number;
  blockCount: number;
  readable: boolean;
  fitted: boolean;
  issues: string[];
}

export interface ISignPositionCheck {
  totalFiles: number;
  fittedCount: number;
  adjustedCount: number;
  unreadableCount: number;
  files: ISignPositionFile[];
}

export interface ISignRequest {
  thumbprint: string;
  sourcePaths: string[];
  reason?: string;
  location?: string;
  useCustomPosition: boolean;
  allowOverlapContent: boolean;
  positions?: ISignPosition[] | null;
  fileOverrides?: ISignFileConfig[] | null;
  fontFamily?: string | null;
  fontSize?: number | null;
  textColor?: string | null;
  sealImagePath?: string | null;
  wetSignatureImagePath?: string | null;
}

export interface ISignFileRow {
  name: string;
  signStatus: 'idle' | 'processing' | 'signed' | 'failed';
  signMessage?: string;
  signedTime?: string | null;
  trustedTime?: string | null;
  signatureCount?: number;
  validSignatureCount?: number;
  signatures?: ISignatureDetail[];
}
