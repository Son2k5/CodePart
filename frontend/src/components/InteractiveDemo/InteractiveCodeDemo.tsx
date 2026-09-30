import React, { useState } from 'react'
import { Play, CheckCircle2, RotateCcw, Sparkles } from 'lucide-react'

interface CodeSample {
  id: string
  language: string
  title: string
  code: string[]
  expectedOutput: string
  testCases: { input: string; output: string; status: 'passed' }[]
}

const SAMPLE_EXERCISES: CodeSample[] = [
  {
    id: 'python-1',
    language: 'Python',
    title: 'Bài tập 01: Thuật toán tìm kiếm nhị phân',
    code: [
      'def binary_search(arr, target):',
      '    # Sinh viên cài đặt thuật toán tìm kiếm nhị phân',
      '    left, right = 0, len(arr) - 1',
      '    while left <= right:',
      '        mid = (left + right) // 2',
      '        if arr[mid] == target:',
      '            return mid',
      '        elif arr[mid] < target:',
      '            left = mid + 1',
      '        else:',
      '            right = mid - 1',
      '    return -1',
      '',
      '# Test:',
      'print(binary_search([2, 5, 8, 12, 16, 23, 38], 16)) # KQ: 4',
    ],
    expectedOutput: '4',
    testCases: [
      { input: 'arr=[1,2,3,4,5], target=3', output: '2', status: 'passed' },
      { input: 'arr=[10,20,30], target=15', output: '-1', status: 'passed' },
      { input: 'arr=[5,8,12,14], target=14', output: '3', status: 'passed' },
    ],
  },
  {
    id: 'java-oop-1',
    language: 'Java OOP',
    title: 'Bài tập 02: Kế thừa và Đa hình (Polymorphism)',
    code: [
      'class Animal {',
      '    public void makeSound() {',
      '        System.out.println("Animal sound");',
      '    }',
      '}',
      '',
      'class Dog extends Animal {',
      '    @Override',
      '    public void makeSound() {',
      '        System.out.println("Woof");',
      '    }',
      '}',
    ],
    expectedOutput: 'Woof',
    testCases: [
      { input: 'Dog dog = new Dog(); dog.makeSound();', output: 'Woof', status: 'passed' },
      { input: 'Animal a = new Dog(); a.makeSound();', output: 'Woof', status: 'passed' },
      { input: 'Check Override Annotation', output: 'Passed', status: 'passed' },
    ],
  },
  {
    id: 'js-1',
    language: 'JavaScript',
    title: 'Bài tập 03: Xử lý mảng (Array Methods)',
    code: [
      'function getActiveUsers(users) {',
      '  // Lọc ra các user có trạng thái "active"',
      '  return users.filter(user => user.status === "active")',
      '              .map(user => user.name);',
      '}',
      '',
      'const data = [',
      '  { name: "Alice", status: "active" },',
      '  { name: "Bob", status: "inactive" }',
      '];',
      'console.log(getActiveUsers(data)); // ["Alice"]'
    ],
    expectedOutput: '["Alice"]',
    testCases: [
      { input: 'Empty array', output: '[]', status: 'passed' },
      { input: 'All inactive', output: '[]', status: 'passed' },
      { input: 'Multiple active', output: '["A", "C"]', status: 'passed' },
    ],
  },
]

export const InteractiveCodeDemo: React.FC = () => {
  const [selectedExercise, setSelectedExercise] = useState<CodeSample>(SAMPLE_EXERCISES[0])
  const [isRunning, setIsRunning] = useState(false)
  const [hasRun, setHasRun] = useState(false)

  const handleRunTest = () => {
    setIsRunning(true)
    setTimeout(() => {
      setIsRunning(false)
      setHasRun(true)
    }, 450)
  }

  const handleReset = () => {
    setHasRun(false)
  }

  return (
    <section id="interactive-demo" className="py-12 bg-slate-50 border-y border-slate-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-10">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white border border-slate-200 text-blue-700 text-xs font-bold uppercase mb-3 shadow-sm">
            <Sparkles className="w-3.5 h-3.5 text-blue-600" />
            <span>Trải nghiệm môi trường thực hành</span>
          </div>
          <h2 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight mb-3">
            Sinh viên code và nhận kết quả tức thì không cần cài đặt
          </h2>
          <p className="text-sm sm:text-base text-slate-600">
            Hệ thống tự động biên dịch, chạy test case và phân tích lỗi logic theo thời gian thực.
          </p>
        </div>

        {/* Code Editor Preview Window */}
        <div className="max-w-4xl mx-auto bg-white rounded-2xl border border-slate-200 shadow-md overflow-hidden">
          {/* Header Bar */}
          <div className="flex flex-wrap items-center justify-between px-4 py-3 bg-white border-b border-slate-200 gap-3">
            {/* Window Dots & Exercise tabs */}
            <div className="flex items-center gap-4">
              <div className="hidden sm:flex items-center gap-1.5">
                <div className="w-3 h-3 rounded-full bg-slate-200" />
                <div className="w-3 h-3 rounded-full bg-slate-200" />
                <div className="w-3 h-3 rounded-full bg-slate-200" />
              </div>

              {/* Language Tabs */}
              <div className="flex items-center gap-1.5">
                {SAMPLE_EXERCISES.map((ex) => (
                  <button
                    key={ex.id}
                    onClick={() => {
                      setSelectedExercise(ex)
                      setHasRun(false)
                    }}
                    className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition ${
                      selectedExercise.id === ex.id
                        ? 'bg-blue-600 text-white shadow-sm'
                        : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
                    }`}
                  >
                    {ex.language}
                  </button>
                ))}
              </div>
            </div>

            {/* Run Button */}
            <div className="flex items-center gap-2">
              {hasRun && (
                <button
                  onClick={handleReset}
                  className="p-1.5 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-lg transition"
                  title="Đặt lại"
                >
                  <RotateCcw className="w-4 h-4" />
                </button>
              )}
              <button
                onClick={handleRunTest}
                disabled={isRunning}
                className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white text-xs font-bold rounded-lg shadow-sm transition"
              >
                <Play className="w-3.5 h-3.5 fill-current" />
                <span>{isRunning ? 'Đang chấm test...' : 'Chạy thử nộp bài'}</span>
              </button>
            </div>
          </div>

          {/* Main Area: Code Editor + Test Case Output */}
          <div className="grid grid-cols-1 md:grid-cols-12 divide-y md:divide-y-0 md:divide-x divide-slate-200">
            {/* Code Lines */}
            <div className="md:col-span-7 p-4 sm:p-5 bg-white font-mono text-xs sm:text-sm leading-relaxed overflow-x-auto text-slate-800">
              <div className="text-slate-400 text-xs font-sans font-medium mb-3 pb-2 border-b border-slate-100">
                // {selectedExercise.title}
              </div>
              {selectedExercise.code.map((line, idx) => (
                <div key={idx} className="flex gap-4">
                  <span className="w-5 text-right text-slate-400 select-none text-xs">{idx + 1}</span>
                  <span className="whitespace-pre">
                    {line.startsWith('#') || line.startsWith('//') ? (
                      <span className="text-slate-400 italic">{line}</span>
                    ) : line.includes('def ') || line.includes('return ') || line.includes('for ') ? (
                      <span className="text-blue-700 font-semibold">{line}</span>
                    ) : (
                      line
                    )}
                  </span>
                </div>
              ))}
            </div>

            {/* Test Results Output Pane */}
            <div className="md:col-span-5 p-4 sm:p-5 bg-slate-50 flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-3 pb-2 border-b border-slate-200">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-700">
                    Kết quả kiểm thử tự động
                  </span>
                  <span className="text-xs font-semibold text-slate-500">Auto-Grader</span>
                </div>

                {hasRun ? (
                  <div className="space-y-2.5">
                    <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-800">
                      <div className="flex items-center gap-2 font-bold text-xs sm:text-sm">
                        <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                        <span>Chấm điểm hoàn tất: 100/100 Điểm</span>
                      </div>
                      <p className="text-xs text-emerald-700 mt-1">
                        Tất cả các bộ dữ liệu kiểm thử đều cho kết quả chính xác (0.03s).
                      </p>
                    </div>

                    <div className="space-y-2 mt-3">
                      {selectedExercise.testCases.map((tc, i) => (
                        <div
                          key={i}
                          className="flex items-center justify-between p-2.5 bg-white border border-slate-200 rounded-lg text-xs"
                        >
                          <span className="font-mono text-slate-600">Test {i + 1}: {tc.input}</span>
                          <span className="inline-flex items-center gap-1 font-semibold text-emerald-600">
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            <span>Khớp ({tc.output})</span>
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="py-8 text-center text-slate-500 text-xs">
                    <p className="mb-2">Nhấn nút "Chạy thử nộp bài" phía trên</p>
                    <p className="text-slate-400">để mô phỏng hệ thống chấm bài trực tiếp của CodePath.</p>
                  </div>
                )}
              </div>

              <div className="mt-4 pt-3 border-t border-slate-200 flex items-center justify-between text-xs text-slate-500">
                <span>Bộ nhớ: 14.2 MB</span>
                <span>Thời gian chạy: &lt; 0.05s</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
