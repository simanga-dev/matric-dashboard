// Example model schema from the Drizzle docs
// https://orm.drizzle.team/docs/sql-schema-declaration

import { sql, relations } from "drizzle-orm";
import { index, sqliteTableCreator } from "drizzle-orm/sqlite-core";

/**
 * This is an example of how to use the multi-project schema feature of Drizzle ORM. Use the same
 * database instance for multiple projects.
 *
 * @see https://orm.drizzle.team/docs/goodies#multi-project-schema
 */
export const createTable = sqliteTableCreator(
  (name) => `matric-dashboard_${name}`,
);

export const school = createTable(
  "school",
  (d) => ({
    id: d.integer({ mode: "number" }).primaryKey({ autoIncrement: true }),
    natemis: d.integer({ mode: "number" }),
    province: d.text({ length: 256 }),
    official_institution_name: d.text({ length: 256 }),
    district_name: d.text({ length: 256 }),
    createdAt: d
      .integer({ mode: "timestamp" })
      .default(sql`(unixepoch())`)
      .notNull(),
    updatedAt: d.integer({ mode: "timestamp" }).$onUpdate(() => new Date()),
  }),
  (t) => [index("natemis").on(t.natemis)],
);

export const marks = createTable("marks", (d) => ({
  year: d.integer({ mode: "number" }),
  school_id: d
    .integer({ mode: "number" })
    .notNull()
    .references(() => school.id),
  id: d.integer({ mode: "number" }).primaryKey({ autoIncrement: true }),
  dinaledi: d.text({ length: 2 }),
  quantile: d.integer({ mode: "number" }),
  centre_number: d.integer({ mode: "number" }),
  learners_wrote: d.integer({ mode: "number" }),
  progressed_number: d.integer({ mode: "number" }),
  learners_pass: d.integer({ mode: "number" }),
  createdAt: d
    .integer({ mode: "timestamp" })
    .default(sql`(unixepoch())`)
    .notNull(),
  updatedAt: d.integer({ mode: "timestamp" }).$onUpdate(() => new Date()),
}));

export const schoolRelations = relations(school, ({ many }) => ({
  marks: many(marks),
}));

export const marksRelations = relations(marks, ({ one }) => ({
  school: one(school, {
    fields: [marks.school_id],
    references: [school.id],
  }),
}));
