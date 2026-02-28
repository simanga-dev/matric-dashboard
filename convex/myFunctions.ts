import { v } from 'convex/values'
import { query, mutation, action } from './_generated/server'
import { api } from './_generated/api'

// Write your Convex functions in any file inside this directory (`convex`).
// See https://docs.convex.dev/functions for more.

export const getTopSchools = query({
  args: {},
  returns: v.union(
    v.object({
      totalSchools2025: v.number(),
      totalSchools2024: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const marks2025 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2025))
      .collect()
    const marks2024 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2024))
      .collect()

    const perfectSchools2025 = new Set(
      marks2025
        .filter((m) => m.total_wrote === m.total_achieved)
        .map((m) => m.school_id),
    )

    const perfectSchools2024 = new Set(
      marks2024
        .filter((m) => m.total_wrote === m.total_achieved)
        .map((m) => m.school_id),
    )

    const totalSchools2025 = perfectSchools2025.size
    const totalSchools2024 = perfectSchools2024.size

    const trendRate =
      totalSchools2024 === 0
        ? null
        : Math.round(
            ((totalSchools2025 - totalSchools2024) / totalSchools2024) * 100,
          ) / 100

    return {
      totalSchools2025,
      totalSchools2024,
      trendRate,
    }
  },
})

export const getExamCenters = query({
  args: {},
  returns: v.union(
    v.object({
      totalCenters2025: v.number(),
      totalCenters2024: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const marks2025 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2025))
      .collect()
    const marks2024 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2024))
      .collect()

    const centers2025 = new Set(marks2025.map((m) => m.school_id))
    const centers2024 = new Set(marks2024.map((m) => m.school_id))

    const totalCenters2025 = centers2025.size
    const totalCenters2024 = centers2024.size

    const trendRate =
      totalCenters2024 === 0
        ? null
        : Math.round(
            ((totalCenters2025 - totalCenters2024) / totalCenters2024) * 100,
          ) / 100

    return {
      totalCenters2025,
      totalCenters2024,
      trendRate,
    }
  },
})

export const getTotalLearners = query({
  args: {},
  returns: v.union(
    v.object({
      totalLearners2025: v.number(),
      totalLearners2024: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const marks2025 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2025))
      .collect()
    const marks2024 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2024))
      .collect()

    const totalLearners2025 = marks2025.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )

    const totalLearners2024 = marks2024.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )

    const trendRate =
      totalLearners2024 === 0
        ? null
        : Math.round(
            ((totalLearners2025 - totalLearners2024) / totalLearners2024) * 100,
          ) / 100

    return {
      totalLearners2025,
      totalLearners2024,
      trendRate,
    }
  },
})

export const getMatricPassRate = query({
  args: {},
  returns: v.union(
    v.object({
      passRate2025: v.union(v.number(), v.null()),
      passRate2024: v.union(v.number(), v.null()),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const marks2025 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2025))
      .collect()
    const marks2024 = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', 2024))
      .collect()

    const totalWrote2025 = marks2025.reduce((sum, m) => sum + m.total_wrote, 0)
    const totalPassed2025 = marks2025.reduce(
      (sum, m) => sum + (m.total_achieved ?? 0),
      0,
    )

    const totalWrote2024 = marks2024.reduce((sum, m) => sum + m.total_wrote, 0)
    const totalPassed2024 = marks2024.reduce(
      (sum, m) => sum + (m.total_achieved ?? 0),
      0,
    )

    const passRate2025 =
      totalWrote2025 === 0
        ? null
        : Math.round((totalPassed2025 / totalWrote2025) * 10000) / 100

    const passRate2024 =
      totalWrote2024 === 0
        ? null
        : Math.round((totalPassed2024 / totalWrote2024) * 10000) / 100

    const trendRate =
      passRate2025 === null || passRate2024 === null
        ? null
        : Math.round((passRate2025 - passRate2024) * 100) / 100

    return {
      passRate2025,
      passRate2024,
      trendRate,
    }
  },
})

export const getSchoolPerformance = query({
  args: {
    year: v.optional(v.number()),
  },
  returns: v.array(
    v.object({
      _id: v.id('school'),
      natemis: v.number(),
      schoolName: v.string(),
      province: v.string(),
      district: v.string(),
      quintile: v.union(v.float64(), v.null()),
      passRateCurrent: v.union(v.number(), v.null()),
      passRatePrevious: v.union(v.number(), v.null()),
      currentYear: v.number(),
      previousYear: v.number(),
      totalWrote: v.number(),
      totalAchieved: v.number(),
      trend: v.union(v.number(), v.null()),
      status: v.string(),
    }),
  ),
  handler: async (ctx, args) => {
    const currentYear = args.year ?? 2025
    const previousYear = currentYear - 1

    const schools = await ctx.db.query('school').collect()
    const currentMarks = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', currentYear))
      .collect()
    const previousMarks = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', previousYear))
      .collect()

    type MarkDoc = (typeof currentMarks)[0]
    const marksBySchool = new Map<
      string,
      { current?: MarkDoc; previous?: MarkDoc }
    >()

    for (const mark of currentMarks) {
      const existing = marksBySchool.get(mark.school_id) || {}
      existing.current = mark
      marksBySchool.set(mark.school_id, existing)
    }
    for (const mark of previousMarks) {
      const existing = marksBySchool.get(mark.school_id) || {}
      existing.previous = mark
      marksBySchool.set(mark.school_id, existing)
    }

    const results = schools
      .map((school) => {
        const marks = marksBySchool.get(school._id)
        const currentMark = marks?.current
        const previousMark = marks?.previous

        const passRateCurrent = currentMark?.percentage_achieved ?? null
        const passRatePrevious = previousMark?.percentage_achieved ?? null

        const trend =
          passRateCurrent !== null && passRatePrevious !== null
            ? Math.round((passRateCurrent - passRatePrevious) * 100) / 100
            : null

        let status: string
        if (passRateCurrent === null) {
          status = 'No Data'
        } else if (passRateCurrent >= 90) {
          status = 'Excellent'
        } else if (passRateCurrent >= 70) {
          status = 'Good'
        } else if (passRateCurrent >= 50) {
          status = 'Average'
        } else {
          status = 'Needs Improvement'
        }

        return {
          _id: school._id,
          natemis: school.natemis,
          schoolName: school.official_institution_name,
          province: school.province,
          district: school.district_name,
          quintile: school.quantile ?? null,
          passRateCurrent,
          passRatePrevious,
          currentYear,
          previousYear,
          totalWrote: currentMark?.total_wrote ?? 0,
          totalAchieved: currentMark?.total_achieved ?? 0,
          trend,
          status,
        }
      })
      .filter((s) => s.totalWrote > 0)

    return results
  },
})

// Debug query to check data
export const debugData = query({
  args: {},
  returns: v.object({
    schoolCount: v.number(),
    markCount: v.number(),
    years: v.array(v.number()),
  }),
  handler: async (ctx) => {
    const schools = await ctx.db.query('school').collect()
    const marks = await ctx.db.query('marks').collect()
    const years = [...new Set(marks.map((m) => m.year))]
    return { schoolCount: schools.length, markCount: marks.length, years }
  },
})

// You can read data from the database via a query:
export const listNumbers = query({
  // Validators for arguments.
  args: {
    count: v.number(),
  },

  // Query implementation.
  handler: async (ctx, args) => {
    //// Read the database as many times as you need here.
    //// See https://docs.convex.dev/database/reading-data.
    const numbers = await ctx.db
      .query('numbers')
      // Ordered by _creationTime, return most recent
      .order('desc')
      .take(args.count)
    return {
      viewer: (await ctx.auth.getUserIdentity())?.name ?? null,
      numbers: numbers.reverse().map((number) => number.value),
    }
  },
})

// You can write data to the database via a mutation:
export const addNumber = mutation({
  // Validators for arguments.
  args: {
    value: v.number(),
  },

  // Mutation implementation.
  handler: async (ctx, args) => {
    //// Insert or modify documents in the database here.
    //// Mutations can also read from the database like queries.
    //// See https://docs.convex.dev/database/writing-data.

    const id = await ctx.db.insert('numbers', { value: args.value })

    console.log('Added new document with id:', id)
    // Optionally, return a value from your mutation.
    // return id;
  },
})

// You can fetch data from and send data to third-party APIs via an action:
export const myAction = action({
  // Validators for arguments.
  args: {
    first: v.number(),
  },

  // Action implementation.
  handler: async (ctx, args) => {
    //// Use the browser-like `fetch` API to send HTTP requests.
    //// See https://docs.convex.dev/functions/actions#calling-third-party-apis-and-using-npm-packages.
    // const response = await ctx.fetch("https://api.thirdpartyservice.com");
    // const data = await response.json();

    //// Query data by running Convex queries.
    const data = await ctx.runQuery(api.myFunctions.listNumbers, {
      count: 10,
    })
    console.log(data)

    //// Write data by running Convex mutations.
    await ctx.runMutation(api.myFunctions.addNumber, {
      value: args.first,
    })
  },
})
