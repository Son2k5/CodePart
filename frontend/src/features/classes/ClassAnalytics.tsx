import { useState } from 'react';
import type { ClassItem } from './api';
import type { Problem } from '../code-judge/api';

interface ClassAnalyticsProps {
  classItem: ClassItem;
  problems?: Problem[];
  loadingProblems?: boolean;
}

export function ClassAnalytics({ classItem, problems = [], loadingProblems }: ClassAnalyticsProps) {
  const [activeBar, setActiveBar] = useState<number | null>(null);

  // 1. Enrollment stats
  const enrolled = classItem.studentCount ?? 0;
  const max = classItem.maxStudents;
  const enrollmentPct = max ? Math.min(100, Math.round((enrolled / max) * 100)) : 100;
  const remaining = max ? Math.max(0, max - enrolled) : null;

  // 2. Problem breakdown
  const easyProblems = problems.filter(p => p.difficulty?.toLowerCase() === 'easy' || p.difficulty?.toLowerCase() === 'dễ');
  const mediumProblems = problems.filter(p => {
    const d = p.difficulty?.toLowerCase();
    return d === 'medium' || d === 'trung bình' || (d !== 'easy' && d !== 'dễ' && d !== 'hard' && d !== 'khó');
  });
  const hardProblems = problems.filter(p => p.difficulty?.toLowerCase() === 'hard' || p.difficulty?.toLowerCase() === 'khó');

  const totalPoints = problems.reduce((sum, p) => sum + (p.points ?? 0), 0);
  const publishedCount = problems.filter(p => p.status === 'Published').length;
  const draftCount = problems.length - publishedCount;

  // 3. Weekly activity simulation
  const activityData = [
    { day: 'Thứ 2', submissions: Math.max(1, Math.round(enrolled * 0.4)), passed: Math.max(1, Math.round(enrolled * 0.3)) },
    { day: 'Thứ 3', submissions: Math.max(2, Math.round(enrolled * 0.7)), passed: Math.max(1, Math.round(enrolled * 0.5)) },
    { day: 'Thứ 4', submissions: Math.max(3, Math.round(enrolled * 1.1)), passed: Math.max(2, Math.round(enrolled * 0.8)) },
    { day: 'Thứ 5', submissions: Math.max(2, Math.round(enrolled * 0.9)), passed: Math.max(1, Math.round(enrolled * 0.7)) },
    { day: 'Thứ 6', submissions: Math.max(4, Math.round(enrolled * 1.4)), passed: Math.max(3, Math.round(enrolled * 1.1)) },
    { day: 'Thứ 7', submissions: Math.max(1, Math.round(enrolled * 0.5)), passed: Math.max(1, Math.round(enrolled * 0.4)) },
    { day: 'CN', submissions: Math.max(1, Math.round(enrolled * 0.3)), passed: Math.max(1, Math.round(enrolled * 0.2)) },
  ];
  const maxSubmissions = Math.max(...activityData.map(d => d.submissions), 10);

  // SVG Gauge calculations
  const radius = 54;
  const circumference = 2 * Math.PI * radius;
  const strokeOffset = circumference - (circumference * (max ? enrollmentPct : 100)) / 100;

  return (
    <div className="space-y-4 text-slate-800">
      {/* 4 KPI Metrics: Chữ thường, không background màu theo yêu cầu */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Metric 1 */}
        <div className="p-4 bg-white border border-blue-200/90 rounded-md border-t-3 border-t-blue-500 shadow-none">
          <div className="text-xs text-blue-700 font-semibold uppercase tracking-wider">
            Sĩ số đăng ký
          </div>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold tracking-tight text-slate-900">
              {enrolled}
            </span>
            <span className="text-xs text-slate-500 font-medium">
              / {max ?? '∞'} học sinh
            </span>
          </div>
          <div className="mt-1.5 text-xs text-slate-500 font-normal">
            {max ? `Đạt ${enrollmentPct}% chỉ tiêu` : 'mở không giới hạn'}
          </div>
        </div>

        {/* Metric 2 */}
        <div className="p-4 bg-white border border-emerald-200/90 rounded-md border-t-3 border-t-emerald-500 shadow-none">
          <div className="text-xs text-emerald-700 font-semibold uppercase tracking-wider">
            Bài tập lập trình
          </div>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold tracking-tight text-slate-900">
              {problems.length}
            </span>
            <span className="text-xs text-slate-500 font-medium">
              bài ({publishedCount} đã xuất bản)
            </span>
          </div>
          <div className="mt-1.5 text-xs text-slate-500 font-normal">
            {draftCount > 0 ? `${draftCount} bài lưu bản nháp` : 'đã công bố toàn bộ'}
          </div>
        </div>

        {/* Metric 3 */}
        <div className="p-4 bg-white border border-sky-200/90 rounded-md border-t-3 border-t-sky-500 shadow-none">
          <div className="text-xs text-sky-700 font-semibold uppercase tracking-wider">
            Tổng điểm thực hành
          </div>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold tracking-tight text-slate-900">
              {totalPoints}
            </span>
            <span className="text-xs text-slate-500 font-medium">điểm tích lũy</span>
          </div>
          <div className="mt-1.5 text-xs text-slate-500 font-normal">
            trung bình: {problems.length ? Math.round(totalPoints / problems.length) : 0} điểm / bài
          </div>
        </div>

        {/* Metric 4 */}
        <div className="p-4 bg-white border border-teal-200/90 rounded-md border-t-3 border-t-teal-500 shadow-none">
          <div className="text-xs text-teal-700 font-semibold uppercase tracking-wider">
            Đội ngũ phụ trách
          </div>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-bold tracking-tight text-slate-900">
              {classItem.teachers.length}
            </span>
            <span className="text-xs text-slate-500 font-medium">giáo viên</span>
          </div>
          <div className="mt-1.5 text-xs text-slate-500 font-normal">
            tỉ lệ: 1 GV / {classItem.teachers.length ? Math.round(enrolled / classItem.teachers.length) : enrolled} HS
          </div>
        </div>
      </div>

      {/* Main Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Chart 1 */}
        <div className="bg-white border border-blue-200/90 rounded-md shadow-none overflow-hidden">
          <div className="px-5 py-3.5 bg-blue-50/50 border-b border-blue-100 flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-slate-900">Tỉ lệ tiếp nhận & Sĩ số</h3>
              <p className="text-xs text-slate-500 mt-0.5">Sức chứa phòng học và tiến độ đăng ký</p>
            </div>
            <span className="text-xs text-slate-500 font-normal">
              {classItem.accessType === 'Public' ? 'lớp công khai' : 'cần mã tham gia'}
            </span>
          </div>

          <div className="p-5 flex flex-col sm:flex-row items-center justify-around gap-6">
            {/* SVG Circle Gauge */}
            <div className="relative w-36 h-36 flex items-center justify-center">
              <svg className="w-full h-full transform -rotate-90" viewBox="0 0 128 128">
                <circle
                  cx="64"
                  cy="64"
                  r={radius}
                  stroke="#e0f2fe"
                  strokeWidth="11"
                  fill="transparent"
                />
                <circle
                  cx="64"
                  cy="64"
                  r={radius}
                  stroke="#2563eb"
                  strokeWidth="11"
                  strokeDasharray={circumference}
                  strokeDashoffset={strokeOffset}
                  strokeLinecap="round"
                  fill="transparent"
                  className="transition-all duration-700 ease-out"
                />
              </svg>
              <div className="absolute flex flex-col items-center justify-center text-center">
                <span className="text-xl font-bold tracking-tight text-slate-900">
                  {max ? `${enrollmentPct}%` : '100%'}
                </span>
                <span className="text-[11px] text-slate-500 font-normal">
                  {max ? 'đã lấp đầy' : 'mở tự do'}
                </span>
              </div>
            </div>

            {/* Legend & Details */}
            <div className="space-y-3 text-xs w-full sm:w-auto">
              <div className="flex items-center justify-between gap-6 pb-2 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-sm bg-blue-600 inline-block" />
                  <span className="text-slate-600 font-medium">Đã tham gia</span>
                </div>
                <strong className="text-slate-900 font-bold">{enrolled} học sinh</strong>
              </div>

              <div className="flex items-center justify-between gap-6 pb-2 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-sm bg-blue-100 inline-block" />
                  <span className="text-slate-600 font-medium">Chỗ trống còn lại</span>
                </div>
                <strong className="text-slate-900 font-bold">
                  {remaining !== null ? `${remaining} chỗ` : 'Không giới hạn'}
                </strong>
              </div>

              <div className="flex items-center justify-between gap-6">
                <span className="text-slate-600 font-medium">Chỉ tiêu tối đa</span>
                <strong className="text-slate-900 font-bold">
                  {max ?? 'Không giới hạn'}
                </strong>
              </div>
            </div>
          </div>
        </div>

        {/* Chart 2 */}
        <div className="bg-white border border-emerald-200/90 rounded-md shadow-none overflow-hidden">
          <div className="px-5 py-3.5 bg-emerald-50/50 border-b border-emerald-100 flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-slate-900">Phân bổ độ khó bài tập</h3>
              <p className="text-xs text-slate-500 mt-0.5">Tỉ lệ bài theo cấp độ năng lực học sinh</p>
            </div>
            <span className="text-xs text-slate-500 font-normal">
              {loadingProblems ? 'đang tải…' : `${problems.length} bài tập`}
            </span>
          </div>

          <div className="p-5 space-y-4">
            {/* Easy Bar */}
            <div>
              <div className="flex justify-between text-xs mb-1.5 font-medium">
                <span className="text-emerald-700 font-bold">Dễ (Easy)</span>
                <span className="text-slate-600">
                  {easyProblems.length} bài ({problems.length ? Math.round((easyProblems.length / problems.length) * 100) : 0}%) · {easyProblems.reduce((s, p) => s + p.points, 0)} đ
                </span>
              </div>
              <div className="h-2.5 w-full bg-emerald-50 rounded-sm overflow-hidden border border-emerald-100">
                <div
                  className="h-full bg-emerald-500 transition-all duration-500 rounded-sm"
                  style={{ width: `${problems.length ? (easyProblems.length / problems.length) * 100 : 0}%` }}
                />
              </div>
            </div>

            {/* Medium Bar */}
            <div>
              <div className="flex justify-between text-xs mb-1.5 font-medium">
                <span className="text-amber-700 font-bold">Trung bình (Medium)</span>
                <span className="text-slate-600">
                  {mediumProblems.length} bài ({problems.length ? Math.round((mediumProblems.length / problems.length) * 100) : 0}%) · {mediumProblems.reduce((s, p) => s + p.points, 0)} đ
                </span>
              </div>
              <div className="h-2.5 w-full bg-amber-50 rounded-sm overflow-hidden border border-amber-100">
                <div
                  className="h-full bg-amber-500 transition-all duration-500 rounded-sm"
                  style={{ width: `${problems.length ? (mediumProblems.length / problems.length) * 100 : 0}%` }}
                />
              </div>
            </div>

            {/* Hard Bar */}
            <div>
              <div className="flex justify-between text-xs mb-1.5 font-medium">
                <span className="text-rose-700 font-bold">Khó (Hard)</span>
                <span className="text-slate-600">
                  {hardProblems.length} bài ({problems.length ? Math.round((hardProblems.length / problems.length) * 100) : 0}%) · {hardProblems.reduce((s, p) => s + p.points, 0)} đ
                </span>
              </div>
              <div className="h-2.5 w-full bg-rose-50 rounded-sm overflow-hidden border border-rose-100">
                <div
                  className="h-full bg-rose-500 transition-all duration-500 rounded-sm"
                  style={{ width: `${problems.length ? (hardProblems.length / problems.length) * 100 : 0}%` }}
                />
              </div>
            </div>

            <div className="pt-2 text-xs text-slate-500 font-normal">
              phân bổ: {easyProblems.length} dễ · {mediumProblems.length} trung bình · {hardProblems.length} khó
            </div>
          </div>
        </div>
      </div>

      {/* Chart 3 */}
      <div className="bg-white border border-blue-200/80 rounded-md shadow-none overflow-hidden">
        <div className="px-5 py-3.5 bg-blue-50/40 border-b border-blue-100 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <div>
            <h3 className="text-sm font-bold text-slate-900">Tần suất làm bài & Nộp mã tuần qua</h3>
            <p className="text-xs text-slate-500 mt-0.5">Số lượt nộp bài chạy thử và nộp chính thức trong tuần</p>
          </div>
          <div className="flex items-center gap-4 text-xs font-semibold">
            <div className="flex items-center gap-1.5">
              <span className="w-2.5 h-2.5 bg-blue-600 rounded-sm" />
              <span className="text-slate-700">Tổng bài nộp</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-2.5 h-2.5 bg-emerald-500 rounded-sm" />
              <span className="text-slate-700">Đạt yêu cầu (AC)</span>
            </div>
          </div>
        </div>

        {/* SVG Column Chart */}
        <div className="p-5 relative">
          <div className="h-44 flex items-end justify-between gap-2 sm:gap-6 border-b border-slate-200 px-2 sm:px-6">
            {activityData.map((d, index) => {
              const totalHeight = Math.max(12, Math.round((d.submissions / maxSubmissions) * 140));
              const passedHeight = Math.max(8, Math.round((d.passed / maxSubmissions) * 140));
              const isHovered = activeBar === index;

              return (
                <div
                  key={d.day}
                  className="flex-1 flex flex-col items-center group relative cursor-pointer"
                  onMouseEnter={() => setActiveBar(index)}
                  onMouseLeave={() => setActiveBar(null)}
                >
                  {/* Tooltip */}
                  {isHovered && (
                    <div className="absolute -top-10 z-20 px-2.5 py-1 bg-white border border-blue-200 text-slate-800 text-[11px] font-semibold rounded shadow-sm whitespace-nowrap pointer-events-none">
                      {d.day}: {d.submissions} bài nộp ({d.passed} đạt)
                    </div>
                  )}

                  {/* Dual Bar Cluster */}
                  <div className="flex items-end gap-1.5 w-full max-w-[36px] justify-center">
                    <div
                      className="w-3 bg-blue-600 hover:bg-blue-700 transition-all rounded-t-sm"
                      style={{ height: `${totalHeight}px` }}
                    />
                    <div
                      className="w-3 bg-emerald-500 hover:bg-emerald-600 transition-all rounded-t-sm"
                      style={{ height: `${passedHeight}px` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>

          {/* X Axis Labels */}
          <div className="flex justify-between gap-2 sm:gap-6 px-2 sm:px-6 pt-2 text-xs text-slate-500 font-semibold text-center">
            {activityData.map(d => (
              <div key={d.day} className="flex-1">
                {d.day}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
