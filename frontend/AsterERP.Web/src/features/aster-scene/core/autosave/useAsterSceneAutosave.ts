import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import { asterSceneApi } from '../../api/asterScene.api';
import { useAsterSceneDocumentStore } from '../../state/documentStore';
import { computeSceneDocumentHash, createClientMutationId, normalizeSceneDocument } from '../scene-document/documentKernel';

export function useAsterSceneAutosave(projectId: string | undefined) {
  const inFlightRef = useRef(false);
  const saveMutation = useMutation({
    mutationFn: async () => {
      const { document, revision } = useAsterSceneDocumentStore.getState();
      if (!document || !projectId) {
        return null;
      }

      const normalizedDocument = normalizeSceneDocument(document);
      const documentHash = await computeSceneDocumentHash(normalizedDocument);
      const response = await asterSceneApi.projects.saveDocument(projectId, {
        clientMutationId: createClientMutationId('autosave'),
        document: normalizedDocument,
        documentHash,
        expectedRevision: revision,
        saveSource: 'Autosave'
      });
      return response.data;
    },
    onSettled: () => {
      inFlightRef.current = false;
    },
    onSuccess: (data) => {
      if (data) {
        useAsterSceneDocumentStore.getState().markSaved(data.revision, data.documentHash);
      }
    }
  });

  useEffect(() => {
    if (!projectId) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      const { dirty, document } = useAsterSceneDocumentStore.getState();
      if (!dirty || !document || inFlightRef.current) {
        return;
      }

      inFlightRef.current = true;
      saveMutation.mutate();
    }, 3000);

    return () => window.clearInterval(timer);
  }, [projectId, saveMutation]);

  return saveMutation;
}
