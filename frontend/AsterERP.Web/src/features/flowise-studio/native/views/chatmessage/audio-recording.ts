import type { FlowisePredictionUpload } from '../../../types/prediction.types';

export interface FlowiseAudioRecordingSession {
  chunks: Blob[];
  recorder: MediaRecorder;
  stream: MediaStream;
}

export function supportsAudioRecording(): boolean {
  return typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getUserMedia === 'function' && typeof MediaRecorder !== 'undefined';
}

export async function startAudioRecording(): Promise<FlowiseAudioRecordingSession> {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  const recorder = new MediaRecorder(stream);
  const chunks: Blob[] = [];

  recorder.ondataavailable = (event) => {
    if (event.data.size > 0) {
      chunks.push(event.data);
    }
  };
  if (isSafariBrowser()) {
    recorder.start(1000);
  } else {
    recorder.start();
  }

  return { chunks, recorder, stream };
}

export function stopAudioRecording(session: FlowiseAudioRecordingSession, readUploadFile: (file: File) => Promise<FlowisePredictionUpload>): Promise<FlowisePredictionUpload> {
  return new Promise((resolve, reject) => {
    session.recorder.onerror = () => reject(new Error());
    session.recorder.onstop = async () => {
      try {
        const mime = session.recorder.mimeType || 'audio/webm';
        const blob = new Blob(session.chunks, { type: mime });
        const file = new File([blob], `flowise-audio-${Date.now()}.webm`, { type: mime });
        resolve(await readUploadFile(file));
      } catch (error) {
        reject(error);
      } finally {
        cleanupAudioRecording(session);
      }
    };
    session.recorder.stop();
  });
}

export function cancelAudioRecording(session: FlowiseAudioRecordingSession | null): void {
  if (!session) {
    return;
  }

  if (session.recorder.state !== 'inactive') {
    session.recorder.ondataavailable = null;
    session.recorder.onstop = null;
    session.recorder.onerror = null;
    session.recorder.stop();
  }
  session.chunks.length = 0;
  cleanupAudioRecording(session);
}

export function cleanupAudioRecording(session: FlowiseAudioRecordingSession | null): void {
  session?.stream.getTracks().forEach((track) => track.stop());
}

function isSafariBrowser(): boolean {
  if (typeof navigator === 'undefined') {
    return false;
  }

  return /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
}
