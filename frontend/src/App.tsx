import { useEffect } from 'react';
import { MemberFooterLayout } from './features/home/MemberFooter';
import './features/home/member-home.css';
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom';
import { GuestRoute } from './routes/GuestRoute';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { RoleGuard } from './routes/RoleGuard';
import { hasSessionIndicator, useAuthStore } from './features/auth/store/authStore';
import { refreshSession } from './lib/axios';
import { AutoLogoutProvider } from './features/auth/components/AutoLogoutProvider';
import { RouteErrorBoundary } from './components/common/RouteErrorBoundary';
import { PageSkeleton } from './components/common/PageSkeleton';

const DashboardRedirect = () => {
  const { user } = useAuthStore();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role === 'Admin') return <Navigate to="/admin" replace />;
  if (user.role === 'Teacher') return <Navigate to="/teacher" replace />;
  return <Navigate to="/home" replace />;
};

const router = createBrowserRouter([
  {
    errorElement: <RouteErrorBoundary />,
    hydrateFallbackElement: <PageSkeleton />,
    children: [
      { path: '/design', lazy: async () => ({ Component: (await import('./features/workspace/DesignSystemPage')).DesignSystemPage }) },
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
                  { path: 'classes', lazy: async () => ({ Component: (await import('./features/classes/ClassListPage')).ClassListPage }) },
                  { path: 'classes/new', lazy: async () => ({ Component: (await import('./features/classes/ClassFormPage')).ClassFormPage }) },
                  { path: 'classes/:id', lazy: async () => ({ Component: (await import('./features/classes/ClassDetailPage')).ClassDetailPage }) },
                  { path: 'classes/:id/edit', lazy: async () => ({ Component: (await import('./features/classes/ClassFormPage')).ClassFormPage }) },
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
              { path: '/teacher', lazy: async () => ({ Component: (await import('./features/teacher/layouts/TeacherLayout')).TeacherLayout }), children: [
                { index: true, element: <Navigate to="/teacher/courses" replace /> },
                { path: 'courses', lazy: async () => ({ Component: (await import('./features/teacher/pages/TeacherCoursesPage')).TeacherCoursesPage }) },
                { path: 'courses/:id', lazy: async () => ({ Component: (await import('./features/teacher/pages/TeacherCourseDetailPage')).TeacherCourseDetailPage }) },
                { path: 'courses/:id/submissions', lazy: async () => ({ Component: (await import('./features/teacher/pages/TeacherCourseDetailPage')).TeacherCourseDetailPage }) },
                { path: 'classes', lazy: async () => ({ Component: (await import('./features/teacher/pages/TeacherClassesPage')).TeacherClassesPage }) },
              ] },
            ],
          },
          {
            element: <RoleGuard allowedRoles={['Student']} />,
            children: [
              { element: <MemberFooterLayout />, children: [
              { path: '/home', lazy: async () => ({ Component: (await import('./features/home/HomePage')).HomePage }) },
              { path: '/courses', lazy: async () => ({ Component: (await import('./features/teacher/pages/StudentCoursesPage')).StudentCoursesPage }) },
              { path: '/classes', lazy: async () => ({ Component: (await import('./features/classes/PublicClassesPage')).PublicClassesPage }) },
              ] },
              {
                path: '/exercise/:slug',
                lazy: async () => {
                  const { ExercisePage } = await import('./features/online-judge/pages/ExercisePage');
                  return { Component: ExercisePage };
                },
              },
            ],
          },
          { element: <RoleGuard allowedRoles={['Admin', 'Teacher']} />, children: [{ path: '/manage/problems', lazy: async () => ({ Component: (await import('./features/code-judge/JudgeAdminPage')).JudgeAdminPage }) }] },
          { element: <RoleGuard allowedRoles={['Student']} />, children: [
            { element: <MemberFooterLayout />, children: [{ path: '/problems', lazy: async () => ({ Component: (await import('./features/code-judge/ProblemListPage')).ProblemListPage }) }] },
            { path: '/classes/:classId/topics/:slug/problems/:problemId', lazy: async () => ({ Component: (await import('./features/workspace/WorkspacePage')).WorkspacePage }) },
            { path: '/problems/:id', lazy: async () => ({ Component: (await import('./features/workspace/WorkspacePage')).WorkspacePage }) },
          ] },
          { path: '/classes/:classId/learning', lazy: async () => ({ Component: (await import('./features/learning/LearningPage')).LearningPage }) },
          { path: '/dashboard', element: <DashboardRedirect /> },
        ],
      },
    ],
  },
]);

export default function App() {
  const isInitialized = useAuthStore((state) => state.isInitialized);

  useEffect(() => {
    if (hasSessionIndicator()) {
      void refreshSession().catch(() => undefined);
    } else {
      useAuthStore.getState().clearAuth();
    }
  }, []);

  if (!isInitialized) {
    return <PageSkeleton />;
  }

  return (
    <AutoLogoutProvider onLogout={() => { void router.navigate('/login', { replace: true }); }}>
      <RouterProvider router={router} />
    </AutoLogoutProvider>
  );
}
