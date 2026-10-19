import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { useClassesQuery, useClassMutation, type ClassItem, type Teacher } from './api';
import { QueryState, TeacherPicker } from './ClassUi';
import './classes.css';

const schema = z.object({ name: z.string().trim().min(3, 'Tên cần ít nhất 3 ký tự').max(100), description: z.string().max(5000),
  thumbnail: z.string().refine(x => !x || /^https?:\/\//.test(x) && URL.canParse(x), 'URL ảnh không hợp lệ'),
  status: z.enum(['Draft', 'Active', 'Archived']), accessType: z.enum(['Public', 'CodeRequired']),
  maxStudents: z.string().refine(x => !x || /^\d+$/.test(x) && Number(x) > 0 && Number(x) <= 2147483647, 'Giới hạn phải là số nguyên dương'),
  startDate: z.string(), endDate: z.string(), joinCodeExpiresAt: z.string(),
}).refine(x => !x.startDate || !x.endDate || new Date(x.endDate) > new Date(x.startDate), { path: ['endDate'], message: 'Ngày kết thúc phải sau ngày bắt đầu' })
  .refine(x => x.accessType !== 'CodeRequired' || !x.joinCodeExpiresAt || new Date(x.joinCodeExpiresAt) > new Date(), { path: ['joinCodeExpiresAt'], message: 'Hạn dùng mã phải ở tương lai' });
type Values = z.infer<typeof schema>;
const localDate = (value?: string | null) => { if (!value) return ''; const d = new Date(value.endsWith('Z') ? value : value + 'Z'); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16); };
const utcDate = (value: string) => value ? new Date(value).toISOString() : null;

function ClassForm({ initial }: { initial?: ClassItem }) {
  const navigate = useNavigate(); const mutation = useClassMutation(); const [teachers, setTeachers] = useState<Teacher[]>(initial?.teachers ?? []);
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: {
    name: initial?.name ?? '', description: initial?.description ?? '', thumbnail: initial?.thumbnail ?? '',
    status: initial?.status ?? 'Draft', accessType: initial?.accessType ?? 'Public', maxStudents: initial?.maxStudents?.toString() ?? '',
    startDate: localDate(initial?.startDate), endDate: localDate(initial?.endDate), joinCodeExpiresAt: localDate(initial?.joinCodeExpiresAt),
  } });
  const accessType = useWatch({ control: form.control, name: 'accessType' });
  const submit = form.handleSubmit(async values => {
    try {
      const result = await mutation.mutateAsync({ path: '/admin/classes' + (initial ? `/${initial.id}` : ''), method: initial ? 'put' : 'post', data: {
        ...values, thumbnail: values.thumbnail || null, maxStudents: values.maxStudents ? Number(values.maxStudents) : null,
        startDate: utcDate(values.startDate), endDate: utcDate(values.endDate),
        joinCodeExpiresAt: values.accessType === 'CodeRequired' ? utcDate(values.joinCodeExpiresAt) : null,
        teacherIds: teachers.map(t => t.id), rowVersion: initial?.rowVersion ?? null,
      } });
      navigate(`/admin/classes/${initial?.id ?? result.id}`);
    } catch { /* Mutation displays the server error, including stale RowVersion. Keep the user's edits. */ }
  });
  return <form onSubmit={submit}><fieldset disabled={mutation.isPending}><div className="class-grid">
    <label>Tên lớp<input {...form.register('name')} /></label>
    <label>Trạng thái<select {...form.register('status')}><option>Draft</option><option>Active</option><option>Archived</option></select></label>
    <label>Quyền tham gia<select {...form.register('accessType')}><option value="Public">Public — miễn phí</option><option value="CodeRequired">Cần mã lớp</option></select></label>
    <label>Giới hạn học sinh (để trống: không giới hạn)<input inputMode="numeric" {...form.register('maxStudents')} /></label>
    <label>Ngày bắt đầu<input type="datetime-local" {...form.register('startDate')} /></label>
    <label>Ngày kết thúc<input type="datetime-local" {...form.register('endDate')} /></label>
    <label>URL ảnh đại diện<input {...form.register('thumbnail')} /></label>
    {accessType === 'CodeRequired' && <label>Hạn dùng mã (để trống: không hết hạn)<input type="datetime-local" {...form.register('joinCodeExpiresAt')} />
      {initial?.joinCode && <button type="button" onClick={() => { void navigator.clipboard.writeText(initial.joinCode!).then(() => toast.success('Đã sao chép mã'), () => toast.error('Không thể sao chép mã')); }}>Sao chép {initial.joinCode}</button>}
    </label>}
  </div><label>Mô tả<textarea rows={4} {...form.register('description')} /></label>
    <TeacherPicker value={teachers} onChange={setTeachers} />
    {Object.entries(form.formState.errors).map(([field, error]) => <p className="class-error" role="alert" key={field}>{error.message}</p>)}
    <div className="class-toolbar"><button className="primary" type="submit">{mutation.isPending ? 'Đang lưu…' : 'Lưu lớp'}</button><Link to="/admin/classes">Hủy</Link></div>
  </fieldset></form>;
}
export function ClassFormPage() {
  const { id } = useParams(); const query = useClassesQuery<ClassItem>(`/admin/classes/${id}`, undefined, !!id);
  return <main className="classes-page"><Link to="/admin/classes">← Danh sách lớp</Link><h1>{id ? 'Sửa lớp học' : 'Tạo lớp học'}</h1>
    {id ? <><QueryState loading={query.isLoading} error={query.error} retry={() => query.refetch()} />{query.data && <ClassForm key={id} initial={query.data} />}</> : <ClassForm />}
  </main>;
}
