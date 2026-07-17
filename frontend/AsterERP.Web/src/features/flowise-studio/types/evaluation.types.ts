export interface FlowiseDatasetSchemaDto {
  advancedSchemaJson: string;
  expectedOutputColumns: string[];
  inputColumns: string[];
}

export interface FlowiseDatasetListItemDto {
  advancedMetadataJson: string;
  category?: string | null;
  createdTime: string;
  datasetKey: string;
  description?: string | null;
  id: string;
  name: string;
  rowCount: number;
  schema: FlowiseDatasetSchemaDto;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseDatasetSaveRequest {
  advancedMetadataJson?: string | null;
  category?: string | null;
  datasetKey: string;
  description?: string | null;
  name: string;
  schema: FlowiseDatasetSchemaDto;
  status?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseDatasetDto {
  createdTime: string;
  description?: string | null;
  id: string;
  name: string;
  rowCount: number;
  status: string;
}

export interface FlowiseDatasetRowDto {
  actualOutput?: string | null;
  datasetId: string;
  expectedOutput?: string | null;
  id: string;
  input: string;
  metadataJson: string;
}

export interface FlowiseDatasetCsvImportDto {
  datasetId: string;
  firstRowHeaders: boolean;
  importedRows: number;
}

export interface FlowiseEvaluatorDto {
  createdTime: string;
  id: string;
  name: string;
  promptTemplate: string;
  provider: string;
  status: string;
}

export interface FlowiseEvaluatorDefinitionDto {
  advancedConfigJson: string;
  gradingMode?: string | null;
  model?: string | null;
  promptTemplate?: string | null;
  provider?: string | null;
}

export interface FlowiseEvaluatorListItemDto {
  advancedMetadataJson: string;
  createdTime: string;
  definition: FlowiseEvaluatorDefinitionDto;
  description?: string | null;
  evaluatorKey: string;
  evaluatorType?: string | null;
  id: string;
  name: string;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseEvaluatorSaveRequest {
  advancedMetadataJson?: string | null;
  definition: FlowiseEvaluatorDefinitionDto;
  description?: string | null;
  evaluatorKey: string;
  evaluatorType?: string | null;
  name: string;
  status?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseEvaluationDto {
  createdTime: string;
  datasetId: string;
  evaluatorId: string;
  id: string;
  name: string;
  status: string;
  targetFlowId: string;
  versionNo: number;
}

export interface FlowiseEvaluationDefinitionDto {
  datasetId: string;
  evaluatorId: string;
  model?: string | null;
  runConfigJson: string;
  targetFlowId: string;
}

export interface FlowiseEvaluationListItemDto {
  advancedMetadataJson: string;
  category?: string | null;
  createdTime: string;
  definition: FlowiseEvaluationDefinitionDto;
  description?: string | null;
  evaluationKey: string;
  id: string;
  name: string;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseEvaluationSaveRequest {
  advancedMetadataJson?: string | null;
  category?: string | null;
  definition: FlowiseEvaluationDefinitionDto;
  description?: string | null;
  evaluationKey: string;
  name: string;
  status?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseEvaluationResultDto {
  averageLatencyMs: number;
  id: string;
  metricsJson: string;
  passRate: number;
  resultRowsJson: string;
  status: string;
  totalTokens: number;
  versionNo: number;
}
