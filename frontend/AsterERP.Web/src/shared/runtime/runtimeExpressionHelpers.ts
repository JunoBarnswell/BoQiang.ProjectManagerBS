import type { RuntimeExpressionHelperDto } from '../../api/runtime/runtime.types';

import { runtimeLog } from './runtimeLog';

const runtimeHelperNameAliases = new Map<string, string>([
  ['arrayavg', 'average'],
  ['arraycount', 'count'],
  ['arraydistinct', 'distinct'],
  ['arraydistinctby', 'uniqueBy'],
  ['arrayfind', 'findByField'],
  ['arrayfirst', 'first'],
  ['arraygroupby', 'groupBy'],
  ['arraylast', 'last'],
  ['arraymax', 'maxBy'],
  ['arraymin', 'minBy'],
  ['arraysum', 'sum'],
  ['base64decode', 'base64Decode'],
  ['base64encode', 'base64Encode'],
  ['camelcase', 'camelCase'],
  ['dateToTimestamp'.toLowerCase(), 'timestamp'],
  ['decimaladd', 'add'],
  ['decimaldiv', 'divide'],
  ['decimalmul', 'multiply'],
  ['decimalsub', 'subtract'],
  ['decodeuri', 'decodeUri'],
  ['decodeurl', 'decodeUri'],
  ['decodeurlcomponent', 'decodeUri'],
  ['encodeuri', 'encodeUri'],
  ['encodeurl', 'encodeUri'],
  ['encodeurlcomponent', 'encodeUri'],
  ['endswith', 'endsWith'],
  ['equalsignorecase', 'equals'],
  ['formatcurrency', 'currency'],
  ['formatdate', 'formatDate'],
  ['formatdatetime', 'formatDate'],
  ['formatdecimal', 'fixed'],
  ['formatpercent', 'percent'],
  ['fromtimestamp', 'fromTimestamp'],
  ['filtercontains', 'filterContains'],
  ['filterequals', 'filterEquals'],
  ['findbyfield', 'findByField'],
  ['getday', 'day'],
  ['getmonth', 'month'],
  ['getvalue', 'objectPath'],
  ['getyear', 'year'],
  ['groupby', 'groupBy'],
  ['isempty', 'isEmpty'],
  ['isarray', 'isArray'],
  ['isboolean', 'isBoolean'],
  ['isdate', 'isDate'],
  ['isemail', 'isEmail'],
  ['isguid', 'isGuid'],
  ['isinteger', 'isInteger'],
  ['isjson', 'isJson'],
  ['isnotblank', 'isNotEmpty'],
  ['isnotempty', 'isNotEmpty'],
  ['isnumber', 'isNumber'],
  ['isobject', 'isObject'],
  ['isphone', 'isPhone'],
  ['isstring', 'isString'],
  ['issafeinteger', 'isInteger'],
  ['isurl', 'isUrl'],
  ['jsonparse', 'parseJson'],
  ['jsonpretty', 'stringifyJson'],
  ['jsonstringify', 'stringifyJson'],
  ['kebabcase', 'kebabCase'],
  ['keepchinese', 'onlyChinese'],
  ['keepletters', 'onlyLetters'],
  ['keepnumbers', 'onlyDigits'],
  ['ltrim', 'ltrim'],
  ['mapfield', 'mapField'],
  ['maskcustom', 'maskText'],
  ['objectkeys', 'objectKeys'],
  ['objectpath', 'objectPath'],
  ['objectvalues', 'objectValues'],
  ['omitfields', 'omitFields'],
  ['onlychinese', 'onlyChinese'],
  ['onlydigits', 'onlyDigits'],
  ['onlyletters', 'onlyLetters'],
  ['padend', 'padRight'],
  ['padleft', 'padLeft'],
  ['padright', 'padRight'],
  ['padstart', 'padLeft'],
  ['parsejson', 'parseJson'],
  ['pickfields', 'pickFields'],
  ['pow', 'power'],
  ['removelinebreaks', 'removeLineBreaks'],
  ['removespaces', 'removeSpaces'],
  ['removespecialchars', 'stripHtml'],
  ['roundto', 'round'],
  ['rtrim', 'rtrim'],
  ['snakecase', 'snakeCase'],
  ['startswith', 'startsWith'],
  ['stringifyjson', 'stringifyJson'],
  ['sortby', 'sortBy'],
  ['timestampToDate'.toLowerCase(), 'fromTimestamp'],
  ['tofixed', 'fixed'],
  ['tofloat', 'toNumber'],
  ['toint', 'toInteger'],
  ['tointeger', 'toInteger'],
  ['tolower', 'lower'],
  ['tonumber', 'toNumber'],
  ['toobject', 'toObject'],
  ['tostring', 'toString'],
  ['toupper', 'upper'],
  ['trimend', 'rtrim'],
  ['trimstart', 'ltrim'],
  ['uniqueby', 'uniqueBy'],
  ['maxby', 'maxBy'],
  ['minby', 'minBy'],
  ['normalizewhitespace', 'normalizeWhitespace']
]);

export function applyRuntimeHelper(value: unknown, helper: RuntimeExpressionHelperDto): unknown {
  const args = helper.args ?? {};
  const helperName = normalizeRuntimeHelperName(helper.name);
  switch (helperName) {
    case 'trim':
      return text(value).trim();
    case 'ltrim':
      return text(value).trimStart();
    case 'rtrim':
      return text(value).trimEnd();
    case 'normalizeWhitespace':
      return text(value).replace(/\s+/g, ' ').trim();
    case 'upper':
      return text(value).toUpperCase();
    case 'lower':
      return text(value).toLowerCase();
    case 'length':
      return Array.isArray(value) ? value.length : text(value).length;
    case 'substring':
      return substring(text(value), numberArg(args.start, 0), optionalNumberArg(args.length));
    case 'left':
      return text(value).slice(0, numberArg(args.length, 0));
    case 'right':
      return right(text(value), numberArg(args.length, 0));
    case 'replace':
      return text(value).replaceAll(text(args.search ?? args.oldValue), text(args.replace ?? args.newValue));
    case 'remove':
      return text(value).replaceAll(text(args.value), '');
    case 'prefix':
      return `${text(args.prefix)}${text(value)}`;
    case 'suffix':
      return `${text(value)}${text(args.suffix)}`;
    case 'padLeft':
      return text(value).padStart(numberArg(args.length, text(value).length), padChar(args.char));
    case 'padRight':
      return text(value).padEnd(numberArg(args.length, text(value).length), padChar(args.char));
    case 'splitTake':
      return splitTake(value, args);
    case 'defaultIfEmpty':
      return isRuntimeValueEmpty(value) ? args.value ?? '' : value;
    case 'capitalize':
      return capitalize(text(value));
    case 'titleCase':
      return words(value).map(capitalize).join(' ');
    case 'camelCase':
      return toDelimitedCase(value, 'camel');
    case 'snakeCase':
      return toDelimitedCase(value, 'snake');
    case 'kebabCase':
      return toDelimitedCase(value, 'kebab');
    case 'removeSpaces':
      return text(value).replace(/\s+/g, '');
    case 'removeLineBreaks':
      return text(value).replace(/[\r\n]+/g, '');
    case 'stripHtml':
      return text(value).replace(/<[^>]*>/g, '');
    case 'onlyDigits':
      return text(value).replace(/\D+/g, '');
    case 'onlyLetters':
      return text(value).replace(/[^A-Za-z]+/g, '');
    case 'truncate':
      return truncate(text(value), numberArg(args.length, 0), text(args.suffix || '...'));
    case 'textBefore':
      return textBefore(value, args.separator);
    case 'textAfter':
      return textAfter(value, args.separator);
    case 'textBetween':
      return textBetween(value, args.start, args.end);
    case 'ensurePrefix':
      return ensureAffix(value, args.prefix, 'prefix');
    case 'ensureSuffix':
      return ensureAffix(value, args.suffix, 'suffix');
    case 'removePrefix':
      return removeAffix(value, args.prefix, 'prefix');
    case 'removeSuffix':
      return removeAffix(value, args.suffix, 'suffix');
    case 'repeat':
      return text(value).repeat(Math.max(numberArg(args.count, 0), 0));
    case 'takeLines':
      return normalizeNewlines(value).split('\n').slice(0, numberArg(args.count, 0)).join('\n');
    case 'normalizeNewlines':
      return normalizeNewlines(value);
    case 'containsChinese':
      return /[\u4e00-\u9fa5]/.test(text(value));
    case 'onlyChinese':
      return text(value).replace(/[^\u4e00-\u9fa5]+/g, '');
    case 'toString':
      return text(value);
    case 'toNumber':
      return toNumber(value);
    case 'toBoolean':
      return toBoolean(value);
    case 'toInteger':
      return Math.trunc(toNumber(value));
    case 'toArray':
      return toArray(value);
    case 'toObject':
      return toObject(value);
    case 'nullIfEmpty':
    case 'emptyToNull':
      return isRuntimeValueEmpty(value) ? null : value;
    case 'parseCurrency':
      return parseCurrency(value);
    case 'encodeUri':
      return encodeURIComponent(text(value));
    case 'decodeUri':
      return safeDecodeUri(value);
    case 'base64Encode':
      return base64Encode(value);
    case 'base64Decode':
      return base64Decode(value);
    case 'abs':
      return Math.abs(toNumber(value));
    case 'round':
      return round(toNumber(value), numberArg(args.digits, 0));
    case 'ceil':
      return Math.ceil(toNumber(value));
    case 'floor':
      return Math.floor(toNumber(value));
    case 'fixed':
      return round(toNumber(value), numberArg(args.digits, 2)).toFixed(numberArg(args.digits, 2));
    case 'currency':
      return new Intl.NumberFormat('zh-CN', { currency: text(args.currency || args.symbol || 'CNY'), style: 'currency' }).format(toNumber(value));
    case 'percent':
      return `${round(toNumber(value) * 100, numberArg(args.digits, 2)).toFixed(numberArg(args.digits, 2))}%`;
    case 'add':
      return toNumber(value) + toNumber(args.value);
    case 'subtract':
      return toNumber(value) - toNumber(args.value);
    case 'multiply':
      return toNumber(value) * toNumber(args.value);
    case 'divide':
      return divide(toNumber(value), toNumber(args.value));
    case 'clamp':
      return clamp(toNumber(value), optionalNumberArg(args.min), optionalNumberArg(args.max));
    case 'min':
      return Math.min(toNumber(value), toNumber(args.value));
    case 'max':
      return Math.max(toNumber(value), toNumber(args.value));
    case 'modulo':
      return modulo(toNumber(value), toNumber(args.value));
    case 'power':
      return toNumber(value) ** toNumber(args.value);
    case 'negate':
      return -toNumber(value);
    case 'parseDate':
      return parseDate(value)?.toISOString() ?? '';
    case 'formatDate':
      return formatDate(value, text(args.format || 'yyyy-MM-dd'));
    case 'now':
      return new Date().toISOString();
    case 'addDays':
      return addDate(value, 'day', numberArg(args.days, 0));
    case 'addHours':
      return addDate(value, 'hour', numberArg(args.hours, 0));
    case 'addMinutes':
      return addDate(value, 'minute', numberArg(args.minutes, 0));
    case 'addSeconds':
      return addDate(value, 'second', numberArg(args.seconds, 0));
    case 'addMonths':
      return addDate(value, 'month', numberArg(args.months, 0));
    case 'addYears':
      return addDate(value, 'year', numberArg(args.years, 0));
    case 'addWeeks':
      return addDate(value, 'day', numberArg(args.weeks, 0) * 7);
    case 'startOfDay':
      return dayBoundary(value, 'start');
    case 'endOfDay':
      return dayBoundary(value, 'end');
    case 'startOfMonth':
      return periodBoundary(value, 'month', 'start');
    case 'endOfMonth':
      return periodBoundary(value, 'month', 'end');
    case 'startOfYear':
      return periodBoundary(value, 'year', 'start');
    case 'endOfYear':
      return periodBoundary(value, 'year', 'end');
    case 'year':
      return parseDate(value)?.getFullYear() ?? '';
    case 'month':
      return parseDate(value) ? parseDate(value)!.getMonth() + 1 : '';
    case 'day':
      return parseDate(value)?.getDate() ?? '';
    case 'today':
      return dayBoundary(new Date(), 'start');
    case 'yesterday':
      return addDate(dayBoundary(new Date(), 'start'), 'day', -1);
    case 'tomorrow':
      return addDate(dayBoundary(new Date(), 'start'), 'day', 1);
    case 'weekOfYear':
      return weekOfYear(value);
    case 'weekday':
      return parseDate(value)?.getDay() ?? '';
    case 'quarter':
      return quarter(value);
    case 'formatQuarter':
      return formatQuarter(value);
    case 'ageYears':
      return ageYears(value);
    case 'diffDays':
      return diffDate(value, args.value, 'day');
    case 'diffHours':
      return diffDate(value, args.value, 'hour');
    case 'diffMinutes':
      return diffDate(value, args.value, 'minute');
    case 'diffSeconds':
      return diffDate(value, args.value, 'second');
    case 'timestamp':
      return parseDate(value)?.getTime() ?? 0;
    case 'fromTimestamp':
      return fromTimestamp(value);
    case 'jsonPath':
      return readRuntimePath(value, text(args.path).replace(/^\$\./, ''));
    case 'parseJson':
      return parseJson(value);
    case 'stringifyJson':
      return JSON.stringify(value ?? null);
    case 'objectKeys':
      return objectKeys(value);
    case 'objectValues':
      return objectValues(value);
    case 'first':
      return Array.isArray(value) ? value[0] : undefined;
    case 'last':
      return Array.isArray(value) ? value.at(-1) : undefined;
    case 'nth':
      return toArray(value)[numberArg(args.index, 0)];
    case 'count':
      return Array.isArray(value) || typeof value === 'string' ? value.length : 0;
    case 'take':
      return toArray(value).slice(0, numberArg(args.count, 0));
    case 'skip':
      return toArray(value).slice(numberArg(args.count, 0));
    case 'slice':
      return toArray(value).slice(numberArg(args.start, 0), numberArg(args.start, 0) + numberArg(args.count, toArray(value).length));
    case 'page':
      return pageItems(value, args.pageIndex, args.pageSize);
    case 'reverse':
      return [...toArray(value)].reverse();
    case 'distinct':
      return distinct(value);
    case 'compact':
      return toArray(value).filter((item) => !isRuntimeValueEmpty(item));
    case 'filterNotEmpty':
      return toArray(value).filter((item) => !isRuntimeValueEmpty(item));
    case 'emptyArrayIfNull':
      return isRuntimeValueEmpty(value) ? [] : toArray(value);
    case 'join':
      return Array.isArray(value) ? value.join(text(args.separator ?? ',')) : text(value);
    case 'mapField':
      return Array.isArray(value) ? value.map((item) => readRuntimePath(item, text(args.field))) : [];
    case 'filterEquals':
      return filterByField(value, args.field, args.value, 'equals');
    case 'filterContains':
      return filterByField(value, args.field, args.value, 'contains');
    case 'rejectEquals':
      return rejectByField(value, args.field, args.value);
    case 'findByField':
      return filterByField(value, args.field, args.value, 'equals')[0];
    case 'containsItem':
      return toArray(value).some((item) => text(item) === text(args.value));
    case 'sortBy':
      return sortByField(value, args.field, text(args.direction || 'asc'));
    case 'uniqueBy':
      return uniqueBy(value, args.field);
    case 'groupBy':
      return groupBy(value, args.field);
    case 'flatten':
      return toArray(value).flat();
    case 'objectPath':
      return readRuntimePath(value, text(args.path));
    case 'hasPath':
      return readRuntimePath(value, text(args.path)) !== undefined;
    case 'pickFields':
      return pickFields(value, args.fields);
    case 'omitFields':
      return omitFields(value, args.fields);
    case 'sum':
      return aggregate(value, args.field, 'sum');
    case 'average':
      return aggregate(value, args.field, 'average');
    case 'minBy':
      return aggregate(value, args.field, 'min');
    case 'maxBy':
      return aggregate(value, args.field, 'max');
    case 'coalesce':
      return isRuntimeValueEmpty(value) ? args.value ?? '' : value;
    case 'isEmpty':
      return isRuntimeValueEmpty(value);
    case 'isNotEmpty':
      return !isRuntimeValueEmpty(value);
    case 'isNull':
      return value === null || value === undefined;
    case 'isNotNull':
      return value !== null && value !== undefined;
    case 'isTrue':
      return toBoolean(value);
    case 'isFalse':
      return !toBoolean(value);
    case 'isString':
      return typeof value === 'string';
    case 'isBoolean':
      return typeof value === 'boolean';
    case 'isArray':
      return Array.isArray(value);
    case 'isObject':
      return value !== null && typeof value === 'object' && !Array.isArray(value);
    case 'not':
      return !toBoolean(value);
    case 'and':
      return toBoolean(value) && toBoolean(args.value);
    case 'or':
      return toBoolean(value) || toBoolean(args.value);
    case 'ifElse':
      return toBoolean(value) ? args.whenTrue ?? '' : args.whenFalse ?? '';
    case 'equals':
      return text(value) === text(args.value);
    case 'notEquals':
      return text(value) !== text(args.value);
    case 'greaterThan':
      return compareValues(value, args.value) > 0;
    case 'greaterOrEquals':
      return compareValues(value, args.value) >= 0;
    case 'lessThan':
      return compareValues(value, args.value) < 0;
    case 'lessOrEquals':
      return compareValues(value, args.value) <= 0;
    case 'contains':
      return text(value).includes(text(args.value));
    case 'startsWith':
      return text(value).startsWith(text(args.value));
    case 'endsWith':
      return text(value).endsWith(text(args.value));
    case 'inList':
      return inList(value, args.values);
    case 'isNumber':
      return Number.isFinite(Number(value));
    case 'isInteger':
      return Number.isInteger(Number(value));
    case 'isPositive':
      return toNumber(value) > 0;
    case 'isNegative':
      return toNumber(value) < 0;
    case 'isZero':
      return toNumber(value) === 0;
    case 'isDate':
      return parseDate(value) !== null;
    case 'isEmail':
      return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(text(value));
    case 'isPhone':
      return /^1[3-9]\d{9}$/.test(onlyDigits(value));
    case 'isUrl':
      return isUrl(value);
    case 'isGuid':
      return /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i.test(text(value));
    case 'isJson':
      return isJson(value);
    case 'isPostalCode':
      return /^\d{6}$/.test(text(value));
    case 'minLength':
      return text(value).length >= numberArg(args.length, 0);
    case 'maxLength':
      return text(value).length <= numberArg(args.length, Number.MAX_SAFE_INTEGER);
    case 'between':
      return between(toNumber(value), optionalNumberArg(args.min), optionalNumberArg(args.max));
    case 'maskPhone':
      return maskText(text(value), 3, 4, '****');
    case 'maskEmail':
      return maskEmail(value);
    case 'maskIdCard':
      return maskText(text(value), 4, 4, '**********');
    case 'maskBankCard':
      return maskText(text(value), 4, 4, ' **** **** ');
    case 'maskName':
      return maskName(value);
    case 'maskText':
      return maskText(text(value), numberArg(args.start, 2), numberArg(args.end, 2), text(args.mask || '***'));
    case 'booleanText':
      return toBoolean(value) ? text(args.trueText || '是') : text(args.falseText || '否');
    case 'yesNo':
      return toBoolean(value) ? '是' : '否';
    case 'enabledDisabled':
      return toBoolean(value) ? '启用' : '禁用';
    case 'successFail':
      return toBoolean(value) ? '成功' : '失败';
    case 'mapValue':
      return mapValue(value, args.mapping, args.defaultValue);
    default:
      runtimeLog.warn('runtime.expression.unknownHelper', { helperName: helper.name });
      return value;
  }
}

export function readRuntimePath(root: unknown, path: unknown): unknown {
  if (typeof path !== 'string') {
    runtimeLog.warn('runtime.expression.invalidPath', { path });
    return undefined;
  }

  if (!path.trim()) {
    return root;
  }

  return path
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .filter(Boolean)
    .reduce<unknown>((current, segment) => {
      if (current === null || current === undefined) {
        return undefined;
      }

      if (Array.isArray(current)) {
        const index = Number(segment);
        return Number.isInteger(index) ? current[index] : undefined;
      }

      if (typeof current === 'object') {
        return (current as Record<string, unknown>)[segment];
      }

      return undefined;
    }, root);
}

export function normalizeExpectedType(value: unknown, expectedType?: string | null): unknown {
  if (!expectedType || value === null || value === undefined) {
    return value;
  }

  switch (expectedType) {
    case 'array':
      return Array.isArray(value) ? value : [];
    case 'boolean':
      return typeof value === 'boolean' ? value : text(value).toLowerCase() === 'true';
    case 'number':
      return toNumber(value);
    case 'string':
      return text(value);
    default:
      return value;
  }
}

function normalizeRuntimeHelperName(name: string): string {
  const segments = name.split('.').filter(Boolean);
  const localName = name.includes('.') ? segments[segments.length - 1] ?? name : name;
  const normalized = localName.trim().replace(/[-_\s]/g, '').toLowerCase();
  return runtimeHelperNameAliases.get(normalized) ?? localName;
}

function text(value: unknown): string {
  return value === null || value === undefined ? '' : String(value);
}

export function runtimeText(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function toNumber(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function toBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  const normalized = text(value).trim().toLowerCase();
  return ['1', 'true', 'yes', 'y', 'on', '是', '启用'].includes(normalized);
}

function numberArg(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function optionalNumberArg(value: unknown): number | undefined {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function right(value: string, length: number): string {
  return length <= 0 ? '' : value.slice(-length);
}

function padChar(value: unknown): string {
  const char = text(value);
  return char ? Array.from(char)[0] ?? ' ' : ' ';
}

function splitTake(value: unknown, args: Record<string, unknown>): string {
  const separator = text(args.separator ?? ',');
  const parts = text(value).split(separator || ',');
  return parts[numberArg(args.index, 0)] ?? '';
}

function words(value: unknown): string[] {
  return text(value)
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .split(/[^A-Za-z0-9\u4e00-\u9fa5]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function capitalize(value: string): string {
  return value ? `${value.charAt(0).toUpperCase()}${value.slice(1).toLowerCase()}` : '';
}

function toDelimitedCase(value: unknown, mode: 'camel' | 'kebab' | 'snake'): string {
  const parts = words(value);
  if (mode === 'camel') {
    return parts.map((part, index) => index === 0 ? part.toLowerCase() : capitalize(part)).join('');
  }

  return parts.map((part) => part.toLowerCase()).join(mode === 'snake' ? '_' : '-');
}

function truncate(value: string, length: number, suffix: string): string {
  if (length <= 0 || value.length <= length) {
    return value;
  }

  return `${value.slice(0, length)}${suffix}`;
}

function textBefore(value: unknown, separator: unknown): string {
  const source = text(value);
  const marker = text(separator);
  if (!marker) {
    return source;
  }

  const index = source.indexOf(marker);
  return index < 0 ? source : source.slice(0, index);
}

function textAfter(value: unknown, separator: unknown): string {
  const source = text(value);
  const marker = text(separator);
  if (!marker) {
    return '';
  }

  const index = source.indexOf(marker);
  return index < 0 ? '' : source.slice(index + marker.length);
}

function textBetween(value: unknown, startMarker: unknown, endMarker: unknown): string {
  const source = text(value);
  const startText = text(startMarker);
  const endText = text(endMarker);
  const startIndex = startText ? source.indexOf(startText) : 0;
  if (startIndex < 0) {
    return '';
  }

  const contentStart = startIndex + startText.length;
  const endIndex = endText ? source.indexOf(endText, contentStart) : source.length;
  return endIndex < 0 ? '' : source.slice(contentStart, endIndex);
}

function ensureAffix(value: unknown, affix: unknown, mode: 'prefix' | 'suffix'): string {
  const source = text(value);
  const part = text(affix);
  if (!part) {
    return source;
  }

  if (mode === 'prefix') {
    return source.startsWith(part) ? source : `${part}${source}`;
  }

  return source.endsWith(part) ? source : `${source}${part}`;
}

function removeAffix(value: unknown, affix: unknown, mode: 'prefix' | 'suffix'): string {
  const source = text(value);
  const part = text(affix);
  if (!part) {
    return source;
  }

  if (mode === 'prefix') {
    return source.startsWith(part) ? source.slice(part.length) : source;
  }

  return source.endsWith(part) ? source.slice(0, -part.length) : source;
}

function normalizeNewlines(value: unknown): string {
  return text(value).replace(/\r\n?/g, '\n');
}

function divide(left: number, rightValue: number): number {
  return rightValue === 0 ? 0 : left / rightValue;
}

function modulo(left: number, rightValue: number): number {
  return rightValue === 0 ? 0 : left % rightValue;
}

function clamp(value: number, min?: number, max?: number): number {
  const lower = min ?? value;
  const upper = max ?? value;
  return Math.min(Math.max(value, lower), upper);
}

function round(value: number, digits: number): number {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

function substring(value: string, start: number, length?: number): string {
  return length === undefined ? value.slice(start) : value.slice(start, start + length);
}

function parseDate(value: unknown): Date | null {
  const date = value instanceof Date ? value : new Date(text(value));
  return Number.isNaN(date.getTime()) ? null : date;
}

function addDate(value: unknown, unit: 'day' | 'hour' | 'minute' | 'month' | 'second' | 'year', amount: number): string {
  const date = parseDate(value) ?? new Date();
  const next = new Date(date.getTime());
  if (unit === 'day') {
    next.setDate(next.getDate() + amount);
  } else if (unit === 'hour') {
    next.setHours(next.getHours() + amount);
  } else if (unit === 'minute') {
    next.setMinutes(next.getMinutes() + amount);
  } else if (unit === 'second') {
    next.setSeconds(next.getSeconds() + amount);
  } else if (unit === 'month') {
    next.setMonth(next.getMonth() + amount);
  } else {
    next.setFullYear(next.getFullYear() + amount);
  }

  return next.toISOString();
}

function periodBoundary(value: unknown, unit: 'month' | 'year', boundary: 'end' | 'start'): string {
  const date = parseDate(value) ?? new Date();
  const next = new Date(date.getTime());
  if (unit === 'month') {
    if (boundary === 'start') {
      next.setDate(1);
      next.setHours(0, 0, 0, 0);
    } else {
      next.setMonth(next.getMonth() + 1, 0);
      next.setHours(23, 59, 59, 999);
    }
  } else if (boundary === 'start') {
    next.setMonth(0, 1);
    next.setHours(0, 0, 0, 0);
  } else {
    next.setMonth(11, 31);
    next.setHours(23, 59, 59, 999);
  }

  return next.toISOString();
}

function diffDate(value: unknown, compareValue: unknown, unit: 'day' | 'hour' | 'minute' | 'second'): number {
  const left = parseDate(value);
  const right = parseDate(compareValue);
  if (!left || !right) {
    return 0;
  }

  const diff = left.getTime() - right.getTime();
  const unitSize = unit === 'day'
    ? 86_400_000
    : unit === 'hour'
      ? 3_600_000
      : unit === 'minute'
        ? 60_000
        : 1_000;
  return Math.trunc(diff / unitSize);
}

function fromTimestamp(value: unknown): string {
  const numeric = toNumber(value);
  if (!numeric) {
    return '';
  }

  const timestamp = numeric < 10_000_000_000 ? numeric * 1000 : numeric;
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

function weekOfYear(value: unknown): number | '' {
  const date = parseDate(value);
  if (!date) {
    return '';
  }

  const utcDate = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  utcDate.setUTCDate(utcDate.getUTCDate() + 4 - (utcDate.getUTCDay() || 7));
  const yearStart = new Date(Date.UTC(utcDate.getUTCFullYear(), 0, 1));
  return Math.ceil((((utcDate.getTime() - yearStart.getTime()) / 86_400_000) + 1) / 7);
}

function quarter(value: unknown): number | '' {
  const date = parseDate(value);
  return date ? Math.floor(date.getMonth() / 3) + 1 : '';
}

function formatQuarter(value: unknown): string {
  const date = parseDate(value);
  if (!date) {
    return '';
  }

  return `${date.getFullYear()}Q${Math.floor(date.getMonth() / 3) + 1}`;
}

function ageYears(value: unknown): number | '' {
  const birth = parseDate(value);
  if (!birth) {
    return '';
  }

  const today = new Date();
  let age = today.getFullYear() - birth.getFullYear();
  const beforeBirthday = today.getMonth() < birth.getMonth() || (today.getMonth() === birth.getMonth() && today.getDate() < birth.getDate());
  if (beforeBirthday) {
    age -= 1;
  }

  return age;
}

function dayBoundary(value: unknown, boundary: 'end' | 'start'): string {
  const date = parseDate(value) ?? new Date();
  const next = new Date(date.getTime());
  if (boundary === 'start') {
    next.setHours(0, 0, 0, 0);
  } else {
    next.setHours(23, 59, 59, 999);
  }

  return next.toISOString();
}

function formatDate(value: unknown, format: string): string {
  const date = parseDate(value);
  if (!date) {
    return '';
  }

  const parts: Record<string, string> = {
    dd: String(date.getDate()).padStart(2, '0'),
    HH: String(date.getHours()).padStart(2, '0'),
    mm: String(date.getMinutes()).padStart(2, '0'),
    MM: String(date.getMonth() + 1).padStart(2, '0'),
    ss: String(date.getSeconds()).padStart(2, '0'),
    yyyy: String(date.getFullYear())
  };
  return Object.entries(parts).reduce((result, [token, part]) => result.replaceAll(token, part), format);
}

function parseJson(value: unknown): unknown {
  if (typeof value !== 'string') {
    return value;
  }

  try {
    return JSON.parse(value) as unknown;
  } catch {
    return null;
  }
}

function toArray(value: unknown): unknown[] {
  if (Array.isArray(value)) {
    return value;
  }

  if (value === null || value === undefined) {
    return [];
  }

  return [value];
}

function toObject(value: unknown): Record<string, unknown> {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }

  const parsed = parseJson(value);
  return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : {};
}

function objectKeys(value: unknown): string[] {
  return Object.keys(toObject(value));
}

function objectValues(value: unknown): unknown[] {
  return Object.values(toObject(value));
}

function safeDecodeUri(value: unknown): string {
  try {
    return decodeURIComponent(text(value));
  } catch {
    return text(value);
  }
}

function parseCurrency(value: unknown): number {
  const normalized = text(value).replace(/[^\d.-]+/g, '');
  return toNumber(normalized);
}

function base64Encode(value: unknown): string {
  try {
    return btoa(unescape(encodeURIComponent(text(value))));
  } catch {
    return '';
  }
}

function base64Decode(value: unknown): string {
  try {
    return decodeURIComponent(escape(atob(text(value))));
  } catch {
    return '';
  }
}

function distinct(value: unknown): unknown[] {
  const seen = new Set<string>();
  return toArray(value).filter((item) => {
    const key = typeof item === 'object' ? JSON.stringify(item) : text(item);
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function pageItems(value: unknown, pageIndexValue: unknown, pageSizeValue: unknown): unknown[] {
  const pageIndex = Math.max(numberArg(pageIndexValue, 1), 1);
  const pageSize = Math.max(numberArg(pageSizeValue, 20), 1);
  const start = (pageIndex - 1) * pageSize;
  return toArray(value).slice(start, start + pageSize);
}

function aggregate(value: unknown, field: unknown, mode: 'average' | 'max' | 'min' | 'sum'): number {
  const values = toArray(value)
    .map((item) => field ? readRuntimePath(item, text(field)) : item)
    .map(toNumber);
  if (values.length === 0) {
    return 0;
  }

  if (mode === 'min') {
    return Math.min(...values);
  }

  if (mode === 'max') {
    return Math.max(...values);
  }

  const sum = values.reduce((total, item) => total + item, 0);
  return mode === 'average' ? divide(sum, values.length) : sum;
}

function filterByField(value: unknown, field: unknown, expected: unknown, mode: 'contains' | 'equals'): Record<string, unknown>[] {
  return toArray(value)
    .filter((item): item is Record<string, unknown> => item !== null && typeof item === 'object')
    .filter((item) => {
      const fieldValue = readRuntimePath(item, text(field));
      return mode === 'equals'
        ? text(fieldValue) === text(expected)
        : text(fieldValue).includes(text(expected));
    });
}

function rejectByField(value: unknown, field: unknown, expected: unknown): Record<string, unknown>[] {
  return toArray(value)
    .filter((item): item is Record<string, unknown> => item !== null && typeof item === 'object')
    .filter((item) => text(readRuntimePath(item, text(field))) !== text(expected));
}

function sortByField(value: unknown, field: unknown, direction: string): unknown[] {
  const factor = direction.toLowerCase() === 'desc' ? -1 : 1;
  return [...toArray(value)].sort((left, rightValue) => compareValues(readRuntimePath(left, text(field)), readRuntimePath(rightValue, text(field))) * factor);
}

function uniqueBy(value: unknown, field: unknown): unknown[] {
  const seen = new Set<string>();
  return toArray(value).filter((item) => {
    const key = text(readRuntimePath(item, text(field)));
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function groupBy(value: unknown, field: unknown): Record<string, unknown[]> {
  return toArray(value).reduce<Record<string, unknown[]>>((groups, item) => {
    const key = text(readRuntimePath(item, text(field))) || '未分组';
    groups[key] = groups[key] ? [...groups[key], item] : [item];
    return groups;
  }, {});
}

function pickFields(value: unknown, fields: unknown): Record<string, unknown> {
  const source = toObject(value);
  return fieldList(fields).reduce<Record<string, unknown>>((result, field) => {
    if (field in source) {
      result[field] = source[field];
    }

    return result;
  }, {});
}

function omitFields(value: unknown, fields: unknown): Record<string, unknown> {
  const removed = new Set(fieldList(fields));
  return Object.entries(toObject(value)).reduce<Record<string, unknown>>((result, [key, itemValue]) => {
    if (!removed.has(key)) {
      result[key] = itemValue;
    }

    return result;
  }, {});
}

function fieldList(value: unknown): string[] {
  return text(value)
    .split(/[,\n;]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function compareValues(left: unknown, rightValue: unknown): number {
  const leftNumber = Number(left);
  const rightNumber = Number(rightValue);
  if (Number.isFinite(leftNumber) && Number.isFinite(rightNumber)) {
    return leftNumber - rightNumber;
  }

  const leftDate = Date.parse(text(left));
  const rightDate = Date.parse(text(rightValue));
  if (Number.isFinite(leftDate) && Number.isFinite(rightDate)) {
    return leftDate - rightDate;
  }

  return text(left).localeCompare(text(rightValue));
}

function inList(value: unknown, values: unknown): boolean {
  return text(values)
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
    .includes(text(value));
}

function onlyDigits(value: unknown): string {
  return text(value).replace(/\D+/g, '');
}

function isUrl(value: unknown): boolean {
  try {
    const parsed = new URL(text(value));
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

function isJson(value: unknown): boolean {
  if (typeof value !== 'string') {
    return value !== null && typeof value === 'object';
  }

  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}

function between(value: number, min?: number, max?: number): boolean {
  if (min !== undefined && value < min) {
    return false;
  }

  if (max !== undefined && value > max) {
    return false;
  }

  return true;
}

function maskText(value: string, start: number, end: number, mask: string): string {
  if (!value) {
    return '';
  }

  if (value.length <= start + end) {
    return mask;
  }

  return `${value.slice(0, start)}${mask}${end > 0 ? value.slice(-end) : ''}`;
}

function maskEmail(value: unknown): string {
  const email = text(value);
  const [name, domain] = email.split('@');
  if (!name || !domain) {
    return maskText(email, 2, 2, '***');
  }

  return `${maskText(name, 1, 1, '***')}@${domain}`;
}

function maskName(value: unknown): string {
  const name = text(value);
  if (name.length <= 1) {
    return name ? '*' : '';
  }

  return `${name.slice(0, 1)}${'*'.repeat(Math.max(name.length - 1, 1))}`;
}

function mapValue(value: unknown, mapping: unknown, defaultValue: unknown): string {
  const pairs = text(mapping)
    .split(/[;\n,]+/)
    .map((item) => item.trim())
    .filter(Boolean);
  for (const pair of pairs) {
    const [key, mappedValue] = pair.split('=');
    if (key?.trim() === text(value)) {
      return mappedValue?.trim() ?? '';
    }
  }

  return isRuntimeValueEmpty(defaultValue) ? text(value) : text(defaultValue);
}

export function isRuntimeValueEmpty(value: unknown): boolean {
  return value === null ||
    value === undefined ||
    (typeof value === 'string' && value.trim() === '') ||
    (Array.isArray(value) && value.length === 0);
}
