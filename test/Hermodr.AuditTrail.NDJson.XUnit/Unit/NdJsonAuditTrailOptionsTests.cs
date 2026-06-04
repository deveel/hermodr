namespace Hermodr.AuditTrail.NDJson.XUnit.Unit;

public class NdJsonAuditTrailOptionsTests
{
    [Fact]
    public void Defaults_DirectoryPath_ShouldBeInTemp()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Contains("audit-trail", options.DirectoryPath);
        Assert.StartsWith(Path.GetTempPath(), options.DirectoryPath);
    }

    [Fact]
    public void Defaults_MaxFileSizeBytes_ShouldBe10Mb()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Equal(10 * 1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void Defaults_RollInterval_ShouldBeNull()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Null(options.RollInterval);
    }

    [Fact]
    public void Defaults_MaxFileCount_ShouldBe30()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Equal(30, options.MaxFileCount);
    }

    [Fact]
    public void Defaults_FileNamePrefix_ShouldBeAuditTrail()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Equal("audit-trail", options.FileNamePrefix);
    }

    [Fact]
    public void Defaults_FileExtension_ShouldBeNdjson()
    {
        var options = new NdJsonAuditTrailOptions();

        Assert.Equal(".ndjson", options.FileExtension);
    }

    [Fact]
    public void Options_ShouldBeMutable()
    {
        var options = new NdJsonAuditTrailOptions
        {
            DirectoryPath = "/custom/path",
            MaxFileSizeBytes = 1024,
            RollInterval = TimeSpan.FromMinutes(5),
            MaxFileCount = 5,
            FileNamePrefix = "events",
            FileExtension = ".jsonl"
        };

        Assert.Equal("/custom/path", options.DirectoryPath);
        Assert.Equal(1024, options.MaxFileSizeBytes);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RollInterval);
        Assert.Equal(5, options.MaxFileCount);
        Assert.Equal("events", options.FileNamePrefix);
        Assert.Equal(".jsonl", options.FileExtension);
    }
}
