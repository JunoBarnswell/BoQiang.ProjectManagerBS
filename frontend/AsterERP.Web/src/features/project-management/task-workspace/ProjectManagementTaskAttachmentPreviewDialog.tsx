import type { SystemFileRecordDto } from '../../../api/system/files.types';
import type { ProjectManagementTaskAttachment } from '../../../api/project-management/projectManagement.types';
import { FilePreviewDialog } from '../../../shared/file-preview/FilePreviewDialog';
import { getFileExtension } from '../../../shared/file-preview/filePreviewUtils';

interface ProjectManagementTaskAttachmentPreviewDialogProps {
  attachment: ProjectManagementTaskAttachment | null;
  error?: string;
  loading: boolean;
  open: boolean;
  previewFile: File | null;
  onClose: () => void;
}

export function ProjectManagementTaskAttachmentPreviewDialog({
  attachment,
  error,
  loading,
  open,
  previewFile,
  onClose
}: ProjectManagementTaskAttachmentPreviewDialogProps) {
  return <FilePreviewDialog
    error={error}
    file={attachment ? toPreviewFile(attachment) : null}
    loading={loading}
    open={open}
    previewFile={previewFile}
    onClose={onClose}
  />;
}

function toPreviewFile(attachment: ProjectManagementTaskAttachment): SystemFileRecordDto {
  return {
    id: attachment.fileId,
    fileName: attachment.fileName,
    contentType: attachment.contentType,
    size: attachment.fileSize,
    relativePath: '',
    createdTime: attachment.createdTime,
    extension: getFileExtension(attachment.fileName),
    downloadUrl: attachment.downloadUrl,
    previewUrl: attachment.previewUrl,
    previewSupported: attachment.previewSupported,
    previewType: attachment.previewType,
    previewPipeline: attachment.previewPipeline
  };
}
