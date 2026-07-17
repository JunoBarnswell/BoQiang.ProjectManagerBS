export interface FlowiseDocumentStoreLoaderConfigDto {
  advancedConfigJson: string;
  chunkOverlap?: number | null;
  chunkSize?: number | null;
  loaderType?: string | null;
  sourceType?: string | null;
}

export interface FlowiseDocumentStoreListItemDto {
  advancedMetadataJson: string;
  category?: string | null;
  createdTime: string;
  description?: string | null;
  id: string;
  loaderConfig: FlowiseDocumentStoreLoaderConfigDto;
  name: string;
  status: string;
  storeKey: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
}

export interface FlowiseDocumentStoreSaveRequest {
  advancedMetadataJson?: string | null;
  category?: string | null;
  description?: string | null;
  loaderConfig: FlowiseDocumentStoreLoaderConfigDto;
  name: string;
  status?: string | null;
  storeKey: string;
  workspaceId?: string | null;
}

export interface FlowiseDocumentStoreDto {
  chunkCount: number;
  createdTime: string;
  description?: string | null;
  fileCount: number;
  id: string;
  name: string;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseDocumentStoreFileDto {
  createdTime: string;
  fileName: string;
  fileSize: number;
  id: string;
  loaderConfigJson: string;
  loaderType: string;
  status: string;
  storeId: string;
}

export interface FlowiseDocumentStoreChunkDto {
  chunkIndex: number;
  content: string;
  documentId?: string | null;
  id: string;
  metadataJson: string;
  storeId: string;
  tokenCount: number;
}

export interface FlowiseVectorStoreConfigDto {
  embeddingProvider: string;
  id: string;
  recordManagerProvider?: string | null;
  storeId: string;
  vectorProvider: string;
  vectorStoreConfigJson: string;
}

export interface FlowiseDocumentStoreUpsertRequest {
  chatflowId?: string | null;
  flowData: string;
  loaderId?: string | null;
  overrideConfigJson?: string;
  replaceExisting: boolean;
  storeId: string;
}

export interface FlowiseDocumentStoreUpsertHistoryDto {
  addedCount: number;
  chatflowId?: string | null;
  createdTime: string;
  errorMessage?: string | null;
  id: string;
  loaderId?: string | null;
  processedCount: number;
  replacedCount: number;
  requestJson: string;
  resultJson: string;
  skippedCount: number;
  status: string;
  storeId: string;
}

export interface FlowiseDocumentStoreQueryRequest {
  limit?: number;
  query: string;
  storeId: string;
}

export interface FlowiseDocumentStoreQueryResultDto {
  chunks: FlowiseDocumentStoreChunkDto[];
  traceId: string;
}
