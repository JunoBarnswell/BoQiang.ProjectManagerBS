import { describe, expect, it } from 'vitest';

import type { RuntimeExpressionFunctionCatalogResponse, RuntimeExpressionFunctionDefinitionDto } from '../../../../api/runtime/runtimeExpressionFunctions.types';

import {
  buildMicroflowSqlScriptFunctionDiagnostics,
  microflowSqlScriptBuiltInVariables
} from './microflowSqlScriptLanguage';

describe('microflowSqlScriptLanguage function diagnostics', () => {
  it('accepts namespaced and nested whitelist functions', () => {
    const diagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
SET @keyword = StringFns.trim(@keyword);
SET @pageSize = NumberFns.clamp(NumberFns.toInt(@pageSize), 1, 200);
RETURN SELECT @keyword, @pageSize;
`, createCatalog());

    expect(diagnostics).toEqual([]);
  });

  it('ignores examples inside strings and comments', () => {
    const diagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
-- SET @keyword = trim(@keyword);
/* SET @pageSize = FooFns.nope(@pageSize); */
SET @keyword = 'StringFns.nope(customer_name)';
RETURN SELECT @keyword;
`, createCatalog());

    expect(diagnostics).toEqual([]);
  });

  it('reports unknown runtime namespaces and functions', () => {
    const diagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
SET @keyword = FooFns.trim(@keyword);
SET @pageSize = NumberFns.unknown(@pageSize);
RETURN SELECT @keyword;
`, createCatalog());

    expect(diagnostics.map((item) => item.message)).toEqual([
      '未知函数命名空间 FooFns',
      '函数 NumberFns.unknown 不在当前 SQL 白名单中'
    ]);
  });

  it('reports wrong argument count and naked helper functions in SET expressions', () => {
    const diagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
SET @keyword = trim(@keyword);
SET @pageSize = NumberFns.clamp(@pageSize, 1);
RETURN SELECT @keyword;
`, createCatalog());

    expect(diagnostics.map((item) => item.message)).toEqual([
      'NumberFns.clamp 参数数量应为 3 个',
      '函数 trim 必须使用 StringFns/NumberFns 等命名空间'
    ]);
  });

  it('reports direct database column arguments', () => {
    const diagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
RETURN SELECT StringFns.trim(customer_name) AS customer_name FROM order_header;
`, createCatalog());

    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0].message).toBe('函数参数不能直接引用数据库列 customer_name，列处理请使用 SQL 原生函数');
  });

  it('exposes fixed sql built-in variables', () => {
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@currentUserId');
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@currentEmploymentId');
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@currentDeptIds');
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@currentPositionIds');
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@auditNow');
    expect(microflowSqlScriptBuiltInVariables.map((item) => item.label)).toContain('@auditCreatedBy');
  });

  it('accepts rbac literal functions and rejects dynamic or unsafe rbac arguments', () => {
    expect(buildMicroflowSqlScriptFunctionDiagnostics(`
RETURN SELECT * FROM orders
WHERE RbacFns.hasPermission('mes:order:viewAll') = 1
   OR created_by = @currentUserId;
`, createCatalog())).toEqual([]);

    const dynamicDiagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
RETURN SELECT * FROM orders WHERE RbacFns.hasPermission(@permissionCode) = 1;
`, createCatalog());
    expect(dynamicDiagnostics.map((item) => item.message)).toEqual([
      'RbacFns.hasPermission 只允许使用字符串字面量权限或角色编码'
    ]);

    const unsafeDiagnostics = buildMicroflowSqlScriptFunctionDiagnostics(`
RETURN SELECT * FROM orders WHERE RbacFns.hasPermission('x''); DROP TABLE users; --') = 1;
`, createCatalog());
    expect(unsafeDiagnostics.map((item) => item.message)).toContain('RbacFns.hasPermission 参数包含非法权限或角色编码');
  });
});

function createCatalog(): RuntimeExpressionFunctionCatalogResponse {
  return {
    functions: [
      createFunction({ canonicalName: 'trim', functionName: 'trim', namespace: 'StringFns', returnType: 'string' }),
      createFunction({ canonicalName: 'clamp', functionName: 'clamp', namespace: 'NumberFns', returnType: 'number', parameters: [
        { dataType: 'number', description: '', label: '最小值', name: 'min', required: true },
        { dataType: 'number', description: '', label: '最大值', name: 'max', required: true }
      ] }),
      createFunction({ canonicalName: 'tointeger', functionName: 'toInt', namespace: 'NumberFns', returnType: 'number' }),
      createFunction({
        canonicalName: 'haspermission',
        functionName: 'hasPermission',
        namespace: 'RbacFns',
        parameters: [
          { dataType: 'string', description: '', label: '权限编码', name: 'permissionCode', required: true }
        ],
        requiresInput: false,
        returnType: 'number'
      })
    ],
    scope: 'microflowSqlScript'
  };
}

function createFunction(patch: Partial<RuntimeExpressionFunctionDefinitionDto>): RuntimeExpressionFunctionDefinitionDto {
  const namespace = patch.namespace ?? 'StringFns';
  const functionName = patch.functionName ?? 'trim';
  return {
    canonicalName: patch.canonicalName ?? functionName.toLowerCase(),
    description: patch.description ?? functionName,
    deterministic: true,
    disabledReason: '',
    examples: patch.examples ?? [],
    functionName,
    label: patch.label ?? functionName,
    moduleKey: patch.moduleKey ?? namespace.replace(/Fns$/, '').toLowerCase(),
    moduleName: patch.moduleName ?? namespace,
    namespace,
    parameters: patch.parameters ?? [],
    qualifiedName: patch.qualifiedName ?? `${namespace}.${functionName}`,
    requiresInput: patch.requiresInput ?? true,
    returnType: patch.returnType ?? 'string',
    sqlEnabled: true
  };
}
