import { RUNTIME_CAPABILITY_CONTRACT } from '../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';

type MessageBag = Record<string, string>;

const componentTerms: Record<string, string> = {
  actions: '动作组', apiCall: '接口调用', article: '文章', aside: '侧栏', audio: '音频', basic: '基础图表', blockquote: '引用块', br: '换行', button: '按钮', canvas: '画布', caption: '标题', checkbox: '复选框', code: '代码', col: '列', colgroup: '列组', color: '颜色', column: '列布局', container: '容器', datalist: '数据列表', dataTable: '数据表', date: '日期', datetimeLocal: '本地日期时间', dd: '定义描述', details: '详情', dialog: '对话框', div: '容器', dl: '定义列表', drawer: '抽屉', dropdown: '下拉框', dt: '定义标题', email: '邮箱', em: '强调文本', figcaption: '图注', figure: '图形', file: '文件', fileUpload: '文件上传', footer: '页脚', form: '表单', formItem: '表单项', h1: '一级标题', h2: '二级标题', h3: '三级标题', h4: '四级标题', h5: '五级标题', h6: '六级标题', header: '页头', heading: '标题', hidden: '隐藏字段', hr: '分隔线', html: 'HTML 容器', iframe: '嵌入框架', imageButton: '图片按钮', imageUpload: '图片上传', img: '图片', input: '输入框', li: '列表项', link: '链接', main: '主内容', mark: '标记文本', math: '数学公式', menu: '菜单', meter: '仪表', month: '月份', multi: '多选框', nav: '导航', number: '数字输入', ol: '有序列表', page: '页面', paragraph: '段落', password: '密码输入', picture: '响应式图片', popover: '浮层', pre: '预格式文本', print: '打印布局', printAction: '打印动作', progress: '进度条', quote: '引用', radio: '单选框', range: '范围输入', resetButton: '重置按钮', responsive: '响应式布局', row: '行布局', search: '搜索输入', section: '分区', semantic: '语义表格', signature: '签名', small: '小号文本', source: '媒体源', span: '行内容器', split: '分栏布局', strong: '加粗文本', submitButton: '提交按钮', svg: '矢量图', tableContainer: '表格容器', tabs: '标签页', tbody: '表体', td: '单元格', tel: '电话输入', template: '模板', text: '文本', textarea: '多行输入', tfoot: '表尾', th: '表头单元格', thead: '表头', time: '时间', track: '字幕轨道', tr: '表格行', ul: '无序列表', url: '网址输入', value: '值显示', video: '视频', week: '周', workflow: '工作流'
};

const componentMessages = Object.fromEntries(RUNTIME_CAPABILITY_CONTRACT.components.flatMap((type) => {
  const keyType = type.replaceAll('.', '_');
  const [, part = type] = type.split('.', 2);
  const label = componentTerms[part] ?? componentTerms[type] ?? '通用控件';
  return [
    [`lowCode.component.${keyType}.label`, label],
    [`lowCode.component.${keyType}.help`, `配置${label}的属性与运行时绑定。`],
    [`lowCode.component.${keyType}.diagnostic`, `验证${label}的配置与运行时能力。`]
  ];
}));

export const lowCodeStudioMessagesZhCN: MessageBag = {
  ...componentMessages,
  'lowCode.pageStudio.backToPages': '返回页面',
  'lowCode.pageStudio.zoomOut': '缩小',
  'lowCode.pageStudio.zoomIn': '放大',
  'lowCode.pageStudio.viewport': '视口',
  'lowCode.pageStudio.devicePreview': '设备预览',
  'lowCode.pageStudio.mobile': '移动端',
  'lowCode.pageStudio.tablet': '平板',
  'lowCode.pageStudio.desktop': '桌面端',
  'lowCode.pageStudio.responsiveBreakpoint': '响应式断点',
  'lowCode.pageStudio.base': '基础尺寸',
  'lowCode.pageStudio.preview': '预览',
  'lowCode.pageStudio.saving': '保存中…',
  'lowCode.pageStudio.saveDraft': '保存草稿',
  'lowCode.pageStudio.inspector': '属性检查器',
  'lowCode.pageStudio.workflow': '工作流',
  'lowCode.pageStudio.publish': '发布',
  'lowCode.pageStudio.latestRuntimeChain': '最新运行时链路',
  'lowCode.pageStudio.checksPassed': '文档、Manifest 与渲染器检查已通过。',
  'lowCode.pageStudio.openPreview': '打开预览',
  'lowCode.pageStudio.refreshMenu': '刷新菜单',
  'lowCode.pageStudio.artifact': '产物',
  'lowCode.pageStudio.menu': '菜单',
  'lowCode.pageStudio.route': '路由',
  'lowCode.pageStudio.permissions': '权限',
  'lowCode.pageStudio.menuCode': '菜单编码',
  'lowCode.pageStudio.menuName': '菜单名称',
  'lowCode.pageStudio.parentMenu': '父级菜单',
  'lowCode.pageStudio.selectParentMenu': '选择父级菜单',
  'lowCode.pageStudio.actionPermissions': '动作权限',
  'lowCode.pageStudio.roles': '角色',
  'lowCode.pageStudio.add': '新增',
  'lowCode.pageStudio.edit': '编辑',
  'lowCode.pageStudio.delete': '删除',
  'lowCode.pageStudio.import': '导入',
  'lowCode.pageStudio.export': '导出'
  , 'lowCode.pageStudio.components': '组件'
  , 'lowCode.pageStudio.layers': '图层'
  , 'lowCode.pageStudio.resources': '资源'
  , 'lowCode.pageStudio.pageTools': '页面工具'
  , 'lowCode.pageStudio.dockTools': '设计器工具'
  , 'lowCode.pageStudio.pinDock': '固定面板'
  , 'lowCode.pageStudio.unpinDock': '取消固定面板'
  , 'lowCode.pageStudio.closeDock': '关闭面板'
  , 'lowCode.pageStudio.dockWidth': '面板宽度'
  , 'lowCode.pageStudio.componentPalette': '组件面板'
  , 'lowCode.pageStudio.readyToInsert': '可插入到当前容器'
  , 'lowCode.pageStudio.selectContainerFirst': '请先选择容器'
  , 'lowCode.pageStudio.searchComponents': '搜索组件'
  , 'lowCode.pageStudio.searchComponentsPlaceholder': '搜索组件名称或类型'
  , 'lowCode.pageStudio.componentCategories': '组件分类'
  , 'lowCode.pageStudio.common': '常用'
  , 'lowCode.pageStudio.all': '全部'
  , 'lowCode.pageStudio.recent': '最近'
  , 'lowCode.pageStudio.favorites': '收藏'
  , 'lowCode.pageStudio.favorite': '收藏'
  , 'lowCode.pageStudio.removeFavorite': '取消收藏'
  , 'lowCode.pageStudio.noMatchingComponents': '没有匹配的组件'
  , 'lowCode.pageStudio.collapse': '收起'
  , 'lowCode.pageStudio.expand': '展开'
  , 'lowCode.pageStudio.layerTree': '图层树'
  , 'lowCode.pageStudio.sectionContent': '内容'
  , 'lowCode.pageStudio.sectionLayout': '布局'
  , 'lowCode.pageStudio.sectionAppearance': '外观'
  , 'lowCode.pageStudio.sectionData': '数据'
  , 'lowCode.pageStudio.sectionInteraction': '交互'
  , 'lowCode.pageStudio.sectionAdvanced': '高级'
  , 'lowCode.pageStudio.selectComponentToInspect': '选择画布中的组件后，可在这里编辑属性。'
  , 'lowCode.pageStudio.selected': '已选择'
  , 'lowCode.pageStudio.batchUnavailable': '不可批量编辑'
  , 'lowCode.pageStudio.undo': '撤销'
  , 'lowCode.pageStudio.redo': '重做'
  , 'lowCode.pageStudio.fitCanvas': '适应画布'
  , 'lowCode.pageStudio.saved': '已保存'
  , 'lowCode.pageStudio.unsavedChanges': '未保存更改'
  , 'lowCode.pageStudio.unsavedChangesDescription': '当前编辑尚未保存，离开后这些更改将丢失。'
  , 'lowCode.pageStudio.leaveWithoutSaving': '仍要离开'
  , 'lowCode.pageStudio.stayEditing': '继续编辑'
  , 'lowCode.pageStudio.previewInvalid': '当前配置无法预览'
  , 'lowCode.pageStudio.unknownComponent': '未知组件'
  , 'lowCode.pageStudio.preparingPreview': '正在准备最新运行时预览'
  , 'lowCode.pageStudio.resourceReferenceCopied': '已复制资源引用'
  , 'lowCode.pageStudio.resourceSelected': '已选择资源'
  , 'lowCode.pageStudio.responsiveOverrides': '响应式差异'
  , 'lowCode.pageStudio.inherited': '继承'
  , 'lowCode.pageStudio.current': '当前'
  , 'lowCode.pageStudio.source': '来源'
  , 'lowCode.pageStudio.noResponsiveDifferences': '当前断点没有差异'
  , 'lowCode.pageStudio.resetToInherited': '重置为继承'
  , 'lowCode.pageStudio.canvasSettings': '画布设置'
  , 'lowCode.pageStudio.closeCanvasSettings': '关闭画布设置'
  , 'lowCode.pageStudio.view': '视图'
  , 'lowCode.pageStudio.device': '设备'
  , 'lowCode.pageStudio.deviceProfile': '设备配置'
  , 'lowCode.pageStudio.editorCanvas': '编辑器画布'
  , 'lowCode.pageStudio.custom': '自定义'
  , 'lowCode.pageStudio.width': '宽度'
  , 'lowCode.pageStudio.height': '高度'
  , 'lowCode.pageStudio.deviceWidth': '设备宽度'
  , 'lowCode.pageStudio.deviceHeight': '设备高度'
  , 'lowCode.pageStudio.toggleOrientation': '切换设备方向'
  , 'lowCode.pageStudio.portrait': '竖屏'
  , 'lowCode.pageStudio.landscape': '横屏'
  , 'lowCode.pageStudio.showRulers': '显示标尺'
  , 'lowCode.pageStudio.rulers': '标尺'
  , 'lowCode.pageStudio.showGrid': '显示网格'
  , 'lowCode.pageStudio.grid': '网格'
  , 'lowCode.pageStudio.gridSize': '网格尺寸'
  , 'lowCode.pageStudio.snapThreshold': '吸附阈值'
  , 'lowCode.pageStudio.clearSearch': '清空搜索'
  , 'lowCode.pageStudio.canvas': '页面设计画布'
  , 'lowCode.pageStudio.canvasRootMissing': '页面根画布不存在'
  , 'lowCode.pageStudio.pageArtboard': '页面画板'
  , 'lowCode.pageStudio.artboard': '画板'
  , 'lowCode.pageStudio.page': '页面'
  , 'lowCode.pageStudio.layerTreeHelp': '拖动图层可调整父级和顺序；点击画板可打开页面设置。'
  , 'lowCode.pageStudio.inlineEditor': '组件内联编辑器'
  , 'lowCode.pageStudio.insertBefore': '插入到前面'
  , 'lowCode.pageStudio.insertAfter': '插入到后面'
  , 'lowCode.pageStudio.insertInside': '插入到容器内部'
  , 'lowCode.pageStudio.moveInside': '移动到容器内部'
  , 'lowCode.pageStudio.invalidMoveTarget': '此位置没有可用的移动目标'
  , 'lowCode.pageStudio.dropToAdd': '释放以添加组件'
  , 'lowCode.pageStudio.dropToMove': '释放以移动组件'
  , 'lowCode.pageStudio.selectionMarquee': '框选区域'
  , 'lowCode.pageStudio.userGuide': '用户参考线'
  , 'lowCode.pageStudio.guides': '参考线'
  , 'lowCode.pageStudio.addVerticalGuide': '添加垂直参考线'
  , 'lowCode.pageStudio.addHorizontalGuide': '添加水平参考线'
  , 'lowCode.pageStudio.noGuides': '暂无参考线'
  , 'lowCode.pageStudio.guidePosition': '参考线位置'
  , 'lowCode.pageStudio.deleteGuide': '删除参考线'
  , 'lowCode.pageStudio.canvasRulers': '画布标尺'
  , 'lowCode.pageStudio.horizontalRuler': '水平标尺'
  , 'lowCode.pageStudio.verticalRuler': '垂直标尺'
  , 'lowCode.pageStudio.bindingSlots': '组件绑定槽位'
  , 'lowCode.pageStudio.bindSlot': '绑定'
  , 'lowCode.pageStudio.bindingAdd': '绑定资源'
  , 'lowCode.pageStudio.bindingReplace': '替换绑定'
  , 'lowCode.pageStudio.bindingRemove': '解除绑定'
  , 'lowCode.pageStudio.bindingTypeIncompatible': '当前绑定类型不兼容，已阻止保存。'
  , 'lowCode.pageStudio.bindingRepairing': '正在修复'
  , 'lowCode.pageStudio.bindingChooseReplacement': '请选择替代资源'
  , 'lowCode.pageStudio.selectOption': '请选择'
  , 'lowCode.pageStudio.mixedValue': '混合值'
  , 'lowCode.pageStudio.addItem': '添加项'
  , 'lowCode.pageStudio.addProperty': '添加属性'
  , 'lowCode.pageStudio.noComplexItems': '暂无项'
  , 'lowCode.pageStudio.noComplexProperties': '暂无属性'
  , 'lowCode.pageStudio.complexPropertyName': '属性名'
  , 'lowCode.pageStudio.browserSimulation': '浏览器顶部区域模拟'
  , 'lowCode.pageStudio.safeAreaSimulation': '设备安全区域模拟'
  , 'lowCode.pageStudio.resizeHandle': '调整组件尺寸'
  , 'lowCode.pageStudio.zoomPresets': '缩放预设'
  , 'lowCode.pageStudio.fitOptions': '画布适应选项'
  , 'lowCode.pageStudio.fitWidth': '适应宽度'
  , 'lowCode.pageStudio.fitPage': '适应整页'
  , 'lowCode.pageStudio.fitSelection': '适应选区'
  , 'lowCode.pageStudio.showMinimap': '显示小地图'
  , 'lowCode.pageStudio.minimap': '小地图'
  , 'lowCode.pageStudio.canvasZoom': '画布缩放'
  , 'lowCode.pageStudio.expandMinimap': '展开小地图'
  , 'lowCode.pageStudio.collapseMinimap': '收起小地图'
  , 'lowCode.pageStudio.minimapDescription': '点击可居中，拖动视口可平移，滚轮可缩放。'
  , 'lowCode.pageStudio.minimapNavigation': '小地图导航区域'
  , 'lowCode.pageStudio.visibleViewport': '当前可见视口'
  , 'lowCode.pageStudio.handTool': '抓手工具'
  , 'lowCode.pageStudio.contextMenu': '画布组件菜单'
  , 'lowCode.pageStudio.contextCopy': '复制'
  , 'lowCode.pageStudio.contextPaste': '粘贴到内部'
  , 'lowCode.pageStudio.contextDuplicate': '创建副本'
  , 'lowCode.pageStudio.contextBringForward': '上移一层'
  , 'lowCode.pageStudio.contextBringToFront': '置于顶层'
  , 'lowCode.pageStudio.contextSendBackward': '下移一层'
  , 'lowCode.pageStudio.contextSendToBack': '置于底层'
  , 'lowCode.pageStudio.contextLock': '锁定组件'
  , 'lowCode.pageStudio.contextUnlock': '解锁组件'
  , 'lowCode.pageStudio.contextDelete': '删除组件'
  , 'lowCode.pageStudio.diagnostic': '诊断'
  , 'lowCode.pageStudio.layoutEditor': '布局编辑工具栏'
  , 'lowCode.pageStudio.layout': '布局'
  , 'lowCode.pageStudio.selectContainerOrChild': '请选择容器或其子组件'
  , 'lowCode.pageStudio.freeLayout': '自由布局'
  , 'lowCode.pageStudio.flexLayout': 'Flex'
  , 'lowCode.pageStudio.gridLayout': 'Grid'
  , 'lowCode.pageStudio.constraintsLayout': '约束布局'
  , 'lowCode.pageStudio.direction': '方向'
  , 'lowCode.pageStudio.flexDirection': 'Flex 方向'
  , 'lowCode.pageStudio.columns': '列数'
  , 'lowCode.pageStudio.gridColumns': 'Grid 列数'
  , 'lowCode.pageStudio.gap': '间距'
  , 'lowCode.pageStudio.layoutGap': '布局间距'
  , 'lowCode.pageStudio.moveChildUp': '子组件上移'
  , 'lowCode.pageStudio.moveChildDown': '子组件下移'
  , 'lowCode.pageStudio.alignLeft': '左对齐'
  , 'lowCode.pageStudio.alignCenter': '水平居中'
  , 'lowCode.pageStudio.alignRight': '右对齐'
  , 'lowCode.pageStudio.alignTop': '顶部对齐'
  , 'lowCode.pageStudio.alignMiddle': '垂直居中'
  , 'lowCode.pageStudio.alignBottom': '底部对齐'
  , 'lowCode.pageStudio.distributeHorizontal': '水平分布'
  , 'lowCode.pageStudio.distributeVertical': '垂直分布'
  , 'lowCode.pageStudio.sameWidth': '统一宽度'
  , 'lowCode.pageStudio.sameHeight': '统一高度'
};
