using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using sportal.Data;

namespace sportal.Services
{
	public class SyncResult
	{
		public bool Success { get; set; }

		public bool Skipped { get; set; }

		public string Message { get; set; }

		public int UserCount { get; set; }

		public DateTime? CompletedUtc { get; set; }

		public bool Failed
		{
			get { return !Success && !Skipped; }
		}
	}

	// Orchestrates Impersonate / Sync mode: signs in as the TenantAdmin, reads users and groups,
	// and hands the result to UserService (and, in Sync mode, to users.json).
	public class TenantSyncService
	{
		private const string USERS_FILE = "users.json";

		private readonly JWTService _jwtService;

		private readonly QlikCloudClient _client;

		private readonly UserService _userService;

		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		private bool _initialized = false;

		public SyncResult LastResult { get; private set; } = new SyncResult { Skipped = true, Message = "" };

		public TenantSyncService(JWTService jwtService, QlikCloudClient client, UserService userService)
		{
			_jwtService = jwtService;
			_client = client;
			_userService = userService;
		}

		// Runs the configured mode once per process; later callers get the previous result.
		public async Task<SyncResult> EnsureInitializedAsync()
		{
			await _gate.WaitAsync();
			try
			{
				if (!_initialized)
				{
					_initialized = true;
					// users.json was already loaded at startup, so only the tenant modes have work to do.
					if (_jwtService.GetEffectiveMode().UsesTenant())
					{
						LastResult = await RunAsync();
					}
				}
				return LastResult;
			}
			finally
			{
				_gate.Release();
			}
		}

		// Re-runs the configured mode (a fresh fetch, or a re-sync) or, in Local mode, re-reads users.json.
		public async Task<SyncResult> RefreshAsync()
		{
			await _gate.WaitAsync();
			try
			{
				_initialized = true;
				LastResult = await RunAsync();
				return LastResult;
			}
			finally
			{
				_gate.Release();
			}
		}

		// Replaces users.json with the stock sample users (the old file is kept as users.json.bak).
		// Only Local mode shows the file's users, so only then are they loaded right away.
		public async Task<SyncResult> CreateStockUsersFileAsync()
		{
			await _gate.WaitAsync();
			try
			{
				string stock = UserService.GetStockUsersJson();
				if (!_jwtService.GetEffectiveMode().UsesTenant())
				{
					_userService.LoadFromJson(stock);
				}
				WriteUsersFile(JArray.Parse(stock));
				return new SyncResult { Success = true, Message = "Created a new stock " + USERS_FILE + ".", CompletedUtc = DateTime.UtcNow };
			}
			catch (Exception e)
			{
				Console.WriteLine("Creating stock " + USERS_FILE + " failed: " + e);
				return Fail("Could not create " + USERS_FILE + ": " + e.Message);
			}
			finally
			{
				_gate.Release();
			}
		}

		private async Task<SyncResult> RunAsync()
		{
			SportalMode mode = _jwtService.GetEffectiveMode();
			if (!mode.UsesTenant())
			{
				try
				{
					_userService.RefreshUsers();
				}
				catch (Exception e)
				{
					return Fail("Could not load users.json: " + e.Message);
				}
				return new SyncResult { Skipped = true, Success = true, Message = "" };
			}

			try
			{
				TenantData tenantData = await _jwtService.GetTenantDataAsync();
				ValidateConfiguration(tenantData);

				string adminJwt = _jwtService.GenerateAdminToken();
				TenantSnapshot snapshot = await _client.FetchAsync(tenantData.Hostname, tenantData.WebIntegrationID, adminJwt, GetOrigin());
				JArray tenantEntries = TenantUserMapper.ToUserEntries(snapshot.Users, snapshot.Groups);

				JArray result = tenantEntries;
				if (mode == SportalMode.Sync)
				{
					result = Merge(tenantEntries);
				}

				_userService.LoadFromJson(result.ToString(Formatting.None));
				if (mode == SportalMode.Sync)
				{
					WriteUsersFile(result);
				}
				string message = mode == SportalMode.Sync
					? "Synced " + tenantEntries.Count + " tenant users into " + USERS_FILE + "."
					: "Loaded " + tenantEntries.Count + " users from the tenant.";
				return new SyncResult { Success = true, UserCount = tenantEntries.Count, Message = message, CompletedUtc = DateTime.UtcNow };
			}
			catch (Exception e)
			{
				Console.WriteLine("Tenant " + mode + " failed: " + e);
				return Fail("Tenant " + mode.ToString().ToLower() + " failed: " + e.Message);
			}
		}

		private static SyncResult Fail(string message)
		{
			return new SyncResult { Success = false, Message = message };
		}

		private static void ValidateConfiguration(TenantData t)
		{
			if (string.IsNullOrWhiteSpace(t.Hostname) || string.IsNullOrWhiteSpace(t.WebIntegrationID) || string.IsNullOrWhiteSpace(t.Issuer) || string.IsNullOrWhiteSpace(t.KeyID))
			{
				throw new QlikCloudException("Hostname, Web Integration ID, JWT Issuer and JWT Key ID must be configured.");
			}
			if (string.IsNullOrWhiteSpace(t.AdminSubject) || string.IsNullOrWhiteSpace(t.AdminName) || string.IsNullOrWhiteSpace(t.AdminEmail))
			{
				throw new QlikCloudException("TenantAdmin subject, name and email must be configured.");
			}
		}

		// The origin Sportal is served from; it has to be allowed in the tenant's web integration.
		private static string GetOrigin()
		{
			return (GlobalSettings.IS_HTTPS ? "https" : "http") + "://localhost:" + GlobalSettings.PORT;
		}

		// Merges tenant entries into the entries of users.json (keeping local fields).
		private static JArray Merge(JArray tenantEntries)
		{
			string path = Path.Combine(SportalFolder.WorkingDirectory, USERS_FILE);
			JArray existing = File.Exists(path) ? JArray.Parse(File.ReadAllText(path)) : new JArray();
			return TenantUserMapper.Merge(existing, tenantEntries);
		}

		// Only called once the merged users loaded successfully; backs up the old file first.
		private static void WriteUsersFile(JArray merged)
		{
			string path = Path.Combine(SportalFolder.WorkingDirectory, USERS_FILE);
			string temp = path + ".tmp";
			File.WriteAllText(temp, merged.ToString(Formatting.Indented));
			if (File.Exists(path))
			{
				File.Copy(path, path + ".bak", true);
			}
			File.Move(temp, path, true);
		}
	}
}
