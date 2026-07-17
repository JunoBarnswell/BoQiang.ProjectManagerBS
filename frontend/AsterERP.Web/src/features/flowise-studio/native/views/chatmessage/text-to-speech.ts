export interface FlowiseTextToSpeechPlayback {
  messageId: string;
  utterance: SpeechSynthesisUtterance;
}

export interface SpeakTextMessageOptions {
  messageId: string;
  text: string;
  onEnd: () => void;
  onError: () => void;
}

export function supportsTextToSpeech(): boolean {
  return typeof window !== 'undefined' && 'speechSynthesis' in window && typeof SpeechSynthesisUtterance !== 'undefined';
}

export function speakTextMessage({ messageId, onEnd, onError, text }: SpeakTextMessageOptions): FlowiseTextToSpeechPlayback {
  const utterance = new SpeechSynthesisUtterance(text);
  utterance.onend = onEnd;
  utterance.onerror = onError;
  window.speechSynthesis.speak(utterance);

  return { messageId, utterance };
}

export function stopTextToSpeech(): void {
  if (supportsTextToSpeech()) {
    window.speechSynthesis.cancel();
  }
}
