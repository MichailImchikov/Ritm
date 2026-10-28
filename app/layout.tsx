import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "Ритм — задачи и планы", description: "Личное пространство для задач, планов и спокойных дней.", icons: { icon: "/favicon.svg" } };
export default function RootLayout({ children }: { children: React.ReactNode }) { return <html lang="ru"><body>{children}</body></html>; }
