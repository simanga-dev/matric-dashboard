import { mutation } from './_generated/server'
import { v } from 'convex/values'

const sampleAchievers = [
  // 2024 Top Achievers
  {
    year: 2024,
    name: 'Thando Mkhize',
    school_name: 'Pretoria Boys High School',
    province: 'Gauteng',
    percentage_mark: 98.7,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Thando',
    rank: 1,
  },
  {
    year: 2024,
    name: 'Naledi Dlamini',
    school_name: 'Durban Girls College',
    province: 'KwaZulu-Natal',
    percentage_mark: 97.9,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Naledi',
    rank: 2,
  },
  {
    year: 2024,
    name: 'Sipho Ndlovu',
    school_name: 'Bishops Diocesan College',
    province: 'Western Cape',
    percentage_mark: 97.5,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Sipho',
    rank: 3,
  },
  {
    year: 2024,
    name: 'Lerato Mokoena',
    school_name: 'St Stithians College',
    province: 'Gauteng',
    percentage_mark: 96.8,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Lerato',
    rank: 4,
  },
  {
    year: 2024,
    name: 'Mpho Khumalo',
    school_name: 'Hilton College',
    province: 'KwaZulu-Natal',
    percentage_mark: 96.2,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Mpho',
    rank: 5,
  },
  {
    year: 2024,
    name: 'Zanele Nkosi',
    school_name: 'Rondebosch Boys High',
    province: 'Western Cape',
    percentage_mark: 95.9,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Zanele',
    rank: 6,
  },

  // 2023 Top Achievers
  {
    year: 2023,
    name: 'Kagiso Molefe',
    school_name: 'King Edward VII School',
    province: 'Gauteng',
    percentage_mark: 99.1,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Kagiso',
    rank: 1,
  },
  {
    year: 2023,
    name: 'Nomvula Zwane',
    school_name: 'Westville Girls High',
    province: 'KwaZulu-Natal',
    percentage_mark: 98.4,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Nomvula',
    rank: 2,
  },
  {
    year: 2023,
    name: 'Tebogo Mabaso',
    school_name: 'Michaelhouse',
    province: 'KwaZulu-Natal',
    percentage_mark: 97.8,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Tebogo',
    rank: 3,
  },
  {
    year: 2023,
    name: 'Ayanda Sithole',
    school_name: 'Westerford High School',
    province: 'Western Cape',
    percentage_mark: 97.2,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Ayanda',
    rank: 4,
  },
  {
    year: 2023,
    name: 'Bongani Cele',
    school_name: 'Grey High School',
    province: 'Eastern Cape',
    percentage_mark: 96.5,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Bongani',
    rank: 5,
  },

  // 2022 Top Achievers
  {
    year: 2022,
    name: 'Lindiwe Mahlangu',
    school_name: 'Jeppe High School for Girls',
    province: 'Gauteng',
    percentage_mark: 98.9,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Lindiwe',
    rank: 1,
  },
  {
    year: 2022,
    name: 'Sibusiso Radebe',
    school_name: 'Maritzburg College',
    province: 'KwaZulu-Natal',
    percentage_mark: 98.1,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Sibusiso',
    rank: 2,
  },
  {
    year: 2022,
    name: 'Precious Mbeki',
    school_name: 'Sans Souci Girls High',
    province: 'Western Cape',
    percentage_mark: 97.6,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Precious',
    rank: 3,
  },
  {
    year: 2022,
    name: 'Themba Zulu',
    school_name: 'St Johns College',
    province: 'Gauteng',
    percentage_mark: 96.9,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Themba',
    rank: 4,
  },
  {
    year: 2022,
    name: 'Nokuthula Shabangu',
    school_name: 'Wykeham Collegiate',
    province: 'KwaZulu-Natal',
    percentage_mark: 96.3,
    headshot_url: 'https://api.dicebear.com/7.x/avataaars/svg?seed=Nokuthula',
    rank: 5,
  },
]

export const seedTopAchievers = mutation({
  args: {},
  returns: v.object({
    success: v.boolean(),
    recordsInserted: v.number(),
  }),
  handler: async (ctx) => {
    // Clear existing data
    const existing = await ctx.db.query('top_achievers').collect()
    for (const record of existing) {
      await ctx.db.delete(record._id)
    }

    // Insert sample data
    for (const achiever of sampleAchievers) {
      await ctx.db.insert('top_achievers', achiever)
    }

    return { success: true, recordsInserted: sampleAchievers.length }
  },
})
