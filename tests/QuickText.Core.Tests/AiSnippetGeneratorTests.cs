using QuickText.Core.Settings;

namespace QuickText.Core.Tests;

public class AiSnippetGeneratorTests
{
    [Fact]
    public void ParseResponse_reads_plain_json()
    {
        var d = AiSnippetGenerator.ParseResponse(
            """{"name":"催款","abbr":"cuikuan","body":"您好，请尽快付款。","useVariables":false}""");
        Assert.Equal("催款", d.Name);
        Assert.Equal("cuikuan", d.Abbr);
        Assert.Equal("您好，请尽快付款。", d.Body);
        Assert.False(d.UseVariables);
    }

    [Fact]
    public void ParseResponse_forces_useVariables_when_body_has_placeholders()
    {
        var d = AiSnippetGenerator.ParseResponse(
            """{"name":"重构","abbr":"dmcg","body":"语言：{语言}\n代码：\n{剪贴板}","useVariables":false}""");
        Assert.True(d.UseVariables);
        Assert.Contains("{剪贴板}", d.Body);
    }

    [Fact]
    public void ParseResponse_strips_markdown_fence_and_prose()
    {
        var d = AiSnippetGenerator.ParseResponse(
            "Here you go:\n```json\n{\"name\":\"Hi\",\"abbr\":\"hi\",\"body\":\"Hello\"}\n```\n");
        Assert.Equal("Hi", d.Name);
        Assert.Equal("hi", d.Abbr);
        Assert.Equal("Hello", d.Body);
    }

    [Fact]
    public void ParseResponse_sanitizes_abbr_whitespace_and_punct()
    {
        var d = AiSnippetGenerator.ParseResponse(
            """{"name":"A","abbr":" ab/cd ","body":"x"}""");
        Assert.Equal("abcd", d.Abbr);
    }

    [Fact]
    public void ParseResponse_rejects_empty_or_non_json()
    {
        Assert.Throws<FormatException>(() => AiSnippetGenerator.ParseResponse(""));
        Assert.Throws<FormatException>(() => AiSnippetGenerator.ParseResponse("not json"));
        Assert.Throws<FormatException>(() =>
            AiSnippetGenerator.ParseResponse("""{"name":"","abbr":"","body":""}"""));
    }

    [Fact]
    public void UserPrompt_includes_dev_scenario_guidance()
    {
        var msg = AiSnippetGenerator.UserPrompt(AiSnippetTemplates.Dev, "重构");
        Assert.Contains("{剪贴板}", msg);
        Assert.Contains("重构", msg);
    }

    [Fact]
    public void SystemPrompt_documents_placeholder_syntax()
    {
        var sys = AiSnippetGenerator.SystemPrompt("zh-Hans");
        Assert.Contains("{剪贴板}", sys);
        Assert.Contains("useVariables", sys);
    }

    [Fact]
    public void CompletionsUrl_matches_provider_shapes()
    {
        Assert.Equal(
            "https://api.openai.com/v1/chat/completions",
            AiChatClient.CompletionsUrl(AiProviders.OpenAi, "https://api.openai.com/v1", "gpt-4o-mini"));
        Assert.Equal(
            "https://api.anthropic.com/v1/messages",
            AiChatClient.CompletionsUrl(AiProviders.Anthropic, "https://api.anthropic.com", "claude"));
        Assert.Equal(
            "https://x.openai.azure.com/openai/deployments/my-dep/chat/completions?api-version=2024-06-01",
            AiChatClient.CompletionsUrl(AiProviders.Azure, "https://x.openai.azure.com", "my-dep"));
        Assert.Null(AiChatClient.CompletionsUrl(AiProviders.OpenAi, "", "m"));
    }

    [Fact]
    public void ExtractAssistantText_openai_and_anthropic()
    {
        var openai = AiChatClient.ExtractAssistantText(AiProviders.OpenAi,
            """{"choices":[{"message":{"content":"{\"name\":\"N\",\"abbr\":\"n\",\"body\":\"B\"}"}}]}""");
        Assert.Contains("\"name\":\"N\"", openai);

        var anth = AiChatClient.ExtractAssistantText(AiProviders.Anthropic,
            """{"content":[{"type":"text","text":"hello"}]}""");
        Assert.Equal("hello", anth);
    }

    [Fact]
    public void BuildRequestBody_openai_has_system_and_user()
    {
        var json = AiChatClient.BuildRequestBody(AiProviders.OpenAi, "gpt", "SYS", "USR");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var msgs = doc.RootElement.GetProperty("messages");
        Assert.Equal(2, msgs.GetArrayLength());
        Assert.Equal("system", msgs[0].GetProperty("role").GetString());
        Assert.Equal("SYS", msgs[0].GetProperty("content").GetString());
        Assert.Equal("user", msgs[1].GetProperty("role").GetString());
    }

    [Fact]
    public void BuildRequestBody_anthropic_uses_top_level_system()
    {
        var json = AiChatClient.BuildRequestBody(AiProviders.Anthropic, "claude", "SYS", "USR");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("SYS", doc.RootElement.GetProperty("system").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("messages").GetArrayLength());
    }
}
