using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

/// <summary>HTTP helpers shared by the Learning API tests; they send the CSRF token like the UI does.</summary>
public static class LearningHttp
{
    public static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    public static Task<HttpResponseMessage> Put(HttpClient client, string path, object body) => Send(client, HttpMethod.Put, path, body);
    public static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => Send(client, HttpMethod.Post, path, body);

    public static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<string> NewClass(HttpClient client, string name) =>
        (await (await TestApplication.Post(client, "/api/classes", new { name, subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

    public static async Task<(HttpClient Client, string ClassId, string Email)> Teacher(TestApplication app, string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        return (client, await NewClass(client, "Toán 2"), email);
    }

    // The bodies below are what ui/src/Personalization.tsx sends.
    public static object CurriculumBody(int version = 0) => new
    {
        subject = "Toán", grade = "Lớp 2", term = "Học kỳ 1", textbook = "Kết nối tri thức",
        masteryThreshold = 80, minTasks = 5, requiredAttempts = 2, version
    };
    public static object SkillBody(string code, int version, bool taught = false, bool archived = false) => new
    {
        code, name = $"Kỹ năng {code}", strand = "Số và phép tính", taught, archived, version
    };

    /// <summary>Configures the class and adds the given skills; returns skill ids by code.</summary>
    public static async Task<(Dictionary<string, string> Ids, int Version)> Seed(HttpClient client, string classId, params string[] codes)
    {
        var path = $"/api/learning/classes/{classId}/curriculum";
        var current = await Json(await Put(client, path, CurriculumBody()), HttpStatusCode.OK);
        var version = current.GetProperty("version").GetInt32();
        foreach (var code in codes)
        {
            current = await Json(await Post(client, $"{path}/skills", SkillBody(code, version)), HttpStatusCode.Created);
            version = current.GetProperty("version").GetInt32();
        }
        var ids = current.GetProperty("skills").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("id").GetString()!);
        return (ids, version);
    }
}
