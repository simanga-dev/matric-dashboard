using MatricDasbhoard.Application.Features.Dashboard.Dtos;
using MatricDasbhoard.WebApi.Features.Dashboard.Dtos;

namespace MatricDasbhoard.WebApi.Features.Dashboard;

/// <summary>
/// Maps dashboard DTOs between application and WebApi layers.
/// </summary>
internal static class DashboardMapper
{
    /// <summary>
    /// Maps application stats output to WebApi response.
    /// </summary>
    public static DashboardStatsResponse ToResponse(this DashboardStatsOutput output) => new()
    {
        Year = output.Year,
        TopSchools = output.TopSchools.ToResponse(),
        ExamCenters = output.ExamCenters.ToResponse(),
        TotalLearners = output.TotalLearners.ToResponse(),
        PassRate = output.PassRate.ToResponse()
    };

    /// <summary>
    /// Maps a single stat item.
    /// </summary>
    public static StatItemResponse ToResponse(this StatItemOutput output) => new()
    {
        Total = output.Total,
        Trend = output.Trend
    };

    /// <summary>
    /// Maps a pass rate trend data point.
    /// </summary>
    public static PassRateTrendResponse ToResponse(this PassRateTrendOutput output) => new()
    {
        Year = output.Year,
        PassRate = output.PassRate,
        TotalLearners = output.TotalLearners
    };

    /// <summary>
    /// Maps aggregated region (country/province/district) stats.
    /// </summary>
    public static RegionStatsResponse ToResponse(this RegionStatsOutput output) => new()
    {
        Scope = output.Scope,
        Name = output.Name,
        Province = output.Province,
        Year = output.Year,
        SchoolCount = output.SchoolCount,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate,
        Years = output.Years.Select(y => y.ToResponse()).ToList()
    };

    /// <summary>
    /// Maps a single year of region performance.
    /// </summary>
    public static RegionYearResponse ToResponse(this RegionYearOutput output) => new()
    {
        Year = output.Year,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate
    };

    /// <summary>
    /// Maps a school entry.
    /// </summary>
    public static SchoolResponse ToResponse(this SchoolOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Province = output.Province,
        Circuit = output.Circuit,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate,
        TotalAchieved = output.TotalAchieved,
        MapUpdatedAt = output.MapUpdatedAt
    };

    /// <summary>
    /// Maps a paginated school list.
    /// </summary>
    public static SchoolListResponse ToResponse(this SchoolListOutput output) => new()
    {
        Items = output.Items.Select(i => i.ToResponse()).ToList(),
        TotalCount = output.TotalCount,
        PageNumber = output.PageNumber,
        PageSize = output.PageSize
    };

    /// <summary>
    /// Maps full school detail.
    /// </summary>
    public static SchoolDetailResponse ToResponse(this SchoolDetailOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Province = output.Province,
        Circuit = output.Circuit,
        CentreNumber = output.CentreNumber,
        TotalAchieved = output.TotalAchieved,
        Quintile = output.Quintile,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate,
        Years = output.Years.Select(y => y.ToResponse()).ToList(),
        Comparison = output.Comparison.ToResponse(),
        SubjectsOfferedYear = output.SubjectsOfferedYear,
        SubjectsOffered = output.SubjectsOffered?.Select(s => s.ToResponse()).ToList() ?? [],
        QuintileCode = output.QuintileCode,
        Email = output.Email,
        Telephone = output.Telephone,
        Sector = output.Sector,
        Phase = output.Phase,
        LandOwnership = output.LandOwnership,
        BuildingOwnership = output.BuildingOwnership,
        IsSection21 = output.IsSection21,
        IsNoFeeSchool = output.IsNoFeeSchool,
        UrbanRural = output.UrbanRural,
        Enrolment = output.Enrolment is null
            ? null
            : new SchoolEnrolmentResponse
            {
                Year = output.Enrolment.Year,
                Learners = output.Enrolment.Learners,
                Educators = output.Enrolment.Educators
            }
    };

    extension(OfferedSubjectOutput output)
    {
        /// <summary>
        /// Maps a subject offered by a school.
        /// </summary>
        public OfferedSubjectResponse ToResponse() => new()
        {
            Code = output.Code,
            Name = output.Name,
            Year = output.Year,
            TotalWrote = output.TotalWrote,
            PercentAchieved = output.PercentAchieved
        };
    }

    extension(SchoolSubjectOutput output)
    {
        /// <summary>
        /// Maps a single subject result for a school.
        /// </summary>
        public SchoolSubjectResponse ToResponse() => new()
        {
            SubjectId = output.SubjectId,
            Code = output.Code,
            Name = output.Name,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved
        };
    }

    extension(SchoolSubjectsOutput output)
    {
        /// <summary>
        /// Maps per-subject school results for the selected year.
        /// </summary>
        public SchoolSubjectsResponse ToResponse() => new()
        {
            SchoolId = output.SchoolId,
            Year = output.Year,
            AvailableYears = output.AvailableYears,
            Subjects = output.Subjects.Select(s => s.ToResponse()).ToList()
        };
    }

    extension(RegionSubjectsOutput output)
    {
        /// <summary>
        /// Maps aggregated per-subject region results for the selected year.
        /// </summary>
        public RegionSubjectsResponse ToResponse() => new()
        {
            Scope = output.Scope,
            Name = output.Name,
            Province = output.Province,
            Year = output.Year,
            AvailableYears = output.AvailableYears,
            SchoolCount = output.SchoolCount,
            Subjects = output.Subjects.Select(s => s.ToResponse()).ToList()
        };
    }

    extension(RegionSubjectOutput output)
    {
        /// <summary>
        /// Maps a single aggregated subject row.
        /// </summary>
        public RegionSubjectResponse ToResponse() => new()
        {
            Code = output.Code,
            Name = output.Name,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved,
            SchoolCount = output.SchoolCount
        };
    }

    extension(SubjectDetailOutput output)
    {
        /// <summary>
        /// Maps a single subject's scoped performance, trend, and regional breakdowns.
        /// </summary>
        public SubjectDetailResponse ToResponse() => new()
        {
            Code = output.Code,
            Name = output.Name,
            IsGateway = output.IsGateway,
            Scope = output.Scope,
            ScopeName = output.ScopeName,
            Province = output.Province,
            District = output.District,
            Year = output.Year,
            AvailableYears = output.AvailableYears,
            SchoolCount = output.SchoolCount,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved,
            Trend = output.Trend.Select(t => t.ToResponse()).ToList(),
            Provinces = output.Provinces.Select(r => r.ToResponse()).ToList(),
            Districts = output.Districts.Select(r => r.ToResponse()).ToList()
        };
    }

    extension(SubjectYearOutput output)
    {
        /// <summary>
        /// Maps one year of a subject's aggregated performance.
        /// </summary>
        public SubjectYearResponse ToResponse() => new()
        {
            Year = output.Year,
            SchoolCount = output.SchoolCount,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved
        };
    }

    extension(SubjectRegionOutput output)
    {
        /// <summary>
        /// Maps a subject's aggregated performance in one province or district.
        /// </summary>
        public SubjectRegionResponse ToResponse() => new()
        {
            Name = output.Name,
            SchoolCount = output.SchoolCount,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved
        };
    }

    extension(SubjectSchoolListOutput output)
    {
        /// <summary>
        /// Maps a paginated list of schools that offered a subject.
        /// </summary>
        public SubjectSchoolListResponse ToResponse() => new()
        {
            Year = output.Year,
            Items = output.Items.Select(i => i.ToResponse()).ToList(),
            TotalCount = output.TotalCount,
            PageNumber = output.PageNumber,
            PageSize = output.PageSize
        };
    }

    extension(SubjectSchoolOutput output)
    {
        /// <summary>
        /// Maps one school's result in a subject.
        /// </summary>
        public SubjectSchoolResponse ToResponse() => new()
        {
            Id = output.Id,
            Name = output.Name,
            Province = output.Province,
            District = output.District,
            Quintile = output.Quintile,
            TotalWrote = output.TotalWrote,
            Achieved = output.Achieved,
            PercentAchieved = output.PercentAchieved
        };
    }

    /// <summary>
    /// Maps a single year's performance.
    /// </summary>
    public static SchoolYearResponse ToResponse(this SchoolYearOutput output) => new()
    {
        Year = output.Year,
        Progressed = output.Progressed,
        TotalWrote = output.TotalWrote,
        TotalPassed = output.TotalPassed,
        PassRate = output.PassRate
    };

    /// <summary>
    /// Maps province and national comparison.
    /// </summary>
    public static SchoolComparisonResponse ToResponse(this SchoolComparisonOutput output) => new()
    {
        ProvinceRank = output.ProvinceRank,
        Year = output.Year,
        ProvinceSchoolCount = output.ProvinceSchoolCount,
        ProvinceAveragePassRate = output.ProvinceAveragePassRate,
        NationalAveragePassRate = output.NationalAveragePassRate
    };
}
