import type {
  ApplicationDevelopmentMenuOption,
  ApplicationDevelopmentRoleOption
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';

export interface DevelopmentPermissionState {
  allowAdd: boolean;
  allowDelete: boolean;
  allowEdit: boolean;
  allowExport: boolean;
  allowImport: boolean;
  menuCode: string;
  menuName: string;
  parentMenuCode: string;
  roleCodes: string[];
}

interface RolePermissionPanelProps {
  menuOptions: ApplicationDevelopmentMenuOption[];
  permissionState: DevelopmentPermissionState;
  roleOptions: ApplicationDevelopmentRoleOption[];
  onChange: (next: DevelopmentPermissionState) => void;
}

export function RolePermissionPanel({ menuOptions, permissionState, roleOptions, onChange }: RolePermissionPanelProps) {
  return (
    <section className="mb-3 overflow-hidden rounded-lg border border-slate-200 bg-white last:mb-0">
      <div className="flex items-center justify-between gap-2 border-b border-slate-200 bg-slate-50 px-3 py-2">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">{translateCurrentLiteral("角色与权限")}</p>
          <h2 className="text-sm font-semibold text-slate-950">{translateCurrentLiteral("发布控制")}</h2>
        </div>
      </div>
      <div className="space-y-3 p-3 text-sm">
        <InputField label="正式菜单编码" value={permissionState.menuCode} onChange={(value) => onChange({ ...permissionState, menuCode: value })} />
        <InputField label="正式菜单名称" value={permissionState.menuName} onChange={(value) => onChange({ ...permissionState, menuName: value })} />
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-slate-600">{translateCurrentLiteral("正式菜单父级")}</span>
          <select className="form-input h-9" value={permissionState.parentMenuCode} onChange={(event) => onChange({ ...permissionState, parentMenuCode: event.target.value })}>
            <option value="">{translateCurrentLiteral("请选择父级菜单")}</option>
            {menuOptions.map((menu) => (
              <option key={menu.menuCode} value={menu.menuCode}>
                {menu.menuName} / {menu.menuCode}
              </option>
            ))}
          </select>
        </label>
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
          <div className="mb-2 text-xs font-semibold text-slate-700">{translateCurrentLiteral("动作授权")}</div>
          <div className="grid gap-2">
            <CheckLine label="允许新增" checked={permissionState.allowAdd} onChange={(checked) => onChange({ ...permissionState, allowAdd: checked })} />
            <CheckLine label="允许编辑" checked={permissionState.allowEdit} onChange={(checked) => onChange({ ...permissionState, allowEdit: checked })} />
            <CheckLine label="允许删除" checked={permissionState.allowDelete} onChange={(checked) => onChange({ ...permissionState, allowDelete: checked })} />
            <CheckLine label="允许导入" checked={permissionState.allowImport} onChange={(checked) => onChange({ ...permissionState, allowImport: checked })} />
            <CheckLine label="允许导出" checked={permissionState.allowExport} onChange={(checked) => onChange({ ...permissionState, allowExport: checked })} />
          </div>
        </div>
        <div>
          <div className="mb-2 text-xs font-semibold text-slate-700">{translateCurrentLiteral("发布后自动授予角色")}</div>
          <div className="max-h-56 space-y-2 overflow-y-auto rounded-lg border border-slate-200 p-3">
            {roleOptions.length === 0 ? <div className="text-xs text-slate-500">{translateCurrentLiteral("当前应用库没有可用角色。")}</div> : null}
            {roleOptions.map((role) => {
              const checked = permissionState.roleCodes.includes(role.roleCode);
              return (
                <label key={role.roleCode} className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    checked={checked}
                    type="checkbox"
                    onChange={(event) =>
                      onChange({
                        ...permissionState,
                        roleCodes: event.target.checked
                          ? [...permissionState.roleCodes, role.roleCode]
                          : permissionState.roleCodes.filter((code) => code !== role.roleCode)
                      })
                    }
                  />
                  <span>{role.roleName}</span>
                  <span className="text-xs text-slate-400">{role.roleCode}</span>
                </label>
              );
            })}
          </div>
        </div>
      </div>
    </section>
  );
}

function CheckLine({ checked, label, onChange }: { checked: boolean; label: string; onChange: (checked: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm text-slate-700">
      <input checked={checked} type="checkbox" onChange={(event) => onChange(event.target.checked)} />
      {label}
    </label>
  );
}

function InputField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-slate-600">{label}</span>
      <input className="form-input h-9" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}
