/**
 * Monaco Editor 懒加载门面
 *
 * 将 `import * as monaco from 'monaco-editor'` 的同步全量加载改为按需动态加载，
 * 并只加载 editor API 子入口，避免默认语言包进入业务编辑器首个模块图。
 *
 * 用法：在调用 monaco API 之前先 await：
 *   const monaco = await getMonaco();
 *   monaco.editor.create(...)
 */

import type * as Monaco from 'monaco-editor';

type MonacoModule = typeof Monaco;
type MonacoLoadPromise = Promise<MonacoModule>;

let monacoPromise: MonacoLoadPromise | null = null;

/** 异步获取 monaco-editor 实例（单例，只加载一次） */
export async function getMonaco(): MonacoLoadPromise {
  if (!monacoPromise) {
    monacoPromise = import('monaco-editor/esm/vs/editor/editor.api');
  }
  return monacoPromise;
}

export type { Monaco };
