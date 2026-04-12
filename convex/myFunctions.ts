import { v } from 'convex/values'
import { query, mutation, action } from './_generated/server'
import { api } from './_generated/api'

// Write your Convex functions in any file inside this directory (`convex`).
// See https://docs.convex.dev/functions for more.

export const getTopSchools = query({
  args: {},
  returns: v.union(
    v.object({
      totalSchoolsCurrent: v.number(),
      totalSchoolsPrevious: v.number(),
      trendRate: v.union(v.number(), v.null()),
      latestYear: v.number(),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const latestMark = await ctx.db
      .query('marks')
      .withIndex('by_year')
      .order('desc')
      .first()
    if (!latestMark) return null
    const latestYear = latestMark.year
    const previousYear = latestYear - 1

    const marksCurrent = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', latestYear))
      .collect()
    const marksPrevious = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', previousYear))
      .collect()

    const perfectSchoolsCurrent = new Set(
      marksCurrent
        .filter((m) => m.total_wrote === m.total_achieved)
        .map((m) => m.school_id),
    )

    const perfectSchoolsPrevious = new Set(
      marksPrevious
        .filter((m) => m.total_wrote === m.total_achieved)
        .map((m) => m.school_id),
    )

    const totalSchoolsCurrent = perfectSchoolsCurrent.size
    const totalSchoolsPrevious = perfectSchoolsPrevious.size

    const trendRate =
      totalSchoolsPrevious === 0
        ? null
        : Math.round(
            ((totalSchoolsCurrent - totalSchoolsPrevious) /
              totalSchoolsPrevious) *
              100,
          ) / 100

    return {
      totalSchoolsCurrent,
      totalSchoolsPrevious,
      trendRate,
      latestYear,
    }
  },
})

export const getExamCenters = query({
  args: {},
  returns: v.union(
    v.object({
      totalCentersCurrent: v.number(),
      totalCentersPrevious: v.number(),
      trendRate: v.union(v.number(), v.null()),
      latestYear: v.number(),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const latestMark = await ctx.db
      .query('marks')
      .withIndex('by_year')
      .order('desc')
      .first()
    if (!latestMark) return null
    const latestYear = latestMark.year
    const previousYear = latestYear - 1

    const marksCurrent = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', latestYear))
      .collect()
    const marksPrevious = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', previousYear))
      .collect()

    const centersCurrent = new Set(marksCurrent.map((m) => m.school_id))
    const centersPrevious = new Set(marksPrevious.map((m) => m.school_id))

    const totalCentersCurrent = centersCurrent.size
    const totalCentersPrevious = centersPrevious.size

    const trendRate =
      totalCentersPrevious === 0
        ? null
        : Math.round(
            ((totalCentersCurrent - totalCentersPrevious) /
              totalCentersPrevious) *
              100,
          ) / 100

    return {
      totalCentersCurrent,
      totalCentersPrevious,
      trendRate,
      latestYear,
    }
  },
})

export const getTotalLearners = query({
  args: {},
  returns: v.union(
    v.object({
      totalLearnersCurrent: v.number(),
      totalLearnersPrevious: v.number(),
      trendRate: v.union(v.number(), v.null()),
      latestYear: v.number(),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const latestMark = await ctx.db
      .query('marks')
      .withIndex('by_year')
      .order('desc')
      .first()
    if (!latestMark) return null
    const latestYear = latestMark.year
    const previousYear = latestYear - 1

    const marksCurrent = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', latestYear))
      .collect()
    const marksPrevious = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', previousYear))
      .collect()

    const totalLearnersCurrent = marksCurrent.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )

    const totalLearnersPrevious = marksPrevious.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )

    const trendRate =
      totalLearnersPrevious === 0
        ? null
        : Math.round(
            ((totalLearnersCurrent - totalLearnersPrevious) /
              totalLearnersPrevious) *
              100,
          ) / 100

    return {
      totalLearnersCurrent,
      totalLearnersPrevious,
      trendRate,
      latestYear,
    }
  },
})

export const getMatricPassRate = query({
  args: {},
  returns: v.union(
    v.object({
      passRateCurrent: v.union(v.number(), v.null()),
      passRatePrevious: v.union(v.number(), v.null()),
      trendRate: v.union(v.number(), v.null()),
      latestYear: v.number(),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const latestMark = await ctx.db
      .query('marks')
      .withIndex('by_year')
      .order('desc')
      .first()
    if (!latestMark) return null
    const latestYear = latestMark.year
    const previousYear = latestYear - 1

    const marksCurrent = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', latestYear))
      .collect()
    const marksPrevious = await ctx.db
      .query('marks')
      .withIndex('by_year', (q) => q.eq('year', previousYear))
      .collect()

    const totalWroteCurrent = marksCurrent.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )
    const totalPassedCurrent = marksCurrent.reduce(
      (sum, m) => sum + (m.total_achieved ?? 0),
      0,
    )

    const totalWrotePrevious = marksPrevious.reduce(
      (sum, m) => sum + m.total_wrote,
      0,
    )
    const totalPassedPrevious = marksPrevious.reduce(
      (sum, m) => sum + (m.total_achieved ?? 0),
      0,
    )

    const passRateCurrent =
      totalWroteCurrent === 0
        ? null
        : Math.round((totalPassedCurrent / totalWroteCurrent) * 10000) / 100

    const passRatePrevious =
      totalWrotePrevious === 0
        ? null
        : Math.round((totalPassedPrevious / totalWrotePrevious) * 10000) / 100

    const trendRate =
      passRateCurrent === null || passRatePrevious === null
        ? null
        : Math.round((passRateCurrent - passRatePrevious) * 100) / 100

    return {
      passRateCurrent,
      passRatePrevious,
      trendRate,
      latestYear,
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
    const latestMark = await ctx.db
      .query('marks')
      .withIndex('by_year')
      .order('desc')
      .first()
    const currentYear =
      args.year ?? latestMark?.year ?? new Date().getFullYear()
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
