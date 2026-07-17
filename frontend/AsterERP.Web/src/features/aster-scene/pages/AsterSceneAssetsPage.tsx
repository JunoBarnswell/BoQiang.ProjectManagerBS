import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackagePlus, Trash2, UploadCloud } from 'lucide-react';
import { useState } from 'react';

import { useI18n } from '@/core/i18n/I18nProvider';
import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import { asterScenePermissions } from '../model/permissions';

export function AsterSceneAssetsPage() {
  const { translate: t } = useI18n();
  const queryClient = useQueryClient();
  const [projectId, setProjectId] = useState('');
  const [assetType, setAssetType] = useState('model');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileName, setFileName] = useState('');
  const [sourceUrl, setSourceUrl] = useState('');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadStatus, setUploadStatus] = useState('');
  const [fileInputKey, setFileInputKey] = useState(0);
  const assetsQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.assets.list({ pageSize: 100, projectId: projectId || undefined }, signal),
    queryKey: ['asterscene', 'assets', projectId]
  });
  const registerMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.assets.register({
        assetType: 'image',
        clientMutationId: createClientMutationId('asset'),
        fileName,
        projectId,
        sourceUrl
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['asterscene', 'assets'] });
      setFileName('');
      setSourceUrl('');
    }
  });
  const uploadMutation = useMutation({
    mutationFn: async () => {
      if (!projectId || !selectedFile) {
        throw new Error(t('asterscene.assets.projectFileRequired'));
      }

      setUploadProgress(0);
      setUploadStatus(t('asterscene.assets.hashingFile'));
      const fileChecksum = await sha256Hex(selectedFile, t('asterscene.assets.shaUnavailable'));
      const totalChunks = Math.max(1, Math.ceil(selectedFile.size / UploadChunkSize));
      const uploadClientMutationId = createUploadMutationId(projectId, assetType, selectedFile);
      const completeClientMutationId = `${uploadClientMutationId}:complete:${fileChecksum}`;
      const sessionEnvelope = await asterSceneApi.assets.startUpload({
        assetType,
        checksum: fileChecksum,
        clientMutationId: uploadClientMutationId,
        contentType: selectedFile.type || null,
        fileName: selectedFile.name,
        projectId,
        sizeBytes: selectedFile.size,
        totalChunks
      });
      const session = sessionEnvelope.data;
      let uploadedChunks = Math.min(session.uploadedChunks, totalChunks);

      for (let chunkIndex = uploadedChunks; chunkIndex < totalChunks; chunkIndex += 1) {
        const start = chunkIndex * UploadChunkSize;
        const chunk = selectedFile.slice(start, Math.min(selectedFile.size, start + UploadChunkSize));
        setUploadStatus(`${t('asterscene.assets.uploadingChunk')} ${chunkIndex + 1}/${totalChunks}`);
        const chunkChecksum = await sha256Hex(chunk, t('asterscene.assets.shaUnavailable'));
        const chunkEnvelope = await asterSceneApi.assets.uploadChunk(session.uploadId, chunkIndex, chunk, chunkChecksum);
        uploadedChunks = Math.min(chunkEnvelope.data.uploadedChunks, totalChunks);
        setUploadProgress(Math.round((uploadedChunks / totalChunks) * 90));
      }

      setUploadStatus(t('asterscene.assets.completingUpload'));
      const completed = await asterSceneApi.assets.completeUpload(session.uploadId, {
        clientMutationId: completeClientMutationId,
        metadata: {
          chunkSize: UploadChunkSize,
          originalName: selectedFile.name,
          totalChunks
        }
      });
      setUploadProgress(100);
      setUploadStatus(t('asterscene.assets.uploadCompleted'));
      return completed.data;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['asterscene', 'assets'] });
      setSelectedFile(null);
      setFileInputKey((value) => value + 1);
    }
  });
  const deleteMutation = useMutation({
    mutationFn: (assetId: string) => asterSceneApi.assets.delete(assetId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asterscene', 'assets'] })
  });
  const assets = assetsQuery.data?.data.items ?? [];

  return (
    <AsterSceneLayout eyebrow={t('asterscene.assets.eyebrow')} title={t('asterscene.assets.title')}>
      <section className="as-band as-band--split">
        <form
          className="as-panel"
          onSubmit={(event) => {
            event.preventDefault();
            uploadMutation.mutate();
          }}
        >
          <h2>{t('asterscene.assets.chunkUpload')}</h2>
          <label>
            {t('asterscene.assets.projectId')}
            <input value={projectId} onChange={(event) => setProjectId(event.target.value)} />
          </label>
          <label>
            {t('asterscene.assets.assetType')}
            <select value={assetType} onChange={(event) => setAssetType(event.target.value)}>
              <option value="model">{t('asterscene.assetType.model')}</option>
              <option value="texture">{t('asterscene.assetType.texture')}</option>
              <option value="material">{t('asterscene.assetType.material')}</option>
              <option value="panorama">{t('asterscene.assetType.panorama')}</option>
              <option value="video">{t('asterscene.assetType.video')}</option>
              <option value="audio">{t('asterscene.assetType.audio')}</option>
              <option value="image">{t('asterscene.assetType.image')}</option>
              <option value="decal">{t('asterscene.assetType.decal')}</option>
              <option value="prefab">{t('asterscene.assetType.prefab')}</option>
              <option value="document">{t('asterscene.assetType.document')}</option>
              <option value="mesh">{t('asterscene.assetType.mesh')}</option>
              <option value="hdri">{t('asterscene.assetType.hdri')}</option>
              <option value="preset">{t('asterscene.assetType.preset')}</option>
            </select>
          </label>
          <label>
            {t('asterscene.assets.file')}
            <input
              key={fileInputKey}
              onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
              type="file"
            />
          </label>
          {selectedFile ? (
            <div className="as-upload-meter">
              <strong>{selectedFile.name}</strong>
              <span>{formatBytes(selectedFile.size)}</span>
            </div>
          ) : null}
          {uploadMutation.isPending || uploadStatus ? (
            <div className="as-upload-meter">
              <progress max={100} value={uploadProgress} />
              <span>{uploadStatus || t('asterscene.common.ready')}</span>
            </div>
          ) : null}
          {uploadMutation.isError ? <AsterSceneState title={getErrorMessage(uploadMutation.error, t('asterscene.assets.operationFailed'))} /> : null}
          <PermissionButton
            className="as-button as-button--primary"
            code={asterScenePermissions.assetUpload}
            disabled={!projectId || !selectedFile || uploadMutation.isPending}
            iconStart={false}
            type="submit"
          >
            <UploadCloud size={16} /> {t('asterscene.assets.upload')}
          </PermissionButton>
        </form>
        <form
          className="as-panel"
          onSubmit={(event) => {
            event.preventDefault();
            registerMutation.mutate();
          }}
        >
          <h2>{t('asterscene.assets.registerRuntimeUrl')}</h2>
          <label>
            {t('asterscene.assets.projectId')}
            <input value={projectId} onChange={(event) => setProjectId(event.target.value)} />
          </label>
          <label>
            {t('asterscene.assets.fileName')}
            <input value={fileName} onChange={(event) => setFileName(event.target.value)} />
          </label>
          <label>
            {t('asterscene.assets.sourceUrl')}
            <input value={sourceUrl} onChange={(event) => setSourceUrl(event.target.value)} placeholder="/uploads/..." />
          </label>
          <PermissionButton
            className="as-button as-button--primary"
            code={asterScenePermissions.assetUpload}
            disabled={!projectId || !fileName || !sourceUrl}
            iconStart={false}
            type="submit"
          >
            <PackagePlus size={16} /> {t('asterscene.assets.register')}
          </PermissionButton>
        </form>
      </section>
      <section className="as-band">
        <section className="as-panel as-panel--wide">
          <h2>{t('asterscene.assets.inventory')}</h2>
          {assetsQuery.isLoading ? <AsterSceneState title={t('asterscene.assets.loading')} /> : null}
          {assetsQuery.isError ? <AsterSceneState title={t('asterscene.assets.failed')} /> : null}
          <div className="as-table">
            {assets.map((asset) => (
              <div className="as-table__row" key={asset.id}>
                <div>
                  <strong>{asset.fileName}</strong>
                  <span>{asset.assetCode}</span>
                </div>
                <span>{asset.assetType}</span>
                <span>{asset.status}</span>
                <PermissionButton
                  className="as-icon-button"
                  code={asterScenePermissions.assetDelete}
                  iconStart={false}
                  onClick={() => deleteMutation.mutate(asset.id)}
                  title={t('asterscene.assets.deleteAsset')}
                  type="button"
                >
                  <Trash2 size={16} />
                </PermissionButton>
              </div>
            ))}
          </div>
        </section>
      </section>
    </AsterSceneLayout>
  );
}

const UploadChunkSize = 8 * 1024 * 1024;

async function sha256Hex(blob: Blob, unavailableMessage: string): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) {
    throw new Error(unavailableMessage);
  }

  const digest = await subtle.digest('SHA-256', await blob.arrayBuffer());
  return Array.from(new Uint8Array(digest))
    .map((value) => value.toString(16).padStart(2, '0'))
    .join('');
}

function createUploadMutationId(projectId: string, assetType: string, file: File): string {
  return ['asset-upload', projectId, assetType, file.name, file.size, file.lastModified].join(':');
}

function formatBytes(value: number): string {
  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
