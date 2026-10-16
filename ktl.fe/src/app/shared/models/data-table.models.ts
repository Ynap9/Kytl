export type IColumn = {
  header: string,
  field?: string,
  headerContainerClass?: string,
  headerContainerStyle?: string,
  cellClass?: string,
  cellStyle?: string,
  cellViewType?: string,
  cellRender?: string,
  dateFormat?: string,
  customComponent?: any,
  clickable?: boolean,
  isFrozenRight?: boolean,
  statusSeverityFunction?: (rowData: any) => 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast' | null | undefined;
}

export type ICustomEmit<T> = {
  type: string,
  data: T
}
