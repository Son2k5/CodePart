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
      <div className="auth-roles" role="group" aria-label="Vai trò tài khoản">
        <button type="button" className="auth-role" disabled={isPending} aria-pressed={role === 'student'} onClick={() => setRole('student')}><GraduationCap size={17} /> Sinh viên</button>
        <button type="button" className="auth-role" disabled={isPending} aria-pressed={role === 'teacher'} onClick={() => setRole('teacher')}><Presentation size={17} /> Giảng viên</button>
      </div>
      <form key={role} onSubmit={onSubmit} className="auth-form" noValidate aria-busy={isPending}>
        <AuthField label="Họ và tên" autoComplete="name" placeholder="Nguyễn Văn An" {...form.register('fullName')} error={errors.fullName?.message} />
        <AuthField label="Email trường" type="email" autoComplete="email" placeholder={role === 'student' ? '123456@hanu.edu.vn' : 'giangvien@hanu.edu.vn'} hint={role === 'student' ? 'Sử dụng email theo mã sinh viên, ví dụ: 123456@hanu.edu.vn.' : 'Sử dụng email giảng viên @hanu.edu.vn. Tài khoản cần được phê duyệt sau khi xác thực.'} {...form.register('email')} error={errors.email?.message} />
        <AuthField label="Mật khẩu" type="password" autoComplete="new-password" placeholder="Tạo mật khẩu của bạn" hint="Ít nhất 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt." {...form.register('password')} error={errors.password?.message} />
        <AuthField label="Xác nhận mật khẩu" type="password" autoComplete="new-password" placeholder="Nhập lại mật khẩu" {...form.register('confirmPassword')} error={errors.confirmPassword?.message} />
        <button type="submit" disabled={isPending} className="auth-submit">{isPending ? 'Đang tạo tài khoản…' : <>Tạo tài khoản <ArrowRight size={17} /></>}</button>
      </form>
      <p className="auth-switch">Đã có tài khoản? <Link to="/login" className="auth-link">Đăng nhập</Link></p>
    </BrightAuthLayout>
  );
};
