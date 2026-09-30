import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom';
import { LoginPage } from './features/auth/pages/LoginPage';
import { RegisterPage } from './features/auth/pages/RegisterPage';
import { VerifyOtpPage } from './features/auth/pages/VerifyOtpPage';
import { ForgotPasswordPage } from './features/auth/pages/ForgotPasswordPage';
import { ResetPasswordPage } from './features/auth/pages/ResetPasswordPage';
import { PendingApprovalPage } from './features/auth/pages/PendingApprovalPage';
import { GuestRoute } from './routes/GuestRoute';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { RoleGuard } from './routes/RoleGuard';
import { useAuthStore } from './features/auth/store/authStore';
import { ExercisePage } from './features/online-judge/pages/ExercisePage';

const DashboardRedirect = () => {
  const { user } = useAuthStore();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role === 'Admin') return <Navigate to="/admin" replace />;
  if (user.role === 'Teacher') return <Navigate to="/teacher" replace />;
  return <Navigate to="/courses" replace />;
};

const router = createBrowserRouter([
  {
    element: <GuestRoute />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
      { path: '/verify-otp', element: <VerifyOtpPage /> },
      { path: '/forgot-password', element: <ForgotPasswordPage /> },
      { path: '/reset-password', element: <ResetPasswordPage /> },
      { path: '/pending-approval', element: <PendingApprovalPage /> },
    ],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <RoleGuard allowedRoles={['Admin']} />,
        children: [
          { path: '/admin', element: <div className="p-8">Admin Dashboard</div> },
        ],
      },
      {
        element: <RoleGuard allowedRoles={['Teacher']} />,
        children: [
          { path: '/teacher', element: <div className="p-8">Teacher Dashboard</div> },
        ],
      },
      {
        element: <RoleGuard allowedRoles={['Student']} />,
        children: [
          { path: '/courses', element: <div className="p-8">Student Courses</div> },
          { path: '/exercise/:slug', element: <ExercisePage /> },
        ],
      },
      { path: '/', element: <DashboardRedirect /> },
    ],
  },
]);

export default function App() {
  return <RouterProvider router={router} />;
}
