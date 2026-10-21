import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminStudentApi, type StudentAction, type StudentFilter, type StudentFormData } from '../api/adminStudentApi';

export function useStudents(filter: StudentFilter, enabled = true) {
  return useQuery({
    queryKey: ['admin-students', 'list', filter],
    queryFn: ({ signal }) => adminStudentApi.list(filter, signal),
    enabled,
  });
}

export function useStudentStats(enabled = true) {
  return useQuery({
    queryKey: ['admin-students', 'stats'],
    queryFn: adminStudentApi.stats,
    enabled,
  });
}

export function useStudentDetail(id?: string) {
  return useQuery({
    queryKey: ['admin-students', 'detail', id],
    queryFn: ({ signal }) => adminStudentApi.detail(id!, signal),
    enabled: !!id,
  });
}

export function useStudentMutations() {
  const client = useQueryClient();
  const onSuccess = () => client.invalidateQueries({ queryKey: ['admin-students'] });
  const create = useMutation({ mutationFn: adminStudentApi.create, onSuccess });
  const update = useMutation({
    mutationFn: ({ id, data }: { id: string; data: StudentFormData }) => adminStudentApi.update(id, data),
    onSuccess,
  });
  const action = useMutation({
    mutationFn: ({ id, action }: { id: string; action: StudentAction }) => adminStudentApi.action(id, action),
    onSuccess,
  });
  const bulk = useMutation({
    mutationFn: ({ ids, action }: { ids: string[]; action: 'approve' | 'disable' }) => adminStudentApi.bulk(ids, action),
    onSuccess,
  });
  const remove = useMutation({
    mutationFn: (id: string) => adminStudentApi.delete(id),
    onSuccess,
  });
  const bulkDelete = useMutation({
    mutationFn: (ids: string[]) => adminStudentApi.bulkDelete(ids),
    onSuccess,
  });
  return { create, update, action, bulk, remove, bulkDelete, busy: create.isPending || update.isPending || action.isPending || bulk.isPending || remove.isPending || bulkDelete.isPending };
}
