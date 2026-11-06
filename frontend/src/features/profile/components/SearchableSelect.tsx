import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';

interface Option {
  id: string;
  name: string;
}

interface SearchableSelectProps {
  label: string;
  value: string | null;
  options: Option[];
  placeholder?: string;
  error?: string;
  disabled?: boolean;
  onChange: (value: string | null) => void;
}

export function SearchableSelect({
  label,
  value,
  options,
  placeholder = 'Tìm chọn…',
  error,
  disabled,
  onChange,
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const rootRef = useRef<HTMLDivElement>(null);

  const selected = options.find((o) => o.id === value) ?? null;
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) => o.name.toLowerCase().includes(q));
  }, [options, query]);

  useEffect(() => {
    const onDoc = (e: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onDoc);
    return () => document.removeEventListener('pointerdown', onDoc);
  }, []);

  return (
    <div className="space-y-1.5" ref={rootRef}>
      <label className="text-xs font-semibold text-[#3e656b]">{label}</label>
      <div className="relative">
        <button
          type="button"
          disabled={disabled}
          onClick={() => setOpen((v) => !v)}
          aria-invalid={!!error}
          className={`flex w-full items-center justify-between gap-2 rounded-xl border bg-white px-3.5 py-2.5 text-left text-sm transition-colors disabled:cursor-not-allowed disabled:bg-slate-50 ${
            error ? 'border-rose-300' : 'border-[#cddfe3] hover:border-[#9fc3cb]'
          }`}
        >
          <span className={selected ? 'text-slate-800' : 'text-slate-400'}>{selected ? selected.name : placeholder}</span>
          <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" />
        </button>

        {open && !disabled && (
          <div className="cp-fade-in absolute z-30 mt-1 w-full overflow-hidden rounded-xl border border-[#dfecee] bg-white shadow-xl">
            <div className="flex items-center gap-2 border-b border-slate-100 px-3">
              <Search className="h-4 w-4 text-slate-400" />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Nhập để tìm…"
                className="w-full bg-transparent py-2 text-sm outline-none"
              />
            </div>
            <div className="max-h-56 overflow-y-auto py-1">
              <button
                type="button"
                onClick={() => {
                  onChange(null);
                  setOpen(false);
                }}
                className="flex w-full items-center px-3 py-2 text-sm text-slate-500 hover:bg-[#edf9fa]"
              >
                (Không chọn)
              </button>
              {filtered.map((o) => (
                <button
                  key={o.id}
                  type="button"
                  onClick={() => {
                    onChange(o.id);
                    setOpen(false);
                  }}
                  className="flex w-full items-center justify-between px-3 py-2 text-left text-sm text-slate-700 hover:bg-[#edf9fa]"
                >
                  <span className="truncate">{o.name}</span>
                  {o.id === value && <Check className="h-4 w-4 shrink-0 text-[#397c8c]" />}
                </button>
              ))}
              {!filtered.length && <p className="px-3 py-6 text-center text-sm text-slate-400">Không có kết quả</p>}
            </div>
          </div>
        )}
      </div>
      {error && <p className="text-xs text-rose-600">{error}</p>}
    </div>
  );
}
