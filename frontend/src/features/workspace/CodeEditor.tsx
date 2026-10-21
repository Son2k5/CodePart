import { useEffect, useRef } from 'react';
import Editor, { loader } from '@monaco-editor/react';
import * as monaco from 'monaco-editor/editor/editor.api';
import 'monaco-editor/languages/definitions/python/register.js';
import 'monaco-editor/languages/definitions/cpp/register.js';
import 'monaco-editor/languages/definitions/java/register.js';
import 'monaco-editor/languages/definitions/csharp/register.js';
import 'monaco-editor/languages/definitions/javascript/register.js';
import EditorWorker from 'monaco-editor/editor/editor.worker.js?worker';
import type { WorkspaceStore } from './store';
import { Loading } from './ui';
globalThis.MonacoEnvironment = { getWorker: () => new EditorWorker() };
loader.config({ monaco });
export type EditorHandle = monaco.editor.IStandaloneCodeEditor;
export type Diagnostic = { line: number; column?: number; message: string };
export default function CodeEditor({ store, draftId, starter, language, fontSize, onReady, diagnostics, suspectedLines, judgedCode, hintCode }: {
  store: WorkspaceStore; draftId: string; starter: string; language: string; fontSize: number;
  onReady: (editor: EditorHandle | null) => void; diagnostics: Diagnostic[]; suspectedLines: number[]; judgedCode?: string; hintCode?: string;
}) {
  const editor = useRef<EditorHandle | null>(null);
  const decorations = useRef<monaco.editor.IEditorDecorationsCollection | null>(null);
  const initial = useRef(store.getState().open(draftId, starter));
  useEffect(() => {
    const save = () => store.getState().save(draftId);
    window.addEventListener('pagehide', save);
    const visibility = () => { if (document.visibilityState === 'hidden') save(); };
    document.addEventListener('visibilitychange', visibility);
    return () => { save(); window.removeEventListener('pagehide', save); document.removeEventListener('visibilitychange', visibility); };
  }, [store, draftId, onReady]);
  useEffect(() => {
    const model = editor.current?.getModel(); if (!model) return;
    monaco.editor.setModelMarkers(model, 'judge', diagnostics.filter(d => model.getValue() === judgedCode && d.line > 0 && d.line <= model.getLineCount()).map(d => ({
      startLineNumber: d.line, endLineNumber: d.line, startColumn: d.column || 1, endColumn: model.getLineMaxColumn(d.line), message: d.message, severity: monaco.MarkerSeverity.Error,
    })));
    const collection = editor.current?.createDecorationsCollection(suspectedLines.filter(line => model.getValue() === hintCode && line > 0 && line <= model.getLineCount()).map(line => ({ range: new monaco.Range(line, 1, line, 1), options: { isWholeLine: true, className: 'ai-suspected-line' } })));
    decorations.current = collection ?? null;
    return () => { collection?.clear(); monaco.editor.setModelMarkers(model, 'judge', []); };
  }, [diagnostics, suspectedLines, judgedCode, hintCode]);
  return <Editor height="100%" path={draftId} defaultValue={initial.current} language={language} theme="codepath-light" loading={<Loading text="Đang mở trình soạn thảo…" />}
    beforeMount={m => m.editor.defineTheme('codepath-light', { base: 'vs', inherit: true, rules: [{ token: 'comment', foreground: '75877B', fontStyle: 'italic' }, { token: 'keyword', foreground: '7654A5' }, { token: 'string', foreground: '397B62' }], colors: { 'editor.background': '#FFFFFF', 'editor.lineHighlightBackground': '#F6FAF8', 'editorLineNumber.foreground': '#8A9692', 'editor.selectionBackground': '#DDEFF8', 'editorCursor.foreground': '#397C70' } })}
    onMount={value => { editor.current = value; onReady(value); }} onChange={value => {
      store.getState().edit(draftId, value ?? '');
      decorations.current?.clear();
      const model = editor.current?.getModel(); if (model) monaco.editor.setModelMarkers(model, 'judge', []);
    }} options={{ minimap: { enabled: false }, fontSize, fontFamily: 'JetBrains Mono, monospace', tabSize: language === 'javascript' ? 2 : 4, padding: { top: 20 }, automaticLayout: true, scrollBeyondLastLine: false, bracketPairColorization: { enabled: true }, wordWrap: 'on', smoothScrolling: !matchMedia('(prefers-reduced-motion: reduce)').matches }} />;
}
