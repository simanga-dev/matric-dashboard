CREATE TABLE `matric-dashboard_school` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`natemis` integer,
	`quantile` integer,
	`centre_number` integer,
	`province` text(256),
	`official_institution_name` text(256),
	`district_name` text(256),
	`createdAt` integer DEFAULT (unixepoch()) NOT NULL,
	`updatedAt` integer
);
--> statement-breakpoint
CREATE INDEX `natemis` ON `matric-dashboard_school` (`natemis`);