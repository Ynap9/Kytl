export const PDF_STAMP_PAGE_TITLE = 'Dấu & chữ ký';

export type PdfStampRowStatus = 'idle' | 'processing' | 'done' | 'failed';

export const PDF_STAMP_ROW_STATUS_LABELS: Record<PdfStampRowStatus, string> = {
  idle: 'Chờ xử lý',
  processing: 'Đang chèn',
  done: 'Đã chèn',
  failed: 'Lỗi'
};

export const PDF_STAMP_PROGRESS_TARGET = {
  manifest: 'manifest',
  file: 'file',
  summary: 'summary'
} as const;
