import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLocale } from '../../../core/i18n/I18nProvider';
import type { PrintScene } from '../types';

type DesignerElement = Record<string, unknown>;
type DesignerTemplateData = Record<string, unknown>;

function textElement(id: string, content: string, x: number, y: number, width: number, height: number, fontSize = 14): DesignerElement {
  return {
    id,
    content,
    height,
    printable: true,
    repeatPerPage: false,
    style: {
      backgroundColor: 'transparent',
      borderColor: 'transparent',
      color: '#111827',
      fontSize,
      fontWeight: fontSize >= 18 ? 700 : 400,
      lineHeight: 1.4
    },
    type: 'text',
    variable: '',
    width,
    x,
    y
  };
}

function variableTextElement(
  id: string,
  content: string,
  variable: string,
  x: number,
  y: number,
  width: number,
  height: number
): DesignerElement {
  return {
    ...textElement(id, content, x, y, width, height),
    variable
  };
}

function listTableElement(): DesignerElement {
  const translate = translateCurrentLocale;
  return {
    autoPaginate: true,
    columns: [
      { field: 'fileName', header: translate('print.systemFile.columns.fileName'), width: 220 },
      { field: 'extension', header: translate('print.systemFile.columns.extension'), width: 70 },
      { field: 'contentType', header: translate('print.systemFile.columns.contentType'), width: 210 },
      { field: 'size', header: translate('print.systemFile.columns.size'), width: 90 },
      { field: 'createdTime', header: translate('print.systemFile.columns.createdTime'), width: 150 },
      { field: 'remark', header: translate('print.systemFile.columns.remark'), width: 110 }
    ],
    columnsVariable: '',
    customScript: '',
    customScriptVariable: '',
    data: [
      {
        contentType: 'application/pdf',
        createdTime: '2026-06-27T12:00:00Z',
        extension: 'pdf',
        fileName: 'file-center-preview.pdf',
        remark: translate('print.systemFile.sampleRowRemark'),
        size: 1024
      }
    ],
    designOmitRows: false,
    embeddedCellTextLayer: 'below',
    embeddedCellTextPosition: 'overlap',
    footerData: [],
    footerDataVariable: '',
    height: 620,
    id: 'system-file-list-table',
    printable: true,
    repeatPerPage: false,
    showFooter: false,
    showHeader: true,
    style: {
      backgroundColor: 'transparent',
      borderColor: '#111827',
      borderStyle: 'solid',
      borderWidth: 1,
      color: '#111827',
      fontSize: 11,
      headerBackgroundColor: '#e5e7eb',
      headerColor: '#111827',
      headerFontSize: 12,
      headerTextAlign: 'left',
      rowHeight: 32,
      textAlign: 'left'
    },
    tfootRepeat: false,
    type: 'table',
    variable: '@rows',
    width: 735,
    x: 30,
    y: 150
  };
}

function detailRowsTableElement(): DesignerElement {
  const translate = translateCurrentLocale;
  return {
    autoPaginate: false,
    columns: [
      { field: 'label', header: translate('print.systemFile.field'), width: 150 },
      { field: 'value', header: translate('print.systemFile.value'), width: 520 }
    ],
    columnsVariable: '',
    customScript: '',
    customScriptVariable: '',
    data: [
      { label: translate('print.systemFile.columns.fileName'), value: '@detail.fileName' },
      { label: translate('print.systemFile.columns.extension'), value: '@detail.extension' },
      { label: translate('print.systemFile.columns.contentType'), value: '@detail.contentType' },
      { label: translate('print.systemFile.columns.size'), value: '@detail.size' },
      { label: translate('print.systemFile.columns.createdTime'), value: '@detail.createdTime' },
      { label: translate('print.systemFile.columns.remark'), value: '@detail.remark' }
    ],
    designOmitRows: false,
    embeddedCellTextLayer: 'below',
    embeddedCellTextPosition: 'overlap',
    footerData: [],
    footerDataVariable: '',
    height: 260,
    id: 'system-file-detail-table',
    printable: true,
    repeatPerPage: false,
    showFooter: false,
    showHeader: true,
    style: {
      backgroundColor: 'transparent',
      borderColor: '#111827',
      borderStyle: 'solid',
      borderWidth: 1,
      color: '#111827',
      fontSize: 12,
      headerBackgroundColor: '#e5e7eb',
      headerColor: '#111827',
      headerFontSize: 12,
      rowHeight: 34,
      textAlign: 'left'
    },
    tfootRepeat: false,
    type: 'table',
    variable: '',
    width: 670,
    x: 60,
    y: 170
  };
}

function createTemplateData(elements: DesignerElement[]): DesignerTemplateData {
  return {
    allowDragOutsideCanvas: false,
    canvasBackground: '#ffffff',
    canvasSize: { height: 1123, width: 794 },
    enableHeaderFooterLineRendering: false,
    footerHeight: 100,
    footerLineColor: '#f87171',
    footerLineSpan: 100,
    footerLineSpanMode: 'percent',
    footerLineStyle: 'dashed',
    footerLineWidth: 1,
    guides: [],
    headerHeight: 100,
    headerLineColor: '#f87171',
    headerLineSpan: 100,
    headerLineSpanMode: 'percent',
    headerLineStyle: 'dashed',
    headerLineWidth: 1,
    pageSpacingX: 0,
    pageSpacingY: 0,
    pages: [{ elements, id: 'system-file-template-page-1' }],
    showFooterLine: false,
    showGrid: true,
    showHeaderLine: false,
    showHistoryPanel: false,
    showMinimap: false,
    unit: 'px',
    watermark: { enabled: false },
    zoom: 1
  };
}

function createListTemplateData(): DesignerTemplateData {
  const translate = translateCurrentLocale;
  return createTemplateData([
    textElement('system-file-list-title', translate('print.systemFile.listTitle'), 30, 34, 360, 34, 22),
    variableTextElement('system-file-list-meta', formatMessage(translate('print.systemFile.listMetaTitle'), { title: '@meta.title' }), '@meta.title', 30, 78, 280, 24),
    variableTextElement('system-file-list-time', formatMessage(translate('print.systemFile.listPrintedAt'), { printedAt: '@meta.printedAt' }), '@meta.printedAt', 330, 78, 320, 24),
    variableTextElement('system-file-list-total', formatMessage(translate('print.systemFile.listTotal'), { total: '@summary.total' }), '@summary.total', 30, 110, 160, 24),
    variableTextElement('system-file-list-page', formatMessage(translate('print.systemFile.listPage'), { pageIndex: '@summary.pageIndex', pageSize: '@summary.pageSize' }), '@summary.pageIndex', 210, 110, 240, 24),
    listTableElement()
  ]);
}

function createDetailTemplateData(): DesignerTemplateData {
  const translate = translateCurrentLocale;
  return createTemplateData([
    textElement('system-file-detail-title', translate('print.systemFile.detailTitle'), 60, 40, 360, 34, 22),
    variableTextElement('system-file-detail-meta', formatMessage(translate('print.systemFile.detailMetaTitle'), { title: '@meta.title' }), '@meta.title', 60, 86, 300, 24),
    variableTextElement('system-file-detail-file-name', formatMessage(translate('print.systemFile.detailFileName'), { fileName: '@detail.fileName' }), '@detail.fileName', 60, 122, 560, 28),
    detailRowsTableElement()
  ]);
}

export function createSystemFilePrintTemplateDefault(scene: PrintScene): DesignerTemplateData {
  return scene === 'detail' ? createDetailTemplateData() : createListTemplateData();
}

export function isEmptyDesignerTemplateData(data: Record<string, unknown> | null | undefined): boolean {
  const pages = data?.pages;
  if (!Array.isArray(pages) || pages.length === 0) {
    return true;
  }

  return pages.every((page) => {
    const elements = (page as { elements?: unknown }).elements;
    return !Array.isArray(elements) || elements.length === 0;
  });
}
