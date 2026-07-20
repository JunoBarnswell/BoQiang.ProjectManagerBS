import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';

import { appEnv, type AppLocale } from '../config/env';

import {
  getLoadedMessages,
  loadLocaleMessages,
  readLoadedLiteralKey,
  readLoadedMessage
} from './messageLoader';
import type { MessageBag } from './messageLoader';

export interface I18nContextValue {
  locale: AppLocale;
  setLocale: (locale: AppLocale) => void;
  translate: (key: string) => string;
}

export const localeStorageKey = 'astererp.locale';
const I18nContext = createContext<I18nContextValue | null>(null);

function loadLocale(): AppLocale {
  if (typeof window === 'undefined' || !window.localStorage) {
    return appEnv.defaultLocale;
  }

  const savedLocale = window.localStorage.getItem(localeStorageKey) as AppLocale | null;
  return savedLocale === 'en-US' || savedLocale === 'zh-CN' ? savedLocale : appEnv.defaultLocale;
}

export function translateValue(locale: AppLocale, key: string): string {
  return readLoadedMessage(locale, key) ?? key;
}

export function translateLiteral(locale: AppLocale, value: string): string {
  if (!value) {
    return value;
  }

  const messageKey = readLoadedLiteralKey(value);
  return messageKey ? translateValue(locale, messageKey) : value;
}

export function getCurrentLocale(): AppLocale {
  return loadLocale();
}

export function translateCurrentLocale(key: string): string {
  return translateValue(getCurrentLocale(), key);
}

export function translateCurrentLiteral(value: string): string {
  return translateLiteral(getCurrentLocale(), value);
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocale] = useState<AppLocale>(() => loadLocale());
  const [loadedCatalogs, setLoadedCatalogs] = useState<Partial<Record<AppLocale, MessageBag>>>(() => ({
    'en-US': getLoadedMessages('en-US'),
    'zh-CN': getLoadedMessages('zh-CN')
  }));
  const activeMessages = loadedCatalogs[locale];

  useEffect(() => {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(localeStorageKey, locale);
    }

    if (typeof document !== 'undefined') {
      document.documentElement.lang = locale;
    }
  }, [locale]);

  useEffect(() => {
    let isDisposed = false;

    const loadMessages = async () => {
      const [nextLocaleMessages, fallbackMessages] = await Promise.all([
        loadLocaleMessages(locale),
        loadLocaleMessages('zh-CN')
      ]);

      if (isDisposed) {
        return;
      }

      setLoadedCatalogs((current) => ({
        ...current,
        [locale]: nextLocaleMessages,
        'zh-CN': fallbackMessages
      }));
    };

    void loadMessages();

    return () => {
      isDisposed = true;
    };
  }, [locale]);

  if (!activeMessages) {
    return null;
  }

  return (
    <I18nContext.Provider
      value={{
        locale,
        setLocale,
        translate: (key: string) => activeMessages[key] ?? loadedCatalogs['zh-CN']?.[key] ?? key
      }}
    >
      {children}
    </I18nContext.Provider>
  );
}

export function useI18n() {
  const context = useContext(I18nContext);

  if (!context) {
    throw new Error('useI18n must be used inside I18nProvider');
  }

  return context;
}
