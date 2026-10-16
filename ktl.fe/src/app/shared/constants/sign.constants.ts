export enum ECertSource {
  Local = 0,
  Server = 1,
  UsbToken = 2
}

export const CERT_SOURCE_LABELS: Record<ECertSource, string> = {
  [ECertSource.Local]: 'Máy cá nhân',
  [ECertSource.Server]: 'Máy chủ',
  [ECertSource.UsbToken]: 'USB token'
};

export const CERT_SOURCE_SEVERITIES: Record<ECertSource, 'success' | 'info' | 'secondary'> = {
  [ECertSource.Local]: 'info',
  [ECertSource.Server]: 'secondary',
  [ECertSource.UsbToken]: 'success'
};

export const SIGN_PAGE_URL = '/ky-so';

export const CERT_SIGNABLE_LABEL = 'Ký được';
export const CERT_NOT_SIGNABLE_LABEL = 'Không ký được';

export type SignRowStatus = 'idle' | 'processing' | 'signed' | 'failed';

export const SIGN_BLOCK_MIN_SCALE = 0.5;
export const SIGN_BLOCK_MAX_SCALE = 4;

export const SIGN_FONT_FAMILIES = ['Times New Roman', 'Arial', 'Verdana', 'Georgia', 'Courier New'];
export const SIGN_FONT_FAMILY_DEFAULT = 'Times New Roman';
export const SIGN_FONT_SIZE_DEFAULT = 10;
export const SIGN_FONT_SIZE_MIN = 6;
export const SIGN_FONT_SIZE_MAX = 24;
export const SIGN_FONT_SIZE_STEP = 0.5;
export const SIGN_TEXT_COLOR_DEFAULT = '#000000';
export const SIGN_LINE_HEIGHT_RATIO = 1.15;
export const SIGN_REFERENCE_PAGE_WIDTH_PT = 595;
export const SIGN_REFERENCE_PAGE_HEIGHT_PT = 842;

export const SIGN_DEFAULT_CONFIG = {
  useCustomPosition: false,
  allowOverlapContent: false,
  positions: null,
  fileOverrides: null
} as const;

export enum ESignTemplatePositionKind {
  ChuKy = 0,
  DauDo = 1,
  ChuKyTuoi = 2
}

export const SIGN_TEMPLATE_POSITION_KIND_LABELS: Record<ESignTemplatePositionKind, string> = {
  [ESignTemplatePositionKind.ChuKy]: 'Chữ ký số',
  [ESignTemplatePositionKind.DauDo]: 'Dấu đỏ',
  [ESignTemplatePositionKind.ChuKyTuoi]: 'Chữ ký tươi'
};

export const SIGN_IMAGE_BLOCK_WIDTH_PT = 100;

export const SIGN_TEMPLATE_NEW_ID = 'moi';

export const SIGN_STATUS_LABELS: Record<SignRowStatus, string> = {
  idle: 'Chưa ký',
  processing: 'Đang ký',
  signed: 'Đã ký',
  failed: 'Ký lỗi'
};
