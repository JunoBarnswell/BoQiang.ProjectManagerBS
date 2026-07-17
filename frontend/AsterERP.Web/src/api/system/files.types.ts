import type { FilterQueryRule, SortQueryRule } from '../queryString';

export interface SystemFileRecordDto {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  relativePath: string;
  createdTime: string;
  remark?: string | null;
  extension: string;
  downloadUrl: string;
  previewUrl: string;
  previewSupported: boolean;
  previewCategory?: string | null;
  previewType?: string | null;
  previewPipeline?: string | null;
}

export interface SystemFileUploadResponse {
  id: string;
  fileName: string;
  downloadUrl: string;
  size: number;
  extension: string;
  previewUrl: string;
  previewSupported: boolean;
  previewCategory?: string | null;
  previewType?: string | null;
  previewPipeline?: string | null;
}

export interface SystemFilePreviewFormatDto {
  extension: string;
  category: string;
  contentType: string;
  viewerType: string;
  previewPipeline: string;
}

export interface SystemFileQuery {
  filters?: FilterQueryRule[];
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  sorts?: SortQueryRule[];
}
