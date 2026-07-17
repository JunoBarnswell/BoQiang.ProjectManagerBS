import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import type { ConfigFormSchema } from '../configFormTypes';

export const dictionaryCodeSchemas: ConfigFormSchema[] = [
  {
    description: '字典分类及字典项配置，发布后写入系统字典。',
    moduleKey: 'dictionary-code',
    objectType: 'DictionaryType',
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("字典编码"), name: 'dictCode', required: true },
          { component: 'text', label: translateCurrentLiteral("字典名称"), name: 'dictName', required: true },
          { component: 'keyValueList', helpText: '键为字典值，值为显示名称。', label: translateCurrentLiteral("字典项"), name: 'items', span: 2 }
        ],
        key: 'basic',
        title: '字典配置'
      }
    ],
    title: '字典分类'
  },
  {
    description: '单个字典项配置。',
    moduleKey: 'dictionary-code',
    objectType: 'DictionaryItem',
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("所属字典编码"), name: 'dictCode', required: true },
          { component: 'text', label: translateCurrentLiteral("字典项值"), name: 'value', required: true },
          { component: 'text', label: translateCurrentLiteral("字典项名称"), name: 'label', required: true },
          { component: 'number', label: translateCurrentLiteral("排序"), name: 'sortOrder' }
        ],
        key: 'basic',
        title: '字典项配置'
      }
    ],
    title: '字典项'
  },
  {
    description: '编码规则配置，可按固定值、日期、业务字段和流水号组合。',
    moduleKey: 'dictionary-code',
    objectType: 'CodeRule',
    sections: [
      {
        fields: [
          { component: 'text', label: translateCurrentLiteral("规则编码"), name: 'ruleCode', required: true },
          { component: 'text', label: translateCurrentLiteral("规则名称"), name: 'ruleName', required: true },
          { component: 'text', defaultValue: 'RK-{yyyyMMdd}-{0001}', label: translateCurrentLiteral("兼容 Pattern"), name: 'pattern', span: 2 },
          { component: 'select', defaultValue: 'Daily', label: translateCurrentLiteral("重置周期"), name: 'resetPolicy', options: [{ label: translateCurrentLiteral("每天"), value: 'Daily' }, { label: translateCurrentLiteral("每月"), value: 'Monthly' }, { label: translateCurrentLiteral("每年"), value: 'Yearly' }, { label: translateCurrentLiteral("不重置"), value: 'Never' }] },
          { component: 'codeRuleSegments', label: translateCurrentLiteral("规则段"), name: 'segments', span: 2 }
        ],
        key: 'basic',
        title: '编码规则'
      }
    ],
    title: '编码规则'
  }
];
