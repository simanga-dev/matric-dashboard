CREATE TABLE `matric-dashboard_marks` (
	`year` integer,
	`school_id` integer NOT NULL,
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`number_progressed` integer,
	`total_wrote` integer,
	`total_archieved` integer,
	`percentage_archived` integer,
	`createdAt` integer DEFAULT (unixepoch()) NOT NULL,
	`updatedAt` integer,
	FOREIGN KEY (`school_id`) REFERENCES `matric-dashboard_school`(`id`) ON UPDATE no action ON DELETE no action
);
