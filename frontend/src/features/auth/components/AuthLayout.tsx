import React from 'react';
import { Code2 } from 'lucide-react';

interface AuthLayoutProps {
  children: React.ReactNode;
  title: string;
  subtitle?: string;
  backButton?: React.ReactNode;
}

export const AuthLayout: React.FC<AuthLayoutProps> = ({ children, title, subtitle, backButton }) => {
  return (
    <div className="min-h-screen bg-[#e8e8ed] flex items-center justify-center p-4 sm:p-8 font-sans dark:bg-slate-900">
      <div className="bg-white dark:bg-slate-800 rounded-[2rem] p-3 flex flex-col lg:flex-row w-full max-w-5xl shadow-[0_8px_30px_rgb(0,0,0,0.04)] min-h-[640px]">
        
        {/* Left Column - Visual/Brand */}
        <div className="hidden lg:block lg:w-1/2 relative rounded-[1.5rem] overflow-hidden bg-slate-100 dark:bg-slate-700">
          <img 
            src="https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?q=80&w=2564&auto=format&fit=crop" 
            alt="Abstract 3D Background" 
            className="absolute inset-0 w-full h-full object-cover"
          />
        </div>

        {/* Right Column - Form */}
        <div className="w-full lg:w-1/2 flex flex-col items-center justify-center p-8 lg:p-16 relative">
          
          {backButton && (
            <div className="absolute top-8 left-8">
              {backButton}
            </div>
          )}

          <div className="w-full max-w-sm">
            {/* Logo */}
            <div className="flex items-center justify-center gap-2 mb-10">
              <div className="w-6 h-6 bg-[#1a1a1a] dark:bg-white rounded flex items-center justify-center text-white dark:text-slate-900">
                <Code2 className="w-4 h-4" />
              </div>
              <span className="text-lg font-extrabold text-slate-900 dark:text-white tracking-tight">CodePath</span>
            </div>

            {/* Header */}
            <div className="text-center mb-10">
              <h2 className="text-3xl font-bold text-slate-900 dark:text-white tracking-tight mb-2">{title}</h2>
              {subtitle && <p className="text-sm text-gray-500 dark:text-gray-400 font-medium">{subtitle}</p>}
            </div>

            {children}
          </div>
        </div>
      </div>
    </div>
  );
};
