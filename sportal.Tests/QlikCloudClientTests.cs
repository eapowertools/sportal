using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using sportal.Services;
using Xunit;

namespace sportal.Tests
{
	public class QlikCloudClientTests
	{
		private class FakeHandler : HttpMessageHandler
		{
			public List<HttpRequestMessage> Requests = new List<HttpRequestMessage>();
			public Func<HttpRequestMessage, HttpResponseMessage> Respond;

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Requests.Add(request);
				return Task.FromResult(Respond(request));
			}
		}

		private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
		{
			return new HttpResponseMessage(status) { Content = new StringContent(body) };
		}

		private static QlikCloudClient ClientFor(FakeHandler handler)
		{
			return new QlikCloudClient(() => handler) { RetryDelay = _ => Task.CompletedTask };
		}

		private const string ME = @"{ ""subject"": ""admin"", ""assignedRoles"": [ { ""name"": ""TenantAdmin"" } ] }";

		[Fact]
		public async Task FetchAsync_FollowsPaginationAndSendsLoginHeaders()
		{
			var handler = new FakeHandler();
			handler.Respond = r =>
			{
				string path = r.RequestUri.AbsolutePath;
				if (path == "/login/jwt-session") return Json("{}");
				if (path == "/api/v1/users/me") return Json(ME);
				if (path == "/api/v1/groups") return Json(@"{ ""data"": [ { ""id"": ""g1"" } ] }");
				if (r.RequestUri.Query.Contains("next=2")) return Json(@"{ ""data"": [ { ""id"": ""u2"" } ] }");
				return Json(@"{ ""data"": [ { ""id"": ""u1"" } ], ""links"": { ""next"": { ""href"": ""https://t.example.com/api/v1/users?limit=100&next=2"" } } }");
			};

			TenantSnapshot snapshot = await ClientFor(handler).FetchAsync("https://t.example.com/", "wid", "the-jwt", "http://localhost:8080");

			Assert.Equal(new[] { "u1", "u2" }, snapshot.Users.Select(u => (string)u["id"]));
			Assert.Single(snapshot.Groups);
			HttpRequestMessage login = handler.Requests[0];
			Assert.Equal(HttpMethod.Post, login.Method);
			Assert.Equal("Bearer", login.Headers.Authorization.Scheme);
			Assert.Equal("the-jwt", login.Headers.Authorization.Parameter);
			Assert.Equal("wid", login.Headers.GetValues("qlik-web-integration-id").Single());
			Assert.Equal("http://localhost:8080", login.Headers.GetValues("Origin").Single());
		}

		[Fact]
		public async Task FetchAsync_RetriesOnTooManyRequests()
		{
			var handler = new FakeHandler();
			int usersCalls = 0;
			handler.Respond = r =>
			{
				string path = r.RequestUri.AbsolutePath;
				if (path == "/login/jwt-session") return Json("{}");
				if (path == "/api/v1/users/me") return Json(ME);
				if (path == "/api/v1/users" && usersCalls++ == 0) return Json("{}", (HttpStatusCode)429);
				return Json(@"{ ""data"": [ { ""id"": ""x"" } ] }");
			};

			TenantSnapshot snapshot = await ClientFor(handler).FetchAsync("t.example.com", "wid", "jwt", null);

			Assert.Equal(2, usersCalls);
			Assert.Single(snapshot.Users);
		}

		[Fact]
		public async Task FetchAsync_LoginRejected_Throws()
		{
			var handler = new FakeHandler { Respond = r => Json("nope", HttpStatusCode.Unauthorized) };

			var ex = await Assert.ThrowsAsync<QlikCloudException>(() => ClientFor(handler).FetchAsync("t.example.com", "wid", "jwt", null));

			Assert.Contains("401", ex.Message);
		}

		[Fact]
		public async Task FetchAsync_NonAdmin_Throws()
		{
			var handler = new FakeHandler();
			handler.Respond = r => r.RequestUri.AbsolutePath == "/api/v1/users/me"
				? Json(@"{ ""subject"": ""u"", ""assignedRoles"": [ { ""name"": ""Analyzer"" } ] }")
				: Json("{}");

			var ex = await Assert.ThrowsAsync<QlikCloudException>(() => ClientFor(handler).FetchAsync("t.example.com", "wid", "jwt", null));

			Assert.Contains("TenantAdmin", ex.Message);
		}

		[Fact]
		public async Task FetchAsync_ForeignPaginationLink_Throws()
		{
			var handler = new FakeHandler();
			handler.Respond = r =>
			{
				string path = r.RequestUri.AbsolutePath;
				if (path == "/api/v1/users")
					return Json(@"{ ""data"": [ { ""id"": ""u1"" } ], ""links"": { ""next"": { ""href"": ""https://evil.example.com/x"" } } }");
				return path == "/api/v1/users/me" ? Json(ME) : Json("{}");
			};

			await Assert.ThrowsAsync<QlikCloudException>(() => ClientFor(handler).FetchAsync("t.example.com", "wid", "jwt", null));
		}
	}
}
