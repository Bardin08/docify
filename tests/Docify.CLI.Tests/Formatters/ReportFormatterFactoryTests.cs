using Docify.CLI.Formatters;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Docify.CLI.Tests.Formatters;

public class ReportFormatterFactoryTests
{
    private readonly ReportFormatterFactory _factory;

    public ReportFormatterFactoryTests()
    {
        var mockTextLogger = new Mock<ILogger<TextReportFormatter>>();
        var mockJsonLogger = new Mock<ILogger<JsonReportFormatter>>();
        var mockMarkdownLogger = new Mock<ILogger<MarkdownReportFormatter>>();

        var textFormatter = new TextReportFormatter(mockTextLogger.Object);
        var jsonFormatter = new JsonReportFormatter(mockJsonLogger.Object);
        var markdownFormatter = new MarkdownReportFormatter(mockMarkdownLogger.Object);

        _factory = new ReportFormatterFactory(textFormatter, jsonFormatter, markdownFormatter);
    }

    [Fact]
    public void GetFormatter_Text_ReturnsTextReportFormatter()
    {
        // Act
        var formatter = _factory.GetFormatter("text");

        // Assert
        formatter.ShouldBeOfType<TextReportFormatter>();
    }

    [Fact]
    public void GetFormatter_Json_ReturnsJsonReportFormatter()
    {
        // Act
        var formatter = _factory.GetFormatter("json");

        // Assert
        formatter.ShouldBeOfType<JsonReportFormatter>();
    }

    [Fact]
    public void GetFormatter_Markdown_ReturnsMarkdownReportFormatter()
    {
        // Act
        var formatter = _factory.GetFormatter("markdown");

        // Assert
        formatter.ShouldBeOfType<MarkdownReportFormatter>();
    }

    [Theory]
    [InlineData("TEXT")]
    [InlineData("Json")]
    [InlineData("MARKDOWN")]
    public void GetFormatter_CaseInsensitive_ReturnsCorrectFormatter(string format)
    {
        // Act
        var formatter = _factory.GetFormatter(format);

        // Assert
        formatter.ShouldNotBeNull();
    }

    [Fact]
    public void GetFormatter_InvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() => _factory.GetFormatter("invalid"));
        ex.Message.ShouldContain("Invalid format");
        ex.ParamName.ShouldBe("format");
    }

    [Fact]
    public void GetFormatter_NullFormat_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _factory.GetFormatter(null!));
    }
}
