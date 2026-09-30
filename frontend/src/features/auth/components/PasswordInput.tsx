import React, { useState, forwardRef } from 'react';
import { Eye, EyeOff } from 'lucide-react';

interface PasswordInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: string;
  label: string;
}

export const PasswordInput = forwardRef<HTMLInputElement, PasswordInputProps>(
  ({ error, label, className = '', ...props }, ref) => {
    const [show, setShow] = useState(false);

    return (
      <div className="space-y-1 relative w-full">
        <label className="block text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
          {label}
        </label>
        <div className={`relative flex items-center border-b ${error ? 'border-red-500' : 'border-gray-300 focus-within:border-slate-900'} transition-colors`}>
          <input
            {...props}
            ref={ref}
            type={show ? 'text' : 'password'}
            className={`w-full py-2.5 text-sm text-slate-900 placeholder-gray-300 focus:outline-none bg-transparent font-medium pr-10 ${className}`}
            aria-invalid={!!error}
          />
          <button
            type="button"
            onClick={() => setShow(!show)}
            className="absolute right-0 text-gray-400 hover:text-gray-600 transition-colors focus:outline-none"
            tabIndex={-1}
          >
            {show ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
          </button>
        </div>
        {error && (
          <p className="text-xs text-red-500 mt-1" role="alert" aria-live="polite">
            {error}
          </p>
        )}
      </div>
    );
  }
);
PasswordInput.displayName = 'PasswordInput';
