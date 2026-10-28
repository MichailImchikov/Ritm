export type Subtask = { id: string; title: string; done: boolean };
export type TaskFields = { title: string; description: string; date: string | null; priority: number; category: string; completed: boolean; subtasks: Subtask[] };
export type Task = TaskFields & { id: string; createdAt: number; version: number };
export function dateKey(d = new Date()) { return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,"0")}-${String(d.getDate()).padStart(2,"0")}`; }
export function isDate(value: unknown): value is string {
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(value + "T12:00:00");
  return !Number.isNaN(date.getTime()) && dateKey(date) === value && value >= "1900-01-01" && value <= "9999-12-31";
}
export function validateTask(value: unknown): TaskFields | null {
  if (!value || typeof value !== "object") return null;
  const v = value as Record<string, unknown>;
  if (typeof v.title !== "string" || !v.title.trim() || v.title.length > 200 || typeof v.description !== "string" || v.description.length > 10000 || typeof v.category !== "string" || !v.category.trim() || v.category.length > 50 || ![1,2,3,4].includes(v.priority as number) || typeof v.completed !== "boolean" || (v.date !== null && !isDate(v.date)) || !Array.isArray(v.subtasks) || v.subtasks.length > 50) return null;
  if (v.subtasks.some(s => !s || typeof s.id !== "string" || s.id.length > 100 || typeof s.title !== "string" || !s.title.trim() || s.title.length > 200 || typeof s.done !== "boolean")) return null;
  return { title: v.title.trim(), description: v.description, date: v.date as string | null, category: v.category.trim(), priority: v.priority as number, completed: v.completed, subtasks: v.subtasks as Subtask[] };
}
