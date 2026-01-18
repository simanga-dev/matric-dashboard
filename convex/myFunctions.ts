import { v } from 'convex/values'
import { query, mutation, action } from './_generated/server'
import { api } from './_generated/api'

// Write your Convex functions in any file inside this directory (`convex`).
// See https://docs.convex.dev/functions for more.

export const getTopSchools = query({
  args: {},
  returns: v.union(
    v.object({
      totalSchools2023: v.number(),
      totalSchools2022: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const allMarks = await ctx.db.query('marks').collect()

    const perfectSchools2023 = new Set(
      allMarks
        .filter((m) => m.year === 2023 && m.total_wrote === m.total_archived)
        .map((m) => m.school_id),
    )

    const perfectSchools2022 = new Set(
      allMarks
        .filter((m) => m.year === 2022 && m.total_wrote === m.total_archived)
        .map((m) => m.school_id),
    )

    const totalSchools2023 = perfectSchools2023.size
    const totalSchools2022 = perfectSchools2022.size

    const trendRate =
      totalSchools2022 === 0
        ? null
        : Math.round(
            ((totalSchools2023 - totalSchools2022) / totalSchools2022) * 100,
          ) / 100

    return {
      totalSchools2023,
      totalSchools2022,
      trendRate,
    }
  },
})

export const getExamCenters = query({
  args: {},
  returns: v.union(
    v.object({
      totalCenters2023: v.number(),
      totalCenters2022: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const allMarks = await ctx.db.query('marks').collect()

    const centers2023 = new Set(
      allMarks.filter((m) => m.year === 2023).map((m) => m.school_id),
    )
    const centers2022 = new Set(
      allMarks.filter((m) => m.year === 2022).map((m) => m.school_id),
    )

    const totalCenters2023 = centers2023.size
    const totalCenters2022 = centers2022.size

    const trendRate =
      totalCenters2022 === 0
        ? null
        : Math.round(
            ((totalCenters2023 - totalCenters2022) / totalCenters2022) * 100,
          ) / 100

    return {
      totalCenters2023,
      totalCenters2022,
      trendRate,
    }
  },
})

export const getTotalLearners = query({
  args: {},
  returns: v.union(
    v.object({
      totalLearners2023: v.number(),
      totalLearners2022: v.number(),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const allMarks = await ctx.db.query('marks').collect()

    const totalLearners2023 = allMarks
      .filter((m) => m.year === 2023)
      .reduce((sum, m) => sum + m.total_wrote, 0)

    const totalLearners2022 = allMarks
      .filter((m) => m.year === 2022)
      .reduce((sum, m) => sum + m.total_wrote, 0)

    const trendRate =
      totalLearners2022 === 0
        ? null
        : Math.round(
            ((totalLearners2023 - totalLearners2022) / totalLearners2022) * 100,
          ) / 100

    return {
      totalLearners2023,
      totalLearners2022,
      trendRate,
    }
  },
})

export const getMatricPassRate = query({
  args: {},
  returns: v.union(
    v.object({
      passRate2023: v.union(v.number(), v.null()),
      passRate2022: v.union(v.number(), v.null()),
      trendRate: v.union(v.number(), v.null()),
    }),
    v.null(),
  ),
  handler: async (ctx) => {
    const allMarks = await ctx.db.query('marks').collect()

    const marks2023 = allMarks.filter((m) => m.year === 2023)
    const marks2022 = allMarks.filter((m) => m.year === 2022)

    const totalWrote2023 = marks2023.reduce((sum, m) => sum + m.total_wrote, 0)
    const totalPassed2023 = marks2023.reduce(
      (sum, m) => sum + m.total_archived,
      0,
    )

    const totalWrote2022 = marks2022.reduce((sum, m) => sum + m.total_wrote, 0)
    const totalPassed2022 = marks2022.reduce(
      (sum, m) => sum + m.total_archived,
      0,
    )

    const passRate2023 =
      totalWrote2023 === 0
        ? null
        : Math.round((totalPassed2023 / totalWrote2023) * 10000) / 100

    const passRate2022 =
      totalWrote2022 === 0
        ? null
        : Math.round((totalPassed2022 / totalWrote2022) * 10000) / 100

    const trendRate =
      passRate2023 === null || passRate2022 === null
        ? null
        : Math.round((passRate2023 - passRate2022) * 100) / 100

    return {
      passRate2023,
      passRate2022,
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
      passRate2024: v.union(v.number(), v.null()),
      passRate2023: v.union(v.number(), v.null()),
      totalWrote: v.number(),
      totalAchieved: v.number(),
      trend: v.union(v.number(), v.null()),
      status: v.string(),
    }),
  ),
  handler: async (ctx, args) => {
    const currentYear = args.year ?? 2023
    const previousYear = currentYear - 1

    const schools = await ctx.db.query('school').collect()
    const allMarks = await ctx.db.query('marks').collect()

    const marksBySchool = new Map<
      string,
      { current?: (typeof allMarks)[0]; previous?: (typeof allMarks)[0] }
    >()

    for (const mark of allMarks) {
      const schoolId = mark.school_id
      const existing = marksBySchool.get(schoolId) || {}
      if (mark.year === currentYear) {
        existing.current = mark
      } else if (mark.year === previousYear) {
        existing.previous = mark
      }
      marksBySchool.set(schoolId, existing)
    }

    const results = schools
      .map((school) => {
        const marks = marksBySchool.get(school._id)
        const currentMark = marks?.current
        const previousMark = marks?.previous

        const passRate2024 = currentMark?.percentage_archived ?? null
        const passRate2023 = previousMark?.percentage_archived ?? null

        const trend =
          passRate2024 !== null && passRate2023 !== null
            ? Math.round((passRate2024 - passRate2023) * 100) / 100
            : null

        let status: string
        if (passRate2024 === null) {
          status = 'No Data'
        } else if (passRate2024 >= 90) {
          status = 'Excellent'
        } else if (passRate2024 >= 70) {
          status = 'Good'
        } else if (passRate2024 >= 50) {
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
          passRate2024,
          passRate2023,
          totalWrote: currentMark?.total_wrote ?? 0,
          totalAchieved: currentMark?.total_archived ?? 0,
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
