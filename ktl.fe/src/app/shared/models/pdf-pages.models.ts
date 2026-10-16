export interface IPdfPageMetric {
  number: number;
  widthPt: number;
  heightPt: number;
}

export interface IPdfPage extends IPdfPageMetric {
  cssWidth: number;
  cssHeight: number;
}
