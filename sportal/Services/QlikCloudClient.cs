using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace sportal.Services
{
	public class QlikCloudException : Exception
	{
		public QlikCloudException(string message) : base(message) { }
	}

	public class TenantSnapshot
	{
		public List<JObject> Users { get; } = new List<JObject>();

		public List<JObject> Groups { get; } = new List<JObject>();
	}

	// Server-side client that signs in as a TenantAdmin with a JWT (login/jwt-session) and
	// reads all active users and groups. A new HTTP session (cookie jar) is used for every fetch.
	public class QlikCloudClient
	{
		private const int PAGE_SIZE = 100;

		private const int MAX_PAGES = 1000;

		private const int MAX_RETRIES = 5;

		private readonly Func<HttpMessageHandler> _handlerFactory;

		// Replaceable so tests do not wait on real back-off delays.
		public Func<TimeSpan, Task> RetryDelay { get; set; } = Task.Delay;

		public QlikCloudClient() : this(() => new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() })
		{
		}

		public QlikCloudClient(Func<HttpMessageHandler> handlerFactory)
		{
			_handlerFactory = handlerFactory;
		}

		public async Task<TenantSnapshot> FetchAsync(string hostname, string webIntegrationID, string adminJwt, string origin)
		{
			string host = NormalizeHostname(hostname);
			using (var httpClient = new HttpClient(_handlerFactory(), false))
			{
				httpClient.Timeout = TimeSpan.FromSeconds(60);
				httpClient.DefaultRequestHeaders.Add("qlik-web-integration-id", webIntegrationID);
				if (!string.IsNullOrEmpty(origin))
				{
					httpClient.DefaultRequestHeaders.Add("Origin", origin);
				}

				await LoginAsync(httpClient, host, webIntegrationID, adminJwt);
				await ValidateAdminAsync(httpClient, host);

				var snapshot = new TenantSnapshot();
				string usersUrl = "https://" + host + "/api/v1/users?limit=" + PAGE_SIZE + "&filter=" + Uri.EscapeDataString("status eq \"active\"");
				snapshot.Users.AddRange(await GetAllPagesAsync(httpClient, host, usersUrl));
				string groupsUrl = "https://" + host + "/api/v1/groups?limit=" + PAGE_SIZE;
				snapshot.Groups.AddRange(await GetAllPagesAsync(httpClient, host, groupsUrl));
				return snapshot;
			}
		}

		public static string NormalizeHostname(string hostname)
		{
			if (string.IsNullOrWhiteSpace(hostname))
			{
				throw new QlikCloudException("Hostname is not configured.");
			}
			string host = hostname.Trim();
			if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				host = host.Substring("https://".Length);
			}
			else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
			{
				host = host.Substring("http://".Length);
			}
			return host.TrimEnd('/');
		}

		private async Task LoginAsync(HttpClient httpClient, string host, string webIntegrationID, string adminJwt)
		{
			string url = "https://" + host + "/login/jwt-session?qlik-web-integration-id=" + Uri.EscapeDataString(webIntegrationID);
			using (HttpResponseMessage response = await SendAsync(httpClient, () =>
			{
				var request = new HttpRequestMessage(HttpMethod.Post, url);
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminJwt);
				request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
				return request;
			}))
			{
				if (!response.IsSuccessStatusCode)
				{
					throw new QlikCloudException("TenantAdmin JWT login failed (HTTP " + (int)response.StatusCode + "). Check the hostname, web integration ID (and its allowed origins), JWT issuer/key ID and the TenantAdmin subject. " + await ReadSnippetAsync(response));
				}
			}
		}

		// Guards against a wrong admin subject: an unknown subject can auto-provision a new, non-admin user.
		private async Task ValidateAdminAsync(HttpClient httpClient, string host)
		{
			string url = "https://" + host + "/api/v1/users/me";
			using (HttpResponseMessage response = await SendAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Get, url)))
			{
				if (!response.IsSuccessStatusCode)
				{
					throw new QlikCloudException("Could not read the TenantAdmin user (HTTP " + (int)response.StatusCode + "). " + await ReadSnippetAsync(response));
				}
				JObject me = JObject.Parse(await response.Content.ReadAsStringAsync());
				if (me["assignedRoles"] is JArray roles && !roles.Any(r => string.Equals((string)r["name"], "TenantAdmin", StringComparison.OrdinalIgnoreCase)))
				{
					throw new QlikCloudException("User '" + (string)me["subject"] + "' does not have the TenantAdmin role. Check the TenantAdmin subject in Settings.");
				}
			}
		}

		private async Task<List<JObject>> GetAllPagesAsync(HttpClient httpClient, string host, string firstUrl)
		{
			var items = new List<JObject>();
			string url = firstUrl;
			for (int page = 0; url != null && page < MAX_PAGES; page++)
			{
				string pageUrl = url;
				using (HttpResponseMessage response = await SendAsync(httpClient, () => new HttpRequestMessage(HttpMethod.Get, pageUrl)))
				{
					if (!response.IsSuccessStatusCode)
					{
						throw new QlikCloudException("Request to '" + new Uri(pageUrl).AbsolutePath + "' failed (HTTP " + (int)response.StatusCode + "). " + await ReadSnippetAsync(response));
					}
					JObject body = JObject.Parse(await response.Content.ReadAsStringAsync());
					JArray data = body["data"] as JArray;
					if (data == null || data.Count == 0)
					{
						break;
					}
					items.AddRange(data.OfType<JObject>());

					url = NextUrl(body, host);
				}
			}
			return items;
		}

		private static string NextUrl(JObject body, string host)
		{
			string next = (string)body.SelectToken("links.next.href");
			if (string.IsNullOrEmpty(next))
			{
				return null;
			}
			// Never send the session cookie anywhere but the tenant.
			if (!Uri.TryCreate(next, UriKind.Absolute, out Uri nextUri) || !string.Equals(nextUri.Host, host, StringComparison.OrdinalIgnoreCase) || nextUri.Scheme != Uri.UriSchemeHttps)
			{
				throw new QlikCloudException("Unexpected pagination link returned by the tenant.");
			}
			return next;
		}

		// Sends a freshly built request, retrying on HTTP 429 (honouring Retry-After).
		private async Task<HttpResponseMessage> SendAsync(HttpClient httpClient, Func<HttpRequestMessage> requestFactory)
		{
			for (int attempt = 0; ; attempt++)
			{
				HttpResponseMessage response;
				using (HttpRequestMessage request = requestFactory())
				{
					response = await httpClient.SendAsync(request);
				}
				if ((int)response.StatusCode != 429 || attempt >= MAX_RETRIES)
				{
					return response;
				}
				TimeSpan delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
				response.Dispose();
				await RetryDelay(delay);
			}
		}

		private static async Task<string> ReadSnippetAsync(HttpResponseMessage response)
		{
			try
			{
				string text = await response.Content.ReadAsStringAsync();
				return text.Length > 200 ? text.Substring(0, 200) : text;
			}
			catch
			{
				return "";
			}
		}
	}
}
