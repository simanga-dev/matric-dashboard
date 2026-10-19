using FluentValidation.TestHelper;
using MatricDasbhoard.WebApi.Features.SchoolSpatial;

namespace MatricDasbhoard.Api.Tests.Validators;

public class SaveSpatialRequestValidatorTests
{
    [Theory]
    [InlineData("reason", "source", true)]
    [InlineData(" ", "source", false)]
    [InlineData("reason", " ", false)]
    public void Metadata_RequiresReasonAndSource(string reason, string source, bool valid)
    {
        var request = new SaveSpatialRequest
        {
            ExpectedRevision = 0,
            Boundary = null,
            LocationMode = "imported",
            Location = null,
            Reason = reason,
            SourceDescription = source
        };
        var result = new SaveSpatialRequestValidator().TestValidate(request);
        Assert.Equal(valid, result.IsValid);
    }
}
