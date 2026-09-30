import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
// @ts-ignore
import { Panel, PanelGroup, PanelResizeHandle } from 'react-resizable-panels';
import CodeMirror from '@uiw/react-codemirror';
import { javascript } from '@codemirror/lang-javascript';
import { python } from '@codemirror/lang-python';
import { java } from '@codemirror/lang-java';
import { cpp } from '@codemirror/lang-cpp';
import { Play, Send, CheckCircle2, XCircle, Clock, Cpu, ChevronLeft } from 'lucide-react';
import { judgeApi } from '../api/judgeApi';
import type { Exercise, RunResult, SubmitResult } from '../api/judgeApi';

const LANGUAGES = [
  { id: 71, name: 'Python 3', value: 'python' },
  { id: 63, name: 'JavaScript', value: 'javascript' },
  { id: 62, name: 'Java', value: 'java' },
  { id: 51, name: 'C#', value: 'csharp' },
  { id: 54, name: 'C++', value: 'cpp' },
];

export const ExercisePage = () => {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [exercise, setExercise] = useState<Exercise | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [language, setLanguage] = useState(LANGUAGES[0]);
  const [code, setCode] = useState('def twoSum(nums, target):\n    # Write your code here\n    pass');
  const [customInput, setCustomInput] = useState('');
  
  const [isRunning, setIsRunning] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const [runResult, setRunResult] = useState<RunResult | null>(null);
  const [submitResult, setSubmitResult] = useState<SubmitResult | null>(null);
  const [activeTab, setActiveTab] = useState<'testcases' | 'result'>('testcases');

  useEffect(() => {
    if (slug) {
      judgeApi.getExerciseBySlug(slug)
        .then(data => {
          setExercise(data);
          if (data.testCases.length > 0) {
            setCustomInput(data.testCases[0].input);
          }
          setLoading(false);
        })
        .catch(err => {
          setError(err.message);
          setLoading(false);
        });
    }
  }, [slug]);

  const handleRunCode = async () => {
    if (!exercise) return;
    setIsRunning(true);
    setRunResult(null);
    setSubmitResult(null);
    setActiveTab('result');
    try {
      const result = await judgeApi.runCode(exercise.id, language.id, code, customInput);
      setRunResult(result);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsRunning(false);
    }
  };

  const handleSubmitCode = async () => {
    if (!exercise) return;
    setIsSubmitting(true);
    setRunResult(null);
    setSubmitResult(null);
    setActiveTab('result');
    try {
      const result = await judgeApi.submitCode(exercise.id, language.id, code);
      setSubmitResult(result);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (loading) {
    return <div className="flex h-screen items-center justify-center bg-gray-900 text-white"><div className="animate-spin rounded-full h-12 w-12 border-b-2 border-white"></div></div>;
  }

  if (error || !exercise) {
    return <div className="flex h-screen items-center justify-center bg-gray-900 text-red-400">Error: {error || 'Exercise not found'}</div>;
  }

  return (
    <div className="flex flex-col h-screen bg-gray-900 text-gray-300 font-sans">
      {/* Navbar */}
      <div className="flex items-center justify-between px-4 py-3 bg-gray-950 border-b border-gray-800">
        <div className="flex items-center gap-4">
          <button onClick={() => navigate('/courses')} className="text-gray-400 hover:text-white transition-colors">
            <ChevronLeft size={20} />
          </button>
          <div className="flex items-center gap-3">
            <span className="font-semibold text-lg text-white">{exercise.title}</span>
            <span className={`text-xs px-2 py-1 rounded-full ${
              exercise.difficulty === 'Easy' ? 'bg-green-500/10 text-green-400' :
              exercise.difficulty === 'Medium' ? 'bg-yellow-500/10 text-yellow-400' :
              'bg-red-500/10 text-red-400'
            }`}>
              {exercise.difficulty}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-4">
          <button 
            onClick={handleRunCode}
            disabled={isRunning || isSubmitting}
            className="flex items-center gap-2 px-4 py-1.5 rounded-md bg-gray-800 hover:bg-gray-700 text-gray-300 transition-colors disabled:opacity-50"
          >
            {isRunning ? <div className="animate-spin h-4 w-4 border-2 border-gray-400 border-t-transparent rounded-full" /> : <Play size={16} />}
            Run
          </button>
          <button 
            onClick={handleSubmitCode}
            disabled={isRunning || isSubmitting}
            className="flex items-center gap-2 px-4 py-1.5 rounded-md bg-green-600 hover:bg-green-500 text-white transition-colors shadow-lg shadow-green-600/20 disabled:opacity-50"
          >
            {isSubmitting ? <div className="animate-spin h-4 w-4 border-2 border-white border-t-transparent rounded-full" /> : <Send size={16} />}
            Submit
          </button>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex-1 overflow-hidden">
        <PanelGroup direction="horizontal">
          {/* Left Panel - Description */}
          <Panel defaultSize={40} minSize={20} className="bg-gray-900 flex flex-col border-r border-gray-800">
            <div className="p-6 overflow-y-auto flex-1 custom-scrollbar">
              <h1 className="text-2xl font-bold text-white mb-6">{exercise.title}</h1>
              <div 
                className="prose prose-invert max-w-none text-gray-300 leading-relaxed"
                dangerouslySetInnerHTML={{ __html: exercise.description }}
              />
            </div>
          </Panel>

          <PanelResizeHandle className="w-1.5 bg-gray-800 hover:bg-blue-500/50 transition-colors cursor-col-resize active:bg-blue-500" />

          {/* Right Panel - Editor & Console */}
          <Panel defaultSize={60}>
            <PanelGroup direction="vertical">
              {/* Top - Editor */}
              <Panel defaultSize={70} className="flex flex-col bg-[#1e1e1e]">
                <div className="flex items-center justify-between px-4 py-2 bg-[#2d2d2d] border-b border-[#1e1e1e]">
                  <select 
                    value={language.id} 
                    onChange={(e) => {
                      const selected = LANGUAGES.find(l => l.id === parseInt(e.target.value));
                      if (selected) setLanguage(selected);
                    }}
                    className="bg-[#3c3c3c] text-sm text-gray-300 border-none rounded px-3 py-1 outline-none focus:ring-1 focus:ring-blue-500 cursor-pointer"
                  >
                    {LANGUAGES.map(lang => (
                      <option key={lang.id} value={lang.id}>{lang.name}</option>
                    ))}
                  </select>
                </div>
                <div className="flex-1 overflow-hidden">
                  <CodeMirror
                    value={code}
                    height="100%"
                    theme="dark"
                    extensions={
                      language.value === 'python' ? [python()] :
                      language.value === 'javascript' ? [javascript()] :
                      language.value === 'java' ? [java()] :
                      language.value === 'cpp' ? [cpp()] : []
                    }
                    onChange={(value) => setCode(value)}
                    className="h-full text-base"
                  />
                </div>
              </Panel>

              <PanelResizeHandle className="h-1.5 bg-gray-800 hover:bg-blue-500/50 transition-colors cursor-row-resize active:bg-blue-500" />

              {/* Bottom - Console */}
              <Panel defaultSize={30} className="bg-[#1e1e1e] flex flex-col">
                <div className="flex items-center gap-6 px-4 py-2 bg-[#2d2d2d] border-b border-[#1e1e1e]">
                  <button 
                    onClick={() => setActiveTab('testcases')}
                    className={`text-sm font-medium transition-colors ${activeTab === 'testcases' ? 'text-white border-b-2 border-white pb-1 -mb-[9px]' : 'text-gray-400 hover:text-gray-200'}`}
                  >
                    Test Cases
                  </button>
                  <button 
                    onClick={() => setActiveTab('result')}
                    className={`text-sm font-medium transition-colors ${activeTab === 'result' ? 'text-white border-b-2 border-white pb-1 -mb-[9px]' : 'text-gray-400 hover:text-gray-200'}`}
                  >
                    Test Result
                  </button>
                </div>
                
                <div className="flex-1 overflow-y-auto p-4 custom-scrollbar">
                  {activeTab === 'testcases' && (
                    <div className="space-y-4">
                      <div>
                        <label className="block text-xs font-semibold text-gray-400 mb-2 uppercase tracking-wider">Custom Input</label>
                        <textarea 
                          value={customInput}
                          onChange={(e) => setCustomInput(e.target.value)}
                          className="w-full h-32 bg-[#2d2d2d] border border-gray-700 rounded-md p-3 text-sm text-gray-300 font-mono focus:outline-none focus:border-blue-500 transition-colors resize-none"
                        />
                      </div>
                    </div>
                  )}

                  {activeTab === 'result' && (
                    <div className="space-y-4">
                      {!runResult && !submitResult && !isRunning && !isSubmitting && (
                        <div className="text-gray-500 text-sm italic">Run or submit code to see results.</div>
                      )}

                      {isRunning && <div className="text-blue-400 text-sm animate-pulse">Running code...</div>}
                      {isSubmitting && <div className="text-blue-400 text-sm animate-pulse">Evaluating submission...</div>}

                      {runResult && (
                        <div className="bg-[#2d2d2d] rounded-lg p-4 border border-gray-800">
                          <div className="flex items-center gap-3 mb-4">
                            {runResult.status.id === 3 ? (
                              <CheckCircle2 className="text-green-500" size={20} />
                            ) : (
                              <XCircle className="text-red-500" size={20} />
                            )}
                            <span className={`font-semibold ${runResult.status.id === 3 ? 'text-green-500' : 'text-red-500'}`}>
                              {runResult.status.description}
                            </span>
                          </div>
                          
                          <div className="grid grid-cols-2 gap-4 mb-4 text-sm">
                            <div className="flex items-center gap-2 text-gray-400">
                              <Clock size={14} /> <span>Time: {runResult.time}s</span>
                            </div>
                            <div className="flex items-center gap-2 text-gray-400">
                              <Cpu size={14} /> <span>Memory: {runResult.memory}KB</span>
                            </div>
                          </div>

                          {runResult.compileOutput && (
                            <div className="mb-4">
                              <div className="text-xs text-gray-500 mb-1">Compiler Output</div>
                              <pre className="bg-[#1e1e1e] p-3 rounded text-red-400 text-sm font-mono overflow-x-auto">
                                {runResult.compileOutput}
                              </pre>
                            </div>
                          )}

                          {runResult.stdout && (
                            <div>
                              <div className="text-xs text-gray-500 mb-1">Standard Output</div>
                              <pre className="bg-[#1e1e1e] p-3 rounded text-gray-300 text-sm font-mono overflow-x-auto">
                                {runResult.stdout}
                              </pre>
                            </div>
                          )}
                          
                          {runResult.stderr && (
                            <div className="mt-4">
                              <div className="text-xs text-gray-500 mb-1">Standard Error</div>
                              <pre className="bg-[#1e1e1e] p-3 rounded text-red-400 text-sm font-mono overflow-x-auto">
                                {runResult.stderr}
                              </pre>
                            </div>
                          )}
                        </div>
                      )}

                      {submitResult && (
                        <div className="space-y-4">
                          <div className="flex items-center gap-3 bg-[#2d2d2d] p-4 rounded-lg border border-gray-800">
                            {submitResult.status === 'Accepted' ? (
                              <CheckCircle2 className="text-green-500" size={24} />
                            ) : (
                              <XCircle className="text-red-500" size={24} />
                            )}
                            <span className={`text-xl font-bold ${submitResult.status === 'Accepted' ? 'text-green-500' : 'text-red-500'}`}>
                              {submitResult.status}
                            </span>
                          </div>
                          
                          <div className="space-y-2">
                            {submitResult.testResults.map((tr, idx) => (
                              <div key={tr.testCaseId} className="bg-[#2d2d2d] rounded-lg p-3 border border-gray-800 flex items-center justify-between">
                                <div className="flex items-center gap-3">
                                  {tr.passed ? (
                                    <CheckCircle2 className="text-green-500" size={16} />
                                  ) : (
                                    <XCircle className="text-red-500" size={16} />
                                  )}
                                  <span className="text-sm font-medium text-gray-300">Test Case {idx + 1}</span>
                                </div>
                                <div className="text-xs text-gray-500">
                                  {tr.runResult.time}s • {tr.runResult.memory}KB
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </Panel>
            </PanelGroup>
          </Panel>
        </PanelGroup>
      </div>

      <style dangerouslySetInnerHTML={{__html: `
        .custom-scrollbar::-webkit-scrollbar { width: 8px; height: 8px; }
        .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
        .custom-scrollbar::-webkit-scrollbar-thumb { background: #4b5563; border-radius: 4px; }
        .custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #6b7280; }
      `}} />
    </div>
  );
};
