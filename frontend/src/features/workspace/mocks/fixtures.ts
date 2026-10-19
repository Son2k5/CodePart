import type { Problem, Submission, TestResult } from '../../code-judge/api';
export const demoProblem: Problem = {
  id: 'demo-sum', title: 'Tổng hai số', difficulty: 'Easy', points: 100,
  timeLimitMs: 1000, memoryLimitMb: 128, classId: 'demo-class', courseId: null, status: 'Published', comparisonMode: 0,
  description: '## Cùng bắt đầu thật nhẹ nhàng\n\nMầm có hai giỏ táo, giỏ thứ nhất có **a** quả và giỏ thứ hai có **b** quả. Bạn hãy giúp Mầm tính tổng số táo nhé!\n\nCho hai số nguyên `a` và `b`. Viết chương trình tính và in ra tổng của chúng.\n\n### Dữ liệu vào\nMột dòng chứa hai số nguyên `a` và `b`, cách nhau bởi một dấu cách.\n\n### Dữ liệu ra\nIn ra một số nguyên duy nhất — tổng của `a` và `b`.\n\n### Giới hạn\n- `−10⁹ ≤ a, b ≤ 10⁹`\n- Đừng quên thử với số âm và số 0 nhé.',
  starterCodes: [
    { language: 'python', template: '# Cùng giúp Mầm đếm táo nhé! 🌱\n# Đọc hai số nguyên từ bàn phím\na, b = map(int, input().split())\n\n# Viết lời giải của bạn ở đây\n\n' },
    { language: 'cpp', template: '#include <iostream>\nusing namespace std;\n\nint main() {\n    long long a, b;\n    cin >> a >> b;\n    // Viết lời giải của bạn ở đây\n\n    return 0;\n}\n' },
    { language: 'java', template: 'import java.util.Scanner;\n\npublic class Main {\n    public static void main(String[] args) {\n        Scanner sc = new Scanner(System.in);\n        long a = sc.nextLong(), b = sc.nextLong();\n        // Viết lời giải của bạn ở đây\n    }\n}\n' },
    { language: 'javascript', template: "const fs = require('fs');\nconst [a, b] = fs.readFileSync(0, 'utf8').trim().split(/\\s+/).map(Number);\n\n// Viết lời giải của bạn ở đây\n" },
  ],
  testCases: [{ id: 'sample-1', input: '2 3', expectedOutput: '5', isSample: true, order: 1, weight: 1 }, { id: 'sample-2', input: '-4 7', expectedOutput: '3', isSample: true, order: 2, weight: 1 }],
};
export const scenarios = ['Accepted', 'WrongAnswer', 'CompileError', 'RuntimeError', 'TimeLimitExceeded', 'MemoryLimitExceeded', 'PartialPass', 'LongQueue', 'JUDGE_BUSY', 'RATE_LIMITED', 'SUBMISSION_IN_PROGRESS'] as const;
export type Scenario = typeof scenarios[number];
export function makeResults(verdict: string, count = 15): TestResult[] {
  return Array.from({ length: count }, (_, i) => {
    const passed = verdict === 'Accepted' || (verdict === 'PartialPass' && i < 8);
    const result: TestResult = { index: i + 1, isSample: i < 2, verdict: passed ? 'Accepted' : verdict === 'PartialPass' ? 'WrongAnswer' : verdict, runtimeMs: 12 + i };
    if (i < 2) Object.assign(result, { input: demoProblem.testCases[i].input, expectedOutput: demoProblem.testCases[i].expectedOutput, actualOutput: passed ? demoProblem.testCases[i].expectedOutput : '0', ...(['CompileError', 'RuntimeError'].includes(verdict) ? { errorMessage: 'Dòng 5: kiểm tra tên biến và câu lệnh xuất kết quả.', line: 5 } : {}) });
    return result;
  });
}
export const demoSubmission: Submission = { id: 'design-example', problemId: demoProblem.id, status: 'Accepted', language: 'python', passedCount: 15, totalCount: 15, score: 100, awardedPoints: 100, createdAt: new Date().toISOString(), results: makeResults('Accepted') };
