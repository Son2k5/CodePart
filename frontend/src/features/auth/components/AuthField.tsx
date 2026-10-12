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
      <div className="auth-field">
        <label htmlFor={fieldId}>{label}</label>
        <div className={isPassword ? 'auth-password' : undefined}>
          <input {...props} ref={ref} id={fieldId} type={isPassword && visible ? 'text' : type} className="auth-input" aria-invalid={!!error} aria-describedby={[error && `${fieldId}-error`, hint && `${fieldId}-hint`].filter(Boolean).join(' ') || undefined} />
          {isPassword && <button type="button" className="auth-password-toggle" onClick={() => setVisible(!visible)} aria-label={`${visible ? 'Ẩn' : 'Hiện'} ${label.toLowerCase()}`} aria-pressed={visible}>{visible ? <EyeOff size={17} /> : <Eye size={17} />}</button>}
        </div>
        {hint && <p className="auth-hint" id={`${fieldId}-hint`}>{hint}</p>}
        {error && <p className="auth-error" id={`${fieldId}-error`} role="alert">{error}</p>}
      </div>
    );
  },
);
AuthField.displayName = 'AuthField';
