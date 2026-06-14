using MatricDasbhoard.Application.Features.Dashboard;
using MatricDasbhoard.Application.Features.Dashboard.Dtos;

namespace MatricDasbhoard.Infrastructure.Features.Dashboard.Services;

/// <summary>
/// Returns mock dashboard data for the frontend overview page.
/// Replace with real database queries once the data model is available.
/// </summary>
internal sealed class DashboardService : IDashboardService
{
    private static readonly IReadOnlyList<PassRateTrendOutput> PassRateTrends = new List<PassRateTrendOutput>
    {
        new(2008, 62.6, 533561),
        new(2009, 60.6, 552073),
        new(2010, 67.8, 537543),
        new(2011, 70.2, 496090),
        new(2012, 73.9, 511152),
        new(2013, 78.2, 562112),
        new(2014, 75.8, 532860),
        new(2015, 70.7, 644536),
        new(2016, 72.5, 610178),
        new(2017, 75.1, 629155),
        new(2018, 78.2, 624733),
        new(2019, 81.3, 616754),
        new(2020, 76.2, 578468),
        new(2021, 76.4, 706451),
        new(2022, 80.1, 699659),
        new(2023, 82.9, 719541),
        new(2024, 87.3, 737472)
    };

    private static readonly IReadOnlyList<SchoolOutput> AllSchools = GenerateSchools();

    public Task<DashboardStatsOutput> GetStatsAsync()
    {
        var stats = new DashboardStatsOutput(
            TopSchools: new StatItemOutput(187, 8.5),
            ExamCenters: new StatItemOutput(6820, 2.1),
            TotalLearners: new StatItemOutput(737472, 5.4),
            PassRate: new StatItemOutput(829, 3.6)
        );
        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<PassRateTrendOutput>> GetPassRateTrendsAsync(string? years = null)
    {
        var data = years switch
        {
            "5" => PassRateTrends.Where(t => t.Year >= 2020).ToList(),
            "10" => PassRateTrends.Where(t => t.Year >= 2015).ToList(),
            _ => PassRateTrends.ToList()
        };
        return Task.FromResult<IReadOnlyList<PassRateTrendOutput>>(data);
    }

    public Task<SchoolListOutput> GetSchoolsAsync(int pageNumber, int pageSize, string? search = null)
    {
        var filtered = AllSchools.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(s =>
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Province.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered.ToList();
        var totalCount = items.Count;
        var paged = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new SchoolListOutput(paged, totalCount, pageNumber, pageSize));
    }

    private static IReadOnlyList<SchoolOutput> GenerateSchools()
    {
        var provinces = new[] { "Gauteng", "KwaZulu-Natal", "Western Cape", "Eastern Cape", "Limpopo", "Mpumalanga", "North West", "Free State", "Northern Cape" };
        var circuits = new[] { "Tshwane South", "Johannesburg East", "Ethekwini North", "Cape Town Metro", "Nelson Mandela Bay", "Mangaung", "Rustenburg", "Polokwane", "Mbombela" };

        var names = new[]
        {
            "Pretoria High School for Girls", "Parktown Boys' High School", "Rondebosch Boys' High",
            "Wynberg Girls' High School", "Maritzburg College", "Hilton College",
            "St. Andrew's College", "Diocesan School for Girls", "St. Mary's Waverley",
            "King Edward VII School", "Jeppe High School for Boys", "Sandown High School",
            "Hoërskool Waterkloof", "Hoërskool Menlopark", "Hoërskool Garsfontein",
            "Brescia House School", "St. Stithians College", "Roedean School",
            "Sacred Heart College", "St. John's College", "St. Mary's School",
            "Crawford College Lonehill", "St. Peter's College", "St. David's Marist Inanda",
            "Deutsche Schule Johannesburg", "American International School", "British International College",
            "Pinnacle College Kyalami", "Trinityhouse Randpark Ridge", "Steyn City School",
            "Curro Aurora", "Curro Hazeldean", "Curro Mossel Bay",
            "Reddam House Umhlanga", "Reddam House Durbanville", "Reddam House Bedfordview",
            "HeronBridge College", "Kearsney College", "Michaelhouse",
            "Clifton College", "St. Henry's Marist College", "Thomas More College",
            "Crawford College North Coast", "Crawford College La Lucia", "Our Lady of Fatima",
            "Holy Family College", "St. Benedict's College", "St. Dunstan's College",
            "Kingswood College", "St. Andrew's School", "St. James School",
            "Somerset College", "St. George's Grammar School", "St. Cyprian's School",
            "Bishops Diocesan College", "Herschel Girls' School", "Rustenburg Girls' High",
            "Springfield Convent", "Swellendam Secondary School", "Outeniqua High School",
            "Pearson High School", "Grey High School", "Grey College",
            "St. Michael's School", "Eunice High School", "St. Mary's DSG",
            "St. Anne's Diocesan College", "Wykeham Collegiate", "Epworth High School",
            "Cordwalles Preparatory School", "Cowies Hill Primary School", "Clifton Preparatory School",
            "Berea Primary School", "Glenwood Preparatory School", "Durban Preparatory School",
            "Westville Senior Primary School", "Northlands Primary School", "Umhlanga Preparatory School"
        };

        var rand = new Random(42);
        var schools = names.Select((name, i) =>
        {
            var totalWrote = rand.Next(80, 350);
            var passRate = Math.Round(rand.NextDouble() * 40 + 55, 1);
            var totalPassed = (int)Math.Round(totalWrote * passRate / 100);
            var hasBachelors = rand.NextDouble() > 0.3;
            var totalAchieved = hasBachelors ? (int?)rand.Next((int)(totalPassed * 0.3), totalPassed) : null;

            return new SchoolOutput(
                i + 1,
                name,
                provinces[i % provinces.Length],
                circuits[i % circuits.Length],
                totalWrote,
                totalPassed,
                passRate,
                totalAchieved
            );
        }).ToList();

        return schools;
    }
}
