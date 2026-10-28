import { sqliteTable, text, integer, index } from "drizzle-orm/sqlite-core";
export const tasks = sqliteTable("tasks", {
  id: text("id").primaryKey(),
  userId: text("user_id").notNull(),
  title: text("title").notNull(),
  description: text("description").notNull().default(""),
  date: text("date"),
  priority: integer("priority").notNull().default(2),
  category: text("category").notNull().default("Жизнь"),
  completed: integer("completed", { mode: "boolean" }).notNull().default(false),
  subtasks: text("subtasks", { mode: "json" }).$type<{ id: string; title: string; done: boolean }[]>().notNull().default([]),
  createdAt: integer("created_at").notNull(),
  version: integer("version").notNull().default(1),
}, (table) => [index("tasks_user_idx").on(table.userId)]);
