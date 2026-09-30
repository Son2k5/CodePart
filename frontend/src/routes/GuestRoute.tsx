import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../features/auth/store/authStore';

export const GuestRoute: React.FC = () => {
  const { user, accessToken } = useAuthStore();
  
  if (user && accessToken) {
    if (user.role === 'Admin') return <Navigate to="/admin" replace />;
    if (user.role === 'Teacher') return <Navigate to="/teacher" replace />;
    return <Navigate to="/courses" replace />;
  }

  return <Outlet />;
};
