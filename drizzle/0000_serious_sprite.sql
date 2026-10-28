CREATE TABLE `tasks` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`title` text NOT NULL,
	`description` text DEFAULT '' NOT NULL,
	`date` text,
	`priority` integer DEFAULT 2 NOT NULL,
	`category` text DEFAULT 'Жизнь' NOT NULL,
	`completed` integer DEFAULT false NOT NULL,
	`subtasks` text DEFAULT '[]' NOT NULL,
	`created_at` integer NOT NULL,
	`version` integer DEFAULT 1 NOT NULL
);
--> statement-breakpoint
CREATE INDEX `tasks_user_idx` ON `tasks` (`user_id`);