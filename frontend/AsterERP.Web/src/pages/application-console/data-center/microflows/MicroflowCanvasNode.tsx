import type { NodeProps } from '@xyflow/react';
import { Copy, GitBranch, LogIn, LogOut, SlidersHorizontal, Trash2 } from 'lucide-react';
import type { ReactNode } from 'react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { FlowCanvasInputHandles, FlowCanvasOutputHandles } from '../../../../shared/flow-canvas/FlowCanvasHandles';

import type {
  MicroflowCanvasNode as MicroflowCanvasNodeType,
  MicroflowCanvasVariableTag
} from './microflowCanvasModel';

const nodeTone: Record<string, string> = {
  callApi: 'microflow-node-card--call-api',
  change: 'microflow-node-card--change',
  compositeCreate: 'microflow-node-card--create',
  compositeDelete: 'microflow-node-card--delete',
  compositeDetail: 'microflow-node-card--detail',
  compositeUpdate: 'microflow-node-card--change',
  create: 'microflow-node-card--create',
  decision: 'microflow-node-card--decision',
  delete: 'microflow-node-card--delete',
  detail: 'microflow-node-card--detail',
  end: 'microflow-node-card--terminal',
  globalVariables: 'microflow-node-card--variable',
  loop: 'microflow-node-card--loop',
  query: 'microflow-node-card--query',
  retrieve: 'microflow-node-card--query',
  return: 'microflow-node-card--return',
  setVariable: 'microflow-node-card--variable',
  start: 'microflow-node-card--start'
};

export function MicroflowCanvasNode({ data, selected }: NodeProps<MicroflowCanvasNodeType>) {
  const node = data.microflowNode;
  const isDesignOnlyNode = node.type === 'globalVariables';
  const inputAnchors = node.type === 'start' || isDesignOnlyNode ? [] : [{ id: 'in', label: 'In', name: 'input' }];
  const outputAnchors = node.type === 'end' || node.type === 'return' || isDesignOnlyNode
    ? []
    : [{ id: 'out', label: 'Out', name: 'output' }];
  const editable = node.type !== 'start';

  return (
    <div className={['microflow-node-card', nodeTone[node.type] ?? '', selected ? 'microflow-node-card--selected' : ''].filter(Boolean).join(' ')}>
      <FlowCanvasInputHandles
        anchors={inputAnchors}
        rootClassName="microflow-node-handles microflow-node-handles--inputs"
        rowClassName="microflow-node-handle-row"
        topStart={52}
        topStep={18}
      />
      <div className="microflow-node-card__header">
        <span className="microflow-node-card__icon">
          <GitBranch size={15} />
        </span>
        <div className="microflow-node-card__text">
          <strong title={node.name}>{node.name}</strong>
          <span title={data.description}>{data.title}</span>
        </div>
      </div>
      <div className="microflow-node-card__params">
        {node.type !== 'start' && !isDesignOnlyNode ? (
          <ParameterRow
            editable={editable}
            emptyLabel="未配置输入"
            icon={<LogIn size={11} />}
            label="输入"
            tags={data.inputTags}
            onEdit={() => data.onEditNodeConfig?.(node.id)}
          />
        ) : null}
        {node.type !== 'end' ? (
          <ParameterRow
            editable={editable}
            emptyLabel={node.type === 'start' ? '开始变量' : isDesignOnlyNode ? '未定义变量' : '未配置输出'}
            icon={<LogOut size={11} />}
            label={isDesignOnlyNode ? '变量' : '输出'}
            tags={data.outputTags}
            onEdit={() => data.onEditNodeConfig?.(node.id)}
          />
        ) : null}
      </div>
      <div className="microflow-node-card__tools">
        {editable ? (
          <button
            aria-label="编辑节点配置"
            className="nodrag nopan"
            data-microflow-node-action="node-config"
            title={translateCurrentLiteral("编辑节点配置")}
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              data.onEditNodeConfig?.(node.id);
            }}
          >
            <SlidersHorizontal size={13} />
          </button>
        ) : null}
        <button
          aria-label="Duplicate node"
          className="nodrag nopan"
          data-microflow-node-action="duplicate"
          title="Duplicate node"
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            data.onDuplicateNode?.(node.id);
          }}
        >
          <Copy size={13} />
        </button>
        <button
          aria-label="Delete node"
          className="nodrag nopan"
          data-microflow-node-action="delete"
          title="Delete node"
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            data.onDeleteNode?.(node.id);
          }}
        >
          <Trash2 size={13} />
        </button>
      </div>
      <FlowCanvasOutputHandles
        anchors={outputAnchors}
        rootClassName="microflow-node-handles microflow-node-handles--outputs"
        rowClassName="microflow-node-handle-row"
        showDefaultOutput={false}
        topStart={52}
        topStep={18}
      />
    </div>
  );
}

function ParameterRow({
  editable,
  emptyLabel,
  icon,
  label,
  onEdit,
  tags
}: {
  editable: boolean;
  emptyLabel: string;
  icon: ReactNode;
  label: string;
  onEdit: () => void;
  tags: MicroflowCanvasVariableTag[];
}) {
  const visibleTags = tags.slice(0, 2);
  const hiddenCount = Math.max(tags.length - visibleTags.length, 0);
  const title = tags.length > 0
    ? tags.map((tag) => tag.title).join('\n')
    : emptyLabel;

  const content = (
    <>
      <span className="microflow-node-card__param-label">{icon}{label}</span>
      <span className="microflow-node-card__tag-list">
        {visibleTags.length > 0 ? visibleTags.map((tag) => <VariableTag key={`${tag.label}:${tag.title}`} tag={tag} />) : (
          <span className="microflow-node-card__tag microflow-node-card__tag--empty">{emptyLabel}</span>
        )}
        {hiddenCount > 0 ? <span className="microflow-node-card__tag microflow-node-card__tag--more">+{hiddenCount}</span> : null}
      </span>
    </>
  );

  if (!editable) {
    return (
      <div className="microflow-node-card__param-row" title={title}>
        {content}
      </div>
    );
  }

  return (
    <button
      className="microflow-node-card__param-row microflow-node-card__param-row--editable nodrag nopan"
      data-microflow-node-action="node-config"
      title={`${title}\n点击编辑节点参数`}
      type="button"
      onClick={(event) => {
        event.stopPropagation();
        onEdit();
      }}
    >
      {content}
    </button>
  );
}

function VariableTag({ tag }: { tag: MicroflowCanvasVariableTag }) {
  return (
    <span
      className={['microflow-node-card__tag', tag.invalid ? 'microflow-node-card__tag--invalid' : ''].filter(Boolean).join(' ')}
      title={tag.title}
    >
      {tag.label}
    </span>
  );
}
