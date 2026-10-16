export enum EMergeStep {
  Source = 0,
  Mapping = 1,
  Stamp = 2,
  Run = 3
}

export const MERGE_STEPS: { step: EMergeStep; label: string; description: string }[] = [
  { step: EMergeStep.Source, label: 'Nguồn', description: 'Chọn mẫu Word và file dữ liệu Excel, hệ thống tự đọc dữ liệu' },
  { step: EMergeStep.Mapping, label: 'Ghép trường', description: 'Ghép từng trường trong mẫu với một cột Excel rồi kiểm tra dữ liệu' },
  { step: EMergeStep.Stamp, label: 'Xem trước', description: 'Xem văn bản đã nhồi dữ liệu, đóng dấu đỏ và chữ ký tươi nếu cần' },
  { step: EMergeStep.Run, label: 'Tạo file', description: 'Chọn nơi lưu và tạo file PDF cho từng dòng dữ liệu' }
];

export const MERGE_PAGE_TITLE = 'Tạo văn bản';

export enum EMergeStampKind {
  Seal = 1,
  WetSignature = 2
}

export const MERGE_STAMP_KIND_LABELS: Record<EMergeStampKind, string> = {
  [EMergeStampKind.Seal]: 'Dấu đỏ',
  [EMergeStampKind.WetSignature]: 'Chữ ký tươi'
};

export enum EMergeStampScope {
  CurrentPage = 0,
  Repeat = 1
}

export const MERGE_STAMP_SCOPE_OPTIONS: { label: string; hint: string; value: EMergeStampScope }[] = [
  { label: 'Trang hiện tại', hint: 'Chỉ trang đặt khối', value: EMergeStampScope.CurrentPage },
  { label: 'Toàn bộ', hint: 'Lặp sang trang khác', value: EMergeStampScope.Repeat }
];

export enum EMergeStampRepeatMode {
  All = 0,
  Even = 1,
  Odd = 2,
  Custom = 3
}

export const MERGE_STAMP_REPEAT_OPTIONS: { label: string; hint: string; value: EMergeStampRepeatMode }[] = [
  { label: 'Tất cả', hint: 'Mọi trang', value: EMergeStampRepeatMode.All },
  { label: 'Trang chẵn', hint: '2, 4, 6…', value: EMergeStampRepeatMode.Even },
  { label: 'Trang lẻ', hint: '1, 3, 5…', value: EMergeStampRepeatMode.Odd },
  { label: 'Tùy chọn', hint: 'Ví dụ 1,3-5', value: EMergeStampRepeatMode.Custom }
];

export enum EMergeStampEditScope {
  AllPages = 'all',
  CurrentPage = 'page'
}

export const MERGE_STAMP_EDIT_SCOPE_OPTIONS: { label: string; hint: string; value: EMergeStampEditScope }[] = [
  { label: 'Mọi trang đã chọn', hint: 'Các trang cùng đổi', value: EMergeStampEditScope.AllPages },
  { label: 'Chỉ trang đang kéo', hint: 'Trang khác giữ nguyên', value: EMergeStampEditScope.CurrentPage }
];

export const MERGE_PAGE_CHIP_LIMIT = 24;

export const MERGE_CUSTOM_PAGE_PATTERN = /^(\d+)(?:\s*-\s*(\d+))?$/;
export const MERGE_CUSTOM_PAGE_SEPARATOR = ',';

export enum EMergePreviewMode {
  Edit = 'edit',
  Final = 'final'
}

export const MERGE_PREVIEW_MODE_OPTIONS: { label: string; value: EMergePreviewMode; icon: string }[] = [
  { label: 'Đặt vị trí', value: EMergePreviewMode.Edit, icon: 'pi pi-arrows-alt' },
  { label: 'Bản hoàn chỉnh', value: EMergePreviewMode.Final, icon: 'pi pi-eye' }
];

export const MERGE_TEMPLATE_EXTENSION = '.docx';
export const MERGE_DATA_EXTENSION = '.xlsx';

export type MergeRowStatus = 'idle' | 'processing' | 'done' | 'failed';

export const MERGE_ROW_STATUS_LABELS: Record<MergeRowStatus, string> = {
  idle: 'Chờ tạo',
  processing: 'Đang tạo',
  done: 'Đã tạo',
  failed: 'Lỗi'
};

export const MERGE_PROGRESS_TARGET = {
  manifest: 'manifest',
  file: 'file',
  summary: 'summary'
} as const;
