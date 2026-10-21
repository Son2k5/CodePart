import React, { useRef } from 'react';
import type { KeyboardEvent } from 'react';

interface OtpInputProps {
  length?: number;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  disabled?: boolean;
}

export const OtpInput: React.FC<OtpInputProps> = ({ length = 6, value, onChange, error, disabled }) => {
  const otp = Array.from({ length }, (_, index) => value[index] ?? '');
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>, index: number) => {
    const val = e.target.value;
    if (!/^\d*$/.test(val)) return;

    const newOtp = [...otp];
    // Allow only last char if pasted multiple
    newOtp[index] = val.substring(val.length - 1);
    onChange(newOtp.join(''));

    // Move to next
    if (val && index < length - 1 && inputRefs.current[index + 1]) {
      inputRefs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>, index: number) => {
    if (e.key === 'Backspace' && !otp[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  };

  const handlePaste = (e: React.ClipboardEvent) => {
    e.preventDefault();
    const pastedData = e.clipboardData.getData('text').slice(0, length).split('');
    if (pastedData.some(char => !/^\d$/.test(char))) return;

    const newOtp = [...otp];
    pastedData.forEach((char, idx) => {
      newOtp[idx] = char;
    });
    onChange(newOtp.join(''));
    
    // Focus last filled
    const lastFilledIndex = Math.min(pastedData.length, length - 1);
    if (inputRefs.current[lastFilledIndex]) {
      inputRefs.current[lastFilledIndex]?.focus();
    }
  };

  return (
    <div className="flex flex-col items-center w-full">
      <div className="auth-otp" role="group" aria-label="Mã xác thực 6 chữ số" onPaste={handlePaste}>
        {otp.map((digit, index) => (
          <input
            key={index}
            ref={(el) => { inputRefs.current[index] = el; }}
            type="text"
            inputMode="numeric"
            aria-label={`Chữ số ${index + 1}`}
            aria-invalid={!!error}
            disabled={disabled}
            autoComplete={index === 0 ? 'one-time-code' : 'off'}
            maxLength={1}
            value={digit}
            onChange={(e) => handleChange(e, index)}
            onKeyDown={(e) => handleKeyDown(e, index)}
            className="auth-input auth-otp-digit"
          />
        ))}
      </div>
      {error && <p className="text-xs text-red-500 mt-2 text-center" role="alert" aria-live="polite">{error}</p>}
    </div>
  );
};
