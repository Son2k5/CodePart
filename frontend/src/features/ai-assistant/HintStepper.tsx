import { LockKeyhole } from 'lucide-react';
export function HintStepper({ unlockedLevel, currentLevel, busy, remaining, onSelect }: { unlockedLevel: number; currentLevel?: number; busy: boolean; remaining?: number; onSelect: (level: number) => void }) {
  return <div><nav aria-label="Cấp gợi ý" className="flex items-center gap-[12px] flex-wrap">{[1, 2, 3].map(level => {
    const locked = level > unlockedLevel + 1;
    return <button key={level} disabled={busy || locked || remaining === 0} aria-pressed={currentLevel === level} title={locked ? 'Thử sửa và nộp thêm bài, hoặc chờ để mở gợi ý tiếp theo.' : undefined} onClick={() => onSelect(level)} className={`inline-flex items-center gap-1.5 px-[14px] py-[9px] rounded-[10px] border text-[#66538c] text-sm cursor-pointer transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${currentLevel === level ? 'bg-[#e9e2fa] border-[#b4a2d3]' : 'bg-white border-[#ddd6ee] hover:bg-[#f6f2fc]'}`}>{locked && <LockKeyhole size={13} />}Gợi ý {level}</button>;
  })}</nav>{unlockedLevel < 2 && <p className="text-xs text-[#64748b] mt-2">🔒 Cấp tiếp theo mở theo tiến độ làm bài. Thử sửa, nộp thêm hoặc quay lại sau nhé.</p>}</div>;
}
