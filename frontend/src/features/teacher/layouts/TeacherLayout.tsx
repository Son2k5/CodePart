import { NavLink, Outlet } from 'react-router-dom';
import { BookOpen, GraduationCap, LogOut } from 'lucide-react';
import { useAuthStore } from '../../auth/store/authStore';
import { useLogout } from '../../auth/hooks/useAuth';
import '../styles/teacher.css';

export function TeacherSidebar() {
  const logout = useLogout();
  const user = useAuthStore(s => s.user);
  return <aside className="teacher-sidebar">
    <NavLink className="teacher-brand" to="/teacher">CodePart<span>CỔNG GIẢNG VIÊN</span></NavLink>
    <nav aria-label="Điều hướng giảng viên">
      <NavLink to="/teacher/courses"><BookOpen size={18} />Lớp học phần</NavLink>
      <NavLink to="/teacher/classes"><GraduationCap size={18} />Lớp niên khóa</NavLink>
      <NavLink to="/manage/problems"><BookOpen size={18} />Code Judge</NavLink>
    </nav>
    <footer><strong>{user?.fullName}</strong><small>{user?.email}</small>
      <button onClick={() => logout.mutate()} disabled={logout.isPending}><LogOut size={16} />Đăng xuất</button></footer>
  </aside>;
}
export function TeacherLayout() {
  return <div className="teacher-shell"><TeacherSidebar /><main className="teacher-main"><Outlet /></main></div>;
}
