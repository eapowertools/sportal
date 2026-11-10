using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using sportal.Data;

namespace sportal.Services
{
	public class JWTService
	{
		private TenantData _tenantData;

		private X509Certificate2 _certificate;

		public JWTService(UserService userService)
		{
			Console.WriteLine("Looking for 'tenantData.tdf' file in: " + SportalFolder.WorkingDirectory);
			//try to load tenant data file
			if (File.Exists(Path.Combine(SportalFolder.WorkingDirectory, "tenantData.tdf")))
			{
				Stream openFileStream = File.OpenRead(Path.Combine(SportalFolder.WorkingDirectory, "tenantData.tdf"));
				_tenantData = (TenantData)JsonSerializer.Deserialize(openFileStream, typeof(TenantData));
				openFileStream.Close();
				_certificate = new X509Certificate2(_tenantData.Certificate, (string)null, X509KeyStorageFlags.Exportable);
			}
			else
			{
				_tenantData = new TenantData();
				_tenantData.WebIntegrationID = "";
				_tenantData.Hostname = "";
				_certificate = GenerateCertificate();
				_tenantData.Certificate = _certificate.Export(X509ContentType.Pkcs12);

				SaveTenantData();
			}
		}

		private void SaveTenantData()
		{
			Stream SaveFileStream = File.Create(Path.Combine(SportalFolder.WorkingDirectory, "tenantData.tdf"));
			JsonSerializer.Serialize(SaveFileStream, _tenantData);
			SaveFileStream.Close();
		}

		private X509Certificate2 GenerateCertificate()
		{
			string CertificateName = "sportal.qlikcloud.com";
			SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
			sanBuilder.AddDnsName("localhost");
			sanBuilder.AddDnsName("qlikcloud.com");

			X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={CertificateName}");

			using (RSA rsa = RSA.Create(2048))
			{
				var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

				request.CertificateExtensions.Add(
					new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));


				request.CertificateExtensions.Add(
				   new X509EnhancedKeyUsageExtension(
					   new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

				request.CertificateExtensions.Add(sanBuilder.Build());

				X509Certificate2 certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(825)));

				return certificate;
			}
		}

		public Task<string> GetPublicKey()
		{
			var certBytes = _certificate.GetRawCertData();
			return Task.FromResult(Convert.ToBase64String(certBytes));
		}

		public string GenerateToken(User user)
		{
			return BuildToken(user.Subject, user.DisplayName, user.Email, user.HasImageURL() ? user.ImageURL : null, user.Groups);
		}

		// Token for the configured TenantAdmin (Impersonate and Sync modes). It carries no 'groups' claim
		// so the admin's group membership in the tenant is left alone.
		public string GenerateAdminToken()
		{
			return BuildToken(_tenantData.AdminSubject, _tenantData.AdminName, _tenantData.AdminEmail, null, new string[0]);
		}

		// The --mode command line option wins over the mode saved in the settings dialog.
		public SportalMode GetEffectiveMode()
		{
			return GlobalSettings.MODE_OVERRIDE ?? _tenantData.Mode;
		}

		private string BuildToken(string subject, string displayName, string email, string imageURL, string[] groups)
		{
			var tokenHandler = new JwtSecurityTokenHandler();
			RsaSecurityKey securityKey = new RsaSecurityKey(RSACertificateExtensions.GetRSAPrivateKey(_certificate));
			securityKey.KeyId = _tenantData.KeyID;
			var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

			var tokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = new ClaimsIdentity(new Claim[]
				{
					new Claim("jti", Guid.NewGuid().ToString()),
					new Claim("sub", subject),
					new Claim("subType", "user"),
					new Claim("name", displayName),
					new Claim("email", email),
					new Claim("email_verified", "true", ClaimValueTypes.Boolean)
				}),
				Expires = DateTime.UtcNow.AddMinutes(3),
				Issuer = _tenantData.Issuer,
				Audience = "qlik.api/login/jwt-session",
				NotBefore = DateTime.UtcNow.AddMinutes(-3),
				SigningCredentials = signingCredentials
			};
			if (!string.IsNullOrEmpty(imageURL))
			{
				tokenDescriptor.Subject.AddClaim(new Claim("picture", imageURL));
			}
			for (int i = 0; i < groups.Length; i++)
			{
				tokenDescriptor.Subject.AddClaim(new Claim("groups", groups[i]));
			}

			var token = tokenHandler.CreateToken(tokenDescriptor);
			string jwtToken = tokenHandler.WriteToken(token);
			return jwtToken;
		}

		public async Task<LoginObject> GetLoginObjectAsync(User u)
		{
			Task<LoginObject> loginObjectTask = new Task<LoginObject>(() =>
			{
				LoginObject loginObject = new LoginObject() { Hostname = _tenantData.Hostname, WebIntegrationID = _tenantData.WebIntegrationID };
				loginObject.JWTToken = GenerateToken(u);
				return loginObject;
			});
			loginObjectTask.Start();

			return await loginObjectTask;
		}

		public Task<TenantData> GetTenantDataAsync()
		{
			return Task.FromResult(_tenantData);
		}

		public void SaveUpdatedTenantData(string hostname, string webIntegrationID, string jwtIssuer, string jwtKeyID)
		{
			SaveUpdatedTenantData(hostname, webIntegrationID, jwtIssuer, jwtKeyID, _tenantData.Mode, _tenantData.AdminSubject, _tenantData.AdminName, _tenantData.AdminEmail);
		}

		public void SaveUpdatedTenantData(string hostname, string webIntegrationID, string jwtIssuer, string jwtKeyID, SportalMode mode, string adminSubject, string adminName, string adminEmail)
		{
			Task<TenantData> saveTenantDataTask = new Task<TenantData>(() =>
			{
				_tenantData.Hostname = hostname;
				_tenantData.WebIntegrationID = webIntegrationID;
				_tenantData.Issuer = jwtIssuer;
				_tenantData.KeyID = jwtKeyID;
				_tenantData.Mode = mode;
				_tenantData.AdminSubject = adminSubject;
				_tenantData.AdminName = adminName;
				_tenantData.AdminEmail = adminEmail;
				SaveTenantData();
				return _tenantData;
			});
			saveTenantDataTask.Start();

			return;
		}

		public Stream GetCertificateFilestream()
		{
			byte[] certificateByteArray = _certificate.Export(X509ContentType.Pfx);
			var fileStream = new MemoryStream(certificateByteArray);
			return fileStream;
		}
	}
}
