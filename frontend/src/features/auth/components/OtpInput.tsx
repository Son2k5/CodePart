import React, { useState, useRef, KeyboardEvent, useEffect } from 'react';

interface OtpInputProps {
  length?: number;
  value: string;
  onChange: (value: string) => void;
  error?: string;
}

export const OtpInput: React.FC<OtpInputProps> = ({ length = 6, value, onChange, error }) => {
  const [otp, setOtp] = useState<string[]>(Array(length).fill(''));
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  useEffect(() => {
    // Sync external value
    if (value.length <= length) {
      const newOtp = Array(length).fill('');
      value.split('').forEach((char, idx) => {
        newOtp[idx] = char;
      });
      setOtp(newOtp);
    }
  }, [value, length]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>, index: number) => {
    const val = e.target.value;
    if (isNaN(Number(val))) return;

    const newOtp = [...otp];
    // Allow only last char if pasted multiple
    newOtp[index] = val.substring(val.length - 1);
    setOtp(newOtp);
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
    if (pastedData.some(char => isNaN(Number(char)))) return;

    const newOtp = [...otp];
    pastedData.forEach((char, idx) => {
      newOtp[idx] = char;
    });
    setOtp(newOtp);
    onChange(newOtp.join(''));
    
    // Focus last filled
    const lastFilledIndex = Math.min(pastedData.length, length - 1);
    if (inputRefs.current[lastFilledIndex]) {
      inputRefs.current[lastFilledIndex]?.focus();
    }
  };

  return (
    <div className="flex flex-col items-center w-full">
      <div className="flex gap-2 justify-center w-full" onPaste={handlePaste}>
        {otp.map((digit, index) => (
          <input
            key={index}
            ref={(el) => (inputRefs.current[index] = el)}
            type="text"
            inputMode="numeric"
            maxLength={1}
            value={digit}
            onChange={(e) => handleChange(e, index)}
            onKeyDown={(e) => handleKeyDown(e, index)}
            className={`w-12 h-14 sm:w-14 sm:h-16 text-center text-xl font-bold rounded-xl border-2 bg-transparent focus:outline-none transition-colors ${
              error ? 'border-red-500 focus:border-red-600' : 'border-gray-200 focus:border-slate-900'
            }`}
          />
        ))}
      </div>
      {error && <p className="text-xs text-red-500 mt-2 text-center" role="alert" aria-live="polite">{error}</p>}
    </div>
  );
};
