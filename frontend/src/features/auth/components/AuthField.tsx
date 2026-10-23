import { forwardRef, useId, useState } from 'react';
import type { InputHTMLAttributes } from 'react';
import { Eye, EyeOff } from 'lucide-react';

interface AuthFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
}

export const AuthField = forwardRef<HTMLInputElement, AuthFieldProps>(
  ({ label, error, hint, type = 'text', id, ...props }, ref) => {
    const generatedId = useId();
    const fieldId = id ?? generatedId;
    const [visible, setVisible] = useState(false);
    const isPassword = type === 'password';
    return (
      <div className="grid gap-2">
        <label htmlFor={fieldId} className="text-xs font-semibold text-[#4b666c]">{label}</label>
        <div className={isPassword ? 'relative [&>input]:pr-12' : undefined}>
          <input {...props} ref={ref} id={fieldId} type={isPassword && visible ? 'text' : type} className="block w-full min-w-0 border border-[#dce8e9] rounded-[12px] px-[15px] py-[13px] text-sm text-[#354f55] bg-[#fbfdfd] transition-all placeholder:text-[#819396] focus:outline-none focus:border-[#70aeb9] focus:ring-3 focus:ring-[#e6f5f8] focus:bg-white aria-invalid:border-[#d65c5c]" aria-invalid={!!error} aria-describedby={[error && `${fieldId}-error`, hint && `${fieldId}-hint`].filter(Boolean).join(' ') || undefined} />
          {isPassword && <button type="button" className="absolute right-[7px] top-1/2 -translate-y-1/2 grid place-items-center w-9 h-9 rounded-lg text-[#60858c] hover:bg-[#edf7f8] cursor-pointer" onClick={() => setVisible(!visible)} aria-label={`${visible ? 'Ẩn' : 'Hiện'} ${label.toLowerCase()}`} aria-pressed={visible}>{visible ? <EyeOff size={17} /> : <Eye size={17} />}</button>}
        </div>
        {hint && <p className="text-[#6b7f83] text-[11px] leading-[1.7]" id={`${fieldId}-hint`}>{hint}</p>}
        {error && <p className="text-[#b43f3f] text-xs leading-[1.6]" id={`${fieldId}-error`} role="alert">{error}</p>}
      </div>
    );
  },
);
AuthField.displayName = 'AuthField';
