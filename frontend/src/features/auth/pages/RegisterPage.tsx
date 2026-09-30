import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { registerStudentSchema, registerTeacherSchema, RegisterStudentInput, RegisterTeacherInput } from '../schemas/authSchemas';
import { useRegisterStudent, useRegisterTeacher } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { PasswordInput } from '../components/PasswordInput';

export const RegisterPage: React.FC = () => {
  const [role, setRole] = useState<'student' | 'teacher'>('student');
  
  const { register: registerStudentForm, handleSubmit: handleSubmitStudent, formState: { errors: errorsStudent } } = useForm<RegisterStudentInput>({
    resolver: zodResolver(registerStudentSchema),
  });
  
  const { register: registerTeacherForm, handleSubmit: handleSubmitTeacher, formState: { errors: errorsTeacher } } = useForm<RegisterTeacherInput>({
    resolver: zodResolver(registerTeacherSchema),
  });

  const { mutate: registerStudent, isPending: isPendingStudent } = useRegisterStudent();
  const { mutate: registerTeacher, isPending: isPendingTeacher } = useRegisterTeacher();

  const isPending = isPendingStudent || isPendingTeacher;

  const onStudentSubmit = (data: RegisterStudentInput) => {
    const { confirmPassword, ...payload } = data;
    registerStudent(payload);
  };

  const onTeacherSubmit = (data: RegisterTeacherInput) => {
    const { confirmPassword, ...payload } = data;
    registerTeacher(payload);
  };

  const registerProps = role === 'student' ? registerStudentForm : registerTeacherForm;
  const errors = role === 'student' ? errorsStudent : errorsTeacher;
  const onSubmit = role === 'student' ? handleSubmitStudent(onStudentSubmit) : handleSubmitTeacher(onTeacherSubmit);

  return (
    <AuthLayout title="Create Account" subtitle="Sign up to get started with CodePath">
      <div className="flex bg-slate-100 p-1 rounded-lg mb-6">
        <button
          className={`flex-1 py-1.5 text-xs font-semibold rounded-md transition-colors ${role === 'student' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500'}`}
          onClick={() => setRole('student')}
        >
          Student
        </button>
        <button
          className={`flex-1 py-1.5 text-xs font-semibold rounded-md transition-colors ${role === 'teacher' ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500'}`}
          onClick={() => setRole('teacher')}
        >
          Teacher
        </button>
      </div>

      <form onSubmit={onSubmit} className="space-y-4">
        <div className="space-y-1">
          <label className="block text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
            Full Name
          </label>
          <input
            {...registerProps('fullName')}
            type="text"
            placeholder="Nguyen Van A"
            className={`w-full border-b py-2 text-sm text-slate-900 placeholder-gray-300 focus:outline-none bg-transparent font-medium transition-colors ${
              errors.fullName ? 'border-red-500 focus:border-red-600' : 'border-gray-300 focus:border-slate-900'
            }`}
          />
          {errors.fullName && <p className="text-xs text-red-500 mt-1">{errors.fullName.message as string}</p>}
        </div>

        <div className="space-y-1">
          <label className="block text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
            Email
          </label>
          <input
            {...registerProps('email')}
            type="email"
            placeholder={role === 'student' ? '123456@hanu.edu.vn' : 'teacher@hanu.edu.vn'}
            className={`w-full border-b py-2 text-sm text-slate-900 placeholder-gray-300 focus:outline-none bg-transparent font-medium transition-colors ${
              errors.email ? 'border-red-500 focus:border-red-600' : 'border-gray-300 focus:border-slate-900'
            }`}
          />
          {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email.message as string}</p>}
        </div>

        <PasswordInput
          label="Password"
          placeholder="••••••••"
          {...registerProps('password')}
          error={errors.password?.message as string}
        />

        <PasswordInput
          label="Confirm Password"
          placeholder="••••••••"
          {...registerProps('confirmPassword')}
          error={errors.confirmPassword?.message as string}
        />

        <div className="pt-4">
          <button
            type="submit"
            disabled={isPending}
            className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black disabled:bg-gray-400 text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
          >
            {isPending ? (
              <div className="w-5 h-5 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
            ) : (
              <span>Sign up</span>
            )}
          </button>
        </div>
      </form>

      <div className="mt-8 text-center text-[13px] font-medium text-gray-500">
        <p>
          Already have an account?{' '}
          <Link to="/login" className="text-slate-900 font-bold hover:underline">
            Log in
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
};
