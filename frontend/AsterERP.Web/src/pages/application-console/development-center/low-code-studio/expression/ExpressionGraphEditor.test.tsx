// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { ExpressionGraph } from './expressionGraph';
import { ExpressionGraphEditor } from './ExpressionGraphEditor';

function StatefulEditor({ initial, onChange }: { initial: ExpressionGraph; onChange: (graph: ExpressionGraph) => void }) {
  const [graph, setGraph] = useState(initial);
  return <ExpressionGraphEditor expectedType="string" graph={graph} onChange={(next) => { setGraph(next); onChange(next); }} />;
}

describe('latest ExpressionGraphEditor', () => {
  afterEach(cleanup);

  it('opens the AST editor and edits a constant node', () => {
    const onChange = vi.fn();
    render(<ExpressionGraphEditor expectedType="string" graph={{ root: { kind: 'literal', value: 'old', valueType: 'string' } }} onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    fireEvent.change(screen.getByDisplayValue('old'), { target: { value: 'new' } });
    expect(onChange).toHaveBeenCalledWith({ root: { kind: 'literal', value: 'new', valueType: 'string' } });
    expect(screen.getByText(/AST \/ 节点图/)).toBeTruthy();
  });

  it('reports an incompatible graph without accepting a raw path editor', () => {
    render(<ExpressionGraphEditor expectedType="string" graph={{ root: { kind: 'literal', value: 1, valueType: 'number' } }} onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    expect(screen.getByRole('alert')).toBeTruthy();
    expect(screen.queryByPlaceholderText(/path|路径/i)).toBeNull();
  });

  it('adds a typed condition node from the node toolbar', () => {
    const onChange = vi.fn();
    render(<ExpressionGraphEditor expectedType="string" graph={{ root: { kind: 'literal', value: 'old', valueType: 'string' } }} onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    fireEvent.click(screen.getByRole('button', { name: '添加条件节点' }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ root: expect.objectContaining({ kind: 'condition', valueType: 'string' }) }));
  });

  it('round-trips a valid expert text graph and reports malformed JSON', () => {
    const onChange = vi.fn();
    render(<ExpressionGraphEditor expectedType="string" graph={{ root: { kind: 'literal', value: 'old', valueType: 'string' } }} onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    fireEvent.click(screen.getByRole('button', { name: '切换专家文本模式' }));
    const editor = screen.getByRole('textbox', { name: '专家文本' });
    fireEvent.change(editor, { target: { value: '{' } });
    expect(screen.getByRole('alert')).toBeTruthy();
    fireEvent.change(editor, { target: { value: JSON.stringify({ root: { kind: 'literal', value: 'next', valueType: 'string' } }) } });
    expect(onChange).toHaveBeenLastCalledWith({ root: { kind: 'literal', value: 'next', valueType: 'string' } });
  });

  it('replaces/deletes nodes and edits function arguments', () => {
    const onChange = vi.fn();
    render(<StatefulEditor initial={{ root: { kind: 'literal', value: 'old', valueType: 'string' } }} onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    fireEvent.change(screen.getByRole('combobox', { name: 'Replace node root' }), { target: { value: 'functionCall' } });
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ root: expect.objectContaining({ kind: 'functionCall' }) }));
    fireEvent.click(screen.getByRole('button', { name: 'Add argument' }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ root: expect.objectContaining({ args: expect.any(Array) }) }));
    fireEvent.click(screen.getByRole('button', { name: 'Delete node root' }));
    expect(onChange).toHaveBeenLastCalledWith({ root: null });
  });

  it('surfaces preview failures instead of accepting unknown runtime functions', () => {
    render(<ExpressionGraphEditor expectedType="string" graph={{ root: { kind: 'functionCall', functionId: 'unknown', args: [], valueType: 'string' } }} onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /打开表达式节点图|表达式节点图/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Preview' }));
    expect(screen.getByTestId('expression-preview-error')).toBeTruthy();
  });
});
