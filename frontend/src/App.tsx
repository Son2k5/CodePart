import { useEffect } from 'react';
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom';
import { GuestRoute } from './routes/GuestRoute';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { RoleGuard } from './routes/RoleGuard';
import { useAuthStore } from './features/auth/store/authStore';
import { refreshSession } from './lib/axios';

const DashboardRedirect = () => {
  const { user } = useAuthStore();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role === 'Admin') return <Navigate to="/admin" replace />;
  if (user.role === 'Teacher') return <Navigate to="/teacher" replace />;
  return <Navigate to="/courses" replace />;
};

const router = createBrowserRouter([
  {
    path: '/',
    lazy: async () => {
      const { LandingPage } = await import('./pages/LandingPage');
      return { Component: LandingPage };
    },
  },
  {
    element: <GuestRoute />,
    children: [
      {
        path: '/login',
        lazy: async () => ({ Component: (await import('./features/auth/pages/LoginPage')).LoginPage }),
      },
      {
        path: '/register',
        lazy: async () => ({ Component: (await import('./features/auth/pages/RegisterPage')).RegisterPage }),
      },
      {
        path: '/verify-otp',
        lazy: async () => ({ Component: (await import('./features/auth/pages/VerifyOtpPage')).VerifyOtpPage }),
      },
      {
        path: '/forgot-password',
        lazy: async () => ({ Component: (await import('./features/auth/pages/ForgotPasswordPage')).ForgotPasswordPage }),
      },
      {
        path: '/reset-password',
        lazy: async () => ({ Component: (await import('./features/auth/pages/ResetPasswordPage')).ResetPasswordPage }),
      },
      {
        path: '/pending-approval',
        lazy: async () => ({ Component: (await import('./features/auth/pages/PendingApprovalPage')).PendingApprovalPage }),
      },
    ],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <RoleGuard allowedRoles={['Admin']} />,
        children: [
          {
            path: '/admin',
            lazy: async () => ({ Component: (await import('./features/admin/layouts/AdminLayout')).AdminLayout }),
            children: [
              { index: true, element: <Navigate to="/admin/dashboard" replace /> },
              {
                path: 'dashboard',
                lazy: async () => ({ Component: (await import('./features/admin/pages/AdminDashboardPage')).AdminDashboardPage }),
              },
              {
                path: 'users',
                element: <Navigate to="/admin/students" replace />,
              },
              {
                path: 'students',
                lazy: async () => ({ Component: (await import('./features/admin/pages/StudentManagementPage')).StudentManagementPage }),
              },
              {
                path: 'teachers',
                lazy: async () => ({ Component: (await import('./features/admin/pages/TeacherManagementPage')).TeacherManagementPage }),
              },
            ],
          },
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
          {
            path: '/exercise/:slug',
            lazy: async () => {
              const { ExercisePage } = await import('./features/online-judge/pages/ExercisePage');
              return { Component: ExercisePage };
            },
          },
        ],
      },
      { path: '/dashboard', element: <DashboardRedirect /> },
    ],
  },
]);

export default function App() {
  const isInitialized = useAuthStore((state) => state.isInitialized);

  useEffect(() => {
    void refreshSession().catch(() => undefined);
  }, []);

  if (!isInitialized) {
    return <div className="min-h-screen bg-white" aria-label="Loading session" />;
  }

  return <RouterProvider router={router} />;
}
