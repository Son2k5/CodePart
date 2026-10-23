import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { ArrowRight, GraduationCap, Presentation } from 'lucide-react';
import { registerStudentSchema, registerTeacherSchema } from '../schemas/authSchemas';
import type { RegisterStudentInput, RegisterTeacherInput } from '../schemas/authSchemas';
import { useRegisterStudent, useRegisterTeacher } from '../hooks/useAuth';
import { BrightAuthLayout } from '../components/BrightAuthLayout';
import { AuthField } from '../components/AuthField';

export const RegisterPage = () => {
  const [role, setRole] = useState<'student' | 'teacher'>('student');
  const studentForm = useForm<RegisterStudentInput>({ resolver: zodResolver(registerStudentSchema) });
  const teacherForm = useForm<RegisterTeacherInput>({ resolver: zodResolver(registerTeacherSchema) });
  const { mutate: registerStudent, isPending: isPendingStudent } = useRegisterStudent();
  const { mutate: registerTeacher, isPending: isPendingTeacher } = useRegisterTeacher();
  const isPending = isPendingStudent || isPendingTeacher;
  const form = role === 'student' ? studentForm : teacherForm;
  const { errors } = form.formState;
  const onSubmit = form.handleSubmit(({ fullName, email, password }) => {
    (role === 'student' ? registerStudent : registerTeacher)({ fullName, email, password });
  });

  return (
    <BrightAuthLayout variant="register" title="Tạo tài khoản mới" subtitle="Chọn vai trò của bạn và bắt đầu cùng CodePart.">
      <div className="flex gap-1.5 bg-[#f2f8f9] p-[5px] rounded-[13px] mb-[22px]" role="group" aria-label="Vai trò tài khoản">
        <button type="button" className={`flex justify-center items-center gap-2 flex-1 px-2 py-2.5 text-xs rounded-[9px] border transition-all cursor-pointer disabled:opacity-65 disabled:cursor-wait ${role === 'student' ? 'bg-white border-[#dce9e9] text-[#397581] shadow-[0_2px_5px_#446b7608] font-semibold' : 'border-transparent text-[#647e83]'}`} disabled={isPending} aria-pressed={role === 'student'} onClick={() => setRole('student')}><GraduationCap size={17} /> Sinh viên</button>
        <button type="button" className={`flex justify-center items-center gap-2 flex-1 px-2 py-2.5 text-xs rounded-[9px] border transition-all cursor-pointer disabled:opacity-65 disabled:cursor-wait ${role === 'teacher' ? 'bg-white border-[#dce9e9] text-[#397581] shadow-[0_2px_5px_#446b7608] font-semibold' : 'border-transparent text-[#647e83]'}`} disabled={isPending} aria-pressed={role === 'teacher'} onClick={() => setRole('teacher')}><Presentation size={17} /> Giảng viên</button>
      </div>
      <form key={role} onSubmit={onSubmit} className="grid gap-[15px]" noValidate aria-busy={isPending}>
        <AuthField label="Họ và tên" autoComplete="name" placeholder="Nguyễn Văn An" {...form.register('fullName')} error={errors.fullName?.message} />
        <AuthField label="Email trường" type="email" autoComplete="email" placeholder={role === 'student' ? '123456@hanu.edu.vn' : 'giangvien@hanu.edu.vn'} hint={role === 'student' ? 'Sử dụng email theo mã sinh viên, ví dụ: 123456@hanu.edu.vn.' : 'Sử dụng email giảng viên @hanu.edu.vn. Tài khoản cần được phê duyệt sau khi xác thực.'} {...form.register('email')} error={errors.email?.message} />
        <AuthField label="Mật khẩu" type="password" autoComplete="new-password" placeholder="Tạo mật khẩu của bạn" hint="Ít nhất 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt." {...form.register('password')} error={errors.password?.message} />
        <AuthField label="Xác nhận mật khẩu" type="password" autoComplete="new-password" placeholder="Nhập lại mật khẩu" {...form.register('confirmPassword')} error={errors.confirmPassword?.message} />
        <button type="submit" disabled={isPending} className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:enabled:bg-[#8ddd63] hover:enabled:-translate-y-0.5 transition-all disabled:opacity-65 disabled:cursor-wait">{isPending ? 'Đang tạo tài khoản…' : <>Tạo tài khoản <ArrowRight size={17} /></>}</button>
      </form>
      <p className="mt-[26px] pt-[23px] border-t border-[#edf2f2] text-center text-xs leading-[1.8] text-[#6b7f83]">Đã có tài khoản? <Link to="/login" className="text-[#397581] font-semibold hover:underline hover:underline-offset-4">Đăng nhập</Link></p>
    </BrightAuthLayout>
  );
};
