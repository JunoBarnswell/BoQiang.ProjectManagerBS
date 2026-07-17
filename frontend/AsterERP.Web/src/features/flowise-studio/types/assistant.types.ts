export interface FlowiseAssistantDefinitionDto {
  fileIds: string[];
  instructions?: string | null;
  model?: string | null;
  responseFormat?: string | null;
  temperature?: number | null;
  tools: string[];
  topP?: number | null;
}

export interface FlowiseAssistantDto {
  advancedMetadataJson: string;
  assistantKey: string;
  assistantType: string;
  createdTime: string;
  definition: FlowiseAssistantDefinitionDto;
  description?: string | null;
  id: string;
  name: string;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
}

export interface FlowiseAssistantUpsertRequest {
  advancedMetadataJson?: string | null;
  assistantKey: string;
  assistantType?: string | null;
  definition: FlowiseAssistantDefinitionDto;
  description?: string | null;
  name: string;
  status?: string | null;
  workspaceId?: string | null;
}
