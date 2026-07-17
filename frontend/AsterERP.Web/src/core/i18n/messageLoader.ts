import type { AppLocale } from '../config/env';

export type MessageBag = Record<string, string>;

const loadedMessages: Partial<Record<AppLocale, MessageBag>> = {};
const loadingMessages: Partial<Record<AppLocale, Promise<MessageBag>>> = {};
let reverseMessageKeyByValue: Map<string, string> | null = null;

const messageLoaders: Record<AppLocale, () => Promise<MessageBag>> = {
  'en-US': async () => {
    const module = await import('./messages.en-US');
    return module.messagesEnUS;
  },
  'zh-CN': async () => {
    const module = await import('./messages.zh-CN');
    return module.messagesZhCN;
  }
};

export function getLoadedMessages(locale: AppLocale): MessageBag | undefined {
  return loadedMessages[locale];
}

export async function loadLocaleMessages(locale: AppLocale): Promise<MessageBag> {
  const loaded = loadedMessages[locale];
  if (loaded) {
    return loaded;
  }

  const loading = loadingMessages[locale];
  if (loading) {
    return loading;
  }

  const nextLoading = messageLoaders[locale]().then((messages) => {
    loadedMessages[locale] = messages;
    if (locale === 'zh-CN') {
      reverseMessageKeyByValue = null;
    }
    return messages;
  });

  loadingMessages[locale] = nextLoading;
  return nextLoading;
}

export function readLoadedMessage(locale: AppLocale, key: string): string | undefined {
  return loadedMessages[locale]?.[key] ?? loadedMessages['zh-CN']?.[key];
}

export function readLoadedLiteralKey(value: string): string | undefined {
  const zhMessages = loadedMessages['zh-CN'];
  if (!zhMessages) {
    return undefined;
  }

  if (!reverseMessageKeyByValue) {
    reverseMessageKeyByValue = new Map<string, string>();
    for (const [key, messageValue] of Object.entries(zhMessages)) {
      if (!reverseMessageKeyByValue.has(messageValue)) {
        reverseMessageKeyByValue.set(messageValue, key);
      }
    }
  }

  return reverseMessageKeyByValue.get(value);
}
