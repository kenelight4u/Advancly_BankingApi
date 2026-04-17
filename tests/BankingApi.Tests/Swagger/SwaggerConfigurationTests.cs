using FluentAssertions;

namespace BankingApi.Tests.Swagger;

public class SwaggerConfigurationTests
{
    [Fact]
    public void Program_RegistersBearerSecurityRequirementUsingSchemeReference()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "BankingApi.Api", "Program.cs"));

        File.Exists(programPath).Should().BeTrue(
            $"Program.cs should exist at {programPath}");

        var source = File.ReadAllText(programPath);

        source.Should().Contain("options.AddSecurityDefinition(\"Bearer\"");
        source.Should().Contain("options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement");
        source.Should().Contain("new OpenApiSecuritySchemeReference(\"Bearer\")");
    }
}
