export enum EMergeStep {
  Source = 0,
  Mapping = 1,
  Stamp = 2,
  Run = 3
}

export const MERGE_STEPS: { step: EMergeStep; label: string }[] = [
  { step: EMergeStep.Source, label: 'Nguồn' },
  { step: EMergeStep.Mapping, label: 'Ghép trường' },
  { step: EMergeStep.Stamp, label: 'Xem trước' },
  { step: EMergeStep.Run, label: 'Tạo file' }
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

export const MERGE_STAMP_SCOPE_OPTIONS: { label: string; value: EMergeStampScope }[] = [
  { label: 'Trang hiện tại', value: EMergeStampScope.CurrentPage },
  { label: 'Toàn bộ', value: EMergeStampScope.Repeat }
];

export enum EMergeStampRepeatMode {
  All = 0,
  Even = 1,
  Odd = 2,
  Custom = 3
}

export const MERGE_STAMP_REPEAT_OPTIONS: { label: string; value: EMergeStampRepeatMode }[] = [
  { label: 'Tất cả', value: EMergeStampRepeatMode.All },
  { label: 'Trang chẵn', value: EMergeStampRepeatMode.Even },
  { label: 'Trang lẻ', value: EMergeStampRepeatMode.Odd },
  { label: 'Tùy chọn', value: EMergeStampRepeatMode.Custom }
];

export enum EMergeStampEditScope {
  AllPages = 'all',
  CurrentPage = 'page'
}

export const MERGE_STAMP_EDIT_SCOPE_OPTIONS: { label: string; value: EMergeStampEditScope }[] = [
  { label: 'Mọi trang đã chọn', value: EMergeStampEditScope.AllPages },
  { label: 'Chỉ trang đang kéo', value: EMergeStampEditScope.CurrentPage }
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
