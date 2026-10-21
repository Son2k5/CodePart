import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminTeacherApi, type TeacherAction, type TeacherFilter, type TeacherFormData } from '../api/adminTeacherApi';

export function useTeachers(filter: TeacherFilter, enabled = true) {
  return useQuery({ queryKey: ['admin-teachers', 'list', filter],
    queryFn: ({ signal }) => adminTeacherApi.list(filter, signal), enabled });
}
export function useDebouncedSearch(value: string) {
  const [search, setSearch] = useState(value);
  useEffect(() => { const timer = setTimeout(() => setSearch(value), 300); return () => clearTimeout(timer); }, [value]);
  return search;
}
export function useTeacherStats(enabled = true) {
  return useQuery({ queryKey: ['admin-teachers', 'stats'], queryFn: adminTeacherApi.stats, enabled });
}
export function useFaculties(enabled = true) {
  return useQuery({ queryKey: ['admin-teachers', 'faculties'], queryFn: adminTeacherApi.faculties, enabled });
}
export function useTeacherDetail(id?: string) {
  return useQuery({ queryKey: ['admin-teachers', 'detail', id],
    queryFn: ({ signal }) => adminTeacherApi.detail(id!, signal), enabled: !!id });
}
export function useTeacherMutations() {
  const client = useQueryClient();
  const onSuccess = () => client.invalidateQueries({ queryKey: ['admin-teachers'] });
  const create = useMutation({ mutationFn: adminTeacherApi.create, onSuccess });
  const update = useMutation({ mutationFn: ({ id, data }: { id: string; data: TeacherFormData }) => adminTeacherApi.update(id, data), onSuccess });
  const action = useMutation({ mutationFn: ({ id, action }: { id: string; action: TeacherAction }) => adminTeacherApi.action(id, action), onSuccess });
  const bulk = useMutation({ mutationFn: ({ ids, action }: { ids: string[]; action: 'approve' | 'disable' }) => adminTeacherApi.bulk(ids, action), onSuccess });
  const remove = useMutation({ mutationFn: (id: string) => adminTeacherApi.delete(id), onSuccess });
  const bulkDelete = useMutation({ mutationFn: (ids: string[]) => adminTeacherApi.bulkDelete(ids), onSuccess });
  return { create, update, action, bulk, remove, bulkDelete, busy: create.isPending || update.isPending || action.isPending || bulk.isPending || remove.isPending || bulkDelete.isPending };
}
