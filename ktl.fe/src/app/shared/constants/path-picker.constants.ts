export type PathPickerKind = 'word' | 'excel' | 'pdf' | 'folder' | 'image';

export const PATH_PICKER_EXTENSIONS: Record<PathPickerKind, string[]> = {
  word: ['.docx'],
  excel: ['.xlsx'],
  pdf: ['.pdf'],
  image: ['.png', '.jpg', '.jpeg'],
  folder: []
};

export const PATH_PICKER_DROPPED_LABEL = 'Đã kéo thả vào ứng dụng';
