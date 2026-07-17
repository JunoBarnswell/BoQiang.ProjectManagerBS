import type {
  ApplicationDevelopmentPageCreateRequest,
  ApplicationDevelopmentPageParameter,
  ApplicationDevelopmentPageType
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';

export interface CreatePageFormState {
  pageCode: string;
  pageName: string;
  parentPageId: string;
  pageParameters: ApplicationDevelopmentPageParameter[];
  pageType: ApplicationDevelopmentPageType;
}

export interface BuildPageDraftRequestInput {
  form: CreatePageFormState;
  moduleId?: string | null;
  versionId: string;
}

export function createInitialPageForm(seed: string = createCodeSeed()): CreatePageFormState {
  return {
    pageCode: createPageCodeFromName('', seed),
    pageName: '',
    parentPageId: '',
    pageParameters: defaultPageParameters('standard'),
    pageType: 'standard'
  };
}

export function createPageFormWithName(pageName: string, seed: string = createCodeSeed(), current?: CreatePageFormState): CreatePageFormState {
  const pageType = current?.pageType ?? 'standard';
  return {
    pageCode: createPageCodeFromName(pageName, seed),
    pageName,
    parentPageId: current?.parentPageId ?? '',
    pageParameters: current?.pageParameters ?? defaultPageParameters(pageType),
    pageType
  };
}

export function createPageCodeFromName(pageName: string, seed: string = createCodeSeed()): string {
  const normalizedName = pageName
    .trim()
    .replace(/[^A-Za-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .toLowerCase();
  const base = normalizedName && /^[A-Za-z]/.test(normalizedName) ? normalizedName : 'page';
  const safeSeed = seed.replace(/[^A-Za-z0-9]+/g, '').toLowerCase() || createCodeSeed();
  return `${base.slice(0, 96)}_${safeSeed}`;
}

export function buildPageDraftRequest({ form, moduleId, versionId }: BuildPageDraftRequestInput): ApplicationDevelopmentPageCreateRequest {
  const pageCode = form.pageCode.trim();
  const pageName = form.pageName.trim();
  return {
    moduleId: moduleId || undefined,
    pageCode,
    pageName,
    parentPageId: form.parentPageId || undefined,
    pageParameters: form.pageParameters,
    pageType: form.pageType,
    sortOrder: 0,
    versionId
  };
}

export function defaultPageParameters(pageType: ApplicationDevelopmentPageType): ApplicationDevelopmentPageParameter[] {
  if (pageType === 'standard') {
    return [];
  }

  return [
    { code: 'mode', direction: 'input', name: '模式', required: true, valueType: 'string' },
    { code: 'currentRow', direction: 'input', name: '当前行', required: false, valueType: 'json' },
    { code: 'saved', direction: 'output', name: '是否保存', required: false, valueType: 'boolean' },
    { code: 'changedRow', direction: 'output', name: '变更行', required: false, valueType: 'json' }
  ];
}

function createCodeSeed(): string {
  return Date.now().toString(36).slice(-8).toLowerCase();
}
