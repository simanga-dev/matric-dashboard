import { z } from "zod";

export const SchoolCardSchema = z.object({
  total_school_2023: z.number(),
  total_school_2022: z.number(),
  trend_schools_2023: z.number(),
  trend_percentage_2023: z.number().nullable(),
});

export const SchoolCountSchema = z.object({
  total_school_2023: z.number(),
  total_school_2022: z.number(),
  trend_rate_schools_2023: z.number(),
});

export const PassRateSchema = z.object({
  year: z.number(),
  total_learners_wrote: z.number(),
  pass_rate_percent: z.number(),
});

export const LearnerCountSchema = z.object({
  total_learners_2023: z.number(),
  total_learners_2022: z.number(),
  trend_rate_learners_2023: z.number(),
});

export const LearnerPassRateSchema = z.object({
  pass_rate_2023: z.number(),
  pass_rate_2022: z.number(),
  trend_rate_2023: z.number(),
});

export const SchoolSchema = z.object({
  id: z.number(),

  quantile: z
    .union([z.number(), z.string().transform((val) => Number(val))])
    .transform((val) => Number(val)),

  centre_number: z
    .union([z.number(), z.string().transform((val) => Number(val))])
    .transform((val) => Number(val)),

  natemis: z
    .union([z.number(), z.string().transform((val) => Number(val))])
    .transform((val) => Number(val)),

  province: z.string(),
  official_institution_name: z.string(),
  district_name: z.string(),
  createdAt: z.date(),
  updatedAt: z.date(),
});
