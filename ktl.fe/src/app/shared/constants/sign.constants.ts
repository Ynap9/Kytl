import { ISignConfig } from '../../models/sign.models';

export enum ECertSource {
  Local = 0,
  Server = 1,
  UsbToken = 2
}

export const SIGN_PAGE_URL = '/ky-so';

export type SignRowStatus = 'idle' | 'processing' | 'signed' | 'failed';

export const SIGN_BLOCK_MIN_SCALE = 0.5;
export const SIGN_BLOCK_MAX_SCALE = 4;

export const SIGN_IMAGE_BLOCK_WIDTH_PT = 100;

export const SIGN_FONT_FAMILIES = ['Times New Roman', 'Arial', 'Verdana', 'Georgia', 'Courier New'];
export const SIGN_FONT_SIZE_MIN = 6;
export const SIGN_FONT_SIZE_MAX = 24;
export const SIGN_FONT_SIZE_STEP = 0.5;
export const SIGN_LINE_HEIGHT_RATIO = 1.15;
export const SIGN_REFERENCE_PAGE_WIDTH_PT = 595;
export const SIGN_REFERENCE_PAGE_HEIGHT_PT = 842;
export const SIGN_PAGE_MARGIN_PT = 8;
export const SIGN_SIGNER_PLACEHOLDER = 'Người ký';
export const SIGN_TIME_PREVIEW_FORMAT = 'YYYY-MM-DDTHH:mm:ss.SSSSSSS[+07:00]';

export const SIGN_IMAGE_DARKNESS_MIN = 40;
export const SIGN_IMAGE_DARKNESS_MAX = 250;
export const SIGN_IMAGE_DARKNESS_BRIGHTNESS_FACTOR = 0.5;
export const SIGN_IMAGE_DARKNESS_MIN_BRIGHTNESS = 0.5;
export const SIGN_IMAGE_STROKE_MIN = 0;
export const SIGN_IMAGE_STROKE_MAX = 200;

export const SIGN_CONFIG_STORAGE_KEY = 'ktl_sign_config';

export const SIGN_DEFAULT_CONFIG: ISignConfig = {
  reason: '',
  location: '',
  fontFamily: 'Times New Roman',
  fontSize: 10,
  textColor: '#000000',
  showSignature: true,
  embedSignatureInImage: false,
  sealImagePath: '',
  wetSignatureImagePath: '',
  sealDarkness: 140,
  wetSignatureDarkness: 140,
  wetSignatureStroke: 100,
  wetSignatureColor: null,
  signaturePosition: null,
  stamps: [],
  fileOverrides: []
};

export const SIGN_STATUS_LABELS: Record<SignRowStatus, string> = {
  idle: 'Chưa ký',
  processing: 'Đang ký',
  signed: 'Đã ký',
  failed: 'Ký lỗi'
};
