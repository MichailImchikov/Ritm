import { getChatGPTUser } from "../../chatgpt-auth";
import { getDb } from "../../../db";
import { tasks } from "../../../db/schema";
import { and, eq } from "drizzle-orm";
import { validateTask } from "../../task-model";
export const dynamic = "force-dynamic";
const json = (data: unknown, status = 200) => Response.json(data, { status, headers: { "Cache-Control": "no-store" } });
export async function GET() {
  const user = await getChatGPTUser();
  if (!user) return json({ error: "Войдите в аккаунт, чтобы открыть задачи." }, 401);
  try { const rows = await getDb().select().from(tasks).where(eq(tasks.userId, user.userId)); return json({ tasks: rows.map(({ userId, ...task }) => task) }); }
  catch { return json({ error: "Не удалось загрузить задачи. Попробуйте ещё раз." }, 503); }
}
export async function POST(request: Request) {
  const user = await getChatGPTUser();
  if (!user) return json({ error: "Требуется вход в аккаунт." }, 401);
  if (request.headers.get("origin") !== new URL(request.url).origin) return json({ error: "Недопустимый источник запроса." }, 403);
  try {
    const task = validateTask(await request.json());
    if (!task) return json({ error: "Проверьте поля задачи." }, 400);
    const row = { ...task, id: crypto.randomUUID(), userId: user.userId, createdAt: Date.now(), version: 1 };
    await getDb().insert(tasks).values(row);
    const { userId, ...result } = row;
    return json({ task: result }, 201);
  } catch { return json({ error: "Не удалось сохранить задачу. Изменения остались в форме." }, 503); }
}
export async function PATCH(request: Request) {
  const user = await getChatGPTUser();
  if (!user) return json({ error: "Требуется вход в аккаунт." }, 401);
  if (request.headers.get("origin") !== new URL(request.url).origin) return json({ error: "Недопустимый источник запроса." }, 403);
  try {
    const body = await request.json() as Record<string, unknown>; const task = validateTask(body);
    if (!task || typeof body.id !== "string" || typeof body.version !== "number" || !Number.isInteger(body.version)) return json({ error: "Проверьте поля задачи." }, 400);
    const rows = await getDb().update(tasks).set({ ...task, version: body.version + 1 }).where(and(eq(tasks.id, body.id), eq(tasks.userId, user.userId), eq(tasks.version, body.version))).returning();
    if (!rows.length) return json({ error: "Задача изменилась на другом устройстве. Обновите список и откройте её заново." }, 409);
    const { userId, ...result } = rows[0]; return json({ task: result });
  } catch { return json({ error: "Не удалось сохранить изменения. Попробуйте ещё раз." }, 503); }
}
export async function DELETE(request: Request) {
  const user = await getChatGPTUser();
  if (!user) return json({ error: "Требуется вход в аккаунт." }, 401);
  if (request.headers.get("origin") !== new URL(request.url).origin) return json({ error: "Недопустимый источник запроса." }, 403);
  try {
    const body = await request.json() as Record<string, unknown>;
    if (!body || typeof body.id !== "string" || typeof body.version !== "number" || !Number.isInteger(body.version)) return json({ error: "Некорректная задача." }, 400);
    const rows = await getDb().delete(tasks).where(and(eq(tasks.id, body.id), eq(tasks.userId, user.userId), eq(tasks.version, body.version))).returning({ id: tasks.id });
    if (!rows.length) return json({ error: "Задача уже изменилась. Обновите список." }, 409);
    return json({ ok: true });
  } catch { return json({ error: "Не удалось удалить задачу." }, 503); }
}
