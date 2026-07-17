import { describe, expect, it } from 'vitest';

import { applicationConsoleMessagesZhCN } from './applicationConsoleMessages.zh-CN';

describe('application console Chinese messages', () => {
  it('provides readable data source diagnostic wizard messages', () => {
    expect(applicationConsoleMessagesZhCN).toMatchObject({
      'applicationConsole.dataCenter.wizard.editTitle': '编辑数据源配置',
      'applicationConsole.dataCenter.wizard.diagnostic.description': '保存前验证配置、网络、TLS、认证、数据库、权限和能力。',
      'applicationConsole.dataCenter.wizard.diagnostic.run': '诊断当前配置',
      'applicationConsole.dataCenter.wizard.diagnostic.passed': '连接诊断通过',
      'applicationConsole.dataCenter.diagnostic.recommendation': '处理建议：{suggestion}'
    });
  });
});
