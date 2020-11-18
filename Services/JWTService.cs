using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using sportal.Data;

namespace sportal.Services
{
	public class JWTService
	{
		private TenantData _tenantData;

		private UserService _userService;

		public JWTService(UserService userService)
		{
			_userService = userService;

			//try to load tenant data file
			if (File.Exists("tenantData.tdf"))
			{
				//userString = File.ReadAllText("users.json");

			}
			else
			{
				_tenantData = new TenantData();
				_tenantData.WebIntegrationID = "vXkk89R3iLz74ft1LJo-L5tV9F7gDMc1";
				_tenantData.Hostname = "jesseparis.us.qlikcloud.com";
			}

		}

		private static X509Certificate2 buildSelfSignedServerCertificate()
		{
			string CertificateName = "test";
			SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
			//sanBuilder.AddIpAddress("qlikcloud");
			//sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
			sanBuilder.AddDnsName("localhost");
			sanBuilder.AddDnsName(Environment.MachineName);

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

				X509Certificate2 certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
				certificate.FriendlyName = CertificateName;

				return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "WeNeedASaf3rPassword"), "WeNeedASaf3rPassword", X509KeyStorageFlags.MachineKeySet);
			}
		}

		public string GenerateToken(User user)
		{
			//private key temp
			SecureString pass = new SecureString();
			string password = "1q2w3e4r";
			for (int i = 0; i < password.Length; i++)
			{
				pass.AppendChar(password[i]);
			}
			X509Certificate2 cert = new X509Certificate2("/Users/eps/Documents/Qlik_Projects/QCS_SaaS_JWT_Prototype/certs/cert.pfx", pass);

			//byte[] key = File.ReadAllBytes("/Users/eps/Documents/Qlik_Projects/QCS_SaaS_JWT_Prototype/certs/key.pem");



			var mySecret = "e0b264af-d18e-4ff6-abe8-86b7febdff3b";


			var tokenHandler = new JwtSecurityTokenHandler();

			//System.Security.Cryptography.RSA rsa = new
			RsaSecurityKey securityKey = new RsaSecurityKey(RSACertificateExtensions.GetRSAPrivateKey(cert));
			securityKey.KeyId = mySecret;
			var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

			var tokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = new ClaimsIdentity(new Claim[]
				{
					new Claim("sub", user.Subject),
					new Claim("subType", "user"),
					new Claim("name", user.DisplayName),
					new Claim("email", user.Email),
					new Claim("email_verified", "true", ClaimValueTypes.Boolean),
					new Claim("groups", "IT"),
					new Claim("groups", "Marketing")
				}),
				Expires = DateTime.UtcNow.AddHours(3),
				Issuer = _tenantData.Hostname,
				Audience = "qlik.api/login/jwt-session",
				SigningCredentials = signingCredentials
			};

			var token = tokenHandler.CreateToken(tokenDescriptor);
			string jwtToken = tokenHandler.WriteToken(token);
			Console.WriteLine(jwtToken);
			return jwtToken;
		}

		private string GenerateJWTToken(User user)
		{

			return "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6ImUwYjI2NGFmLWQxOGUtNGZmNi1hYmU4LTg2YjdmZWJkZmYzYiJ9.eyJzdWIiOiJTYWFTUC1lNzk3ZDA5NC03MTZhLTQzNmQtYTU2Yi1lNjcxNDRjNWZiYjQiLCJzdWJUeXBlIjoidXNlciIsIm5hbWUiOiJFbWlseSBUaWVybmVuIiwiZW1haWwiOiJlbWlseS50aWVybmVuQHNhYXNwZXJzb25hcy5kZXYiLCJlbWFpbF92ZXJpZmllZCI6dHJ1ZSwiZ3JvdXBzIjpbXSwiaWF0IjoxNjA1NjQwNzc1LCJleHAiOjE2MDU2NTE1NzUsImF1ZCI6InFsaWsuYXBpL2xvZ2luL2p3dC1zZXNzaW9uIiwiaXNzIjoiamVzc2VwYXJpcy51cy5xbGlrY2xvdWQuY29tIn0.TLi08ze_WOl2EP3PKaxofD_nkeWuoz9rcGCGIFcADOKQwaolnm0z0gYeKBtJb-a1MfgcubPNCZJZIn9yGhGzr8anPifLhHKI9xILS9ZHOjDB3WwRyHjbTKmnBanNpGYzEe93L2LsLXSm77T9lMJvTQ9VZPE_Z7JtRpJRXtWow0x1kKPrUtbBHQpIhVc7i8rpyREj5haltb9plxFg-h5mRbj3zBsaRZsLxdyyJdkXtvGsLQr-POhdW9Qm1W4qkOblqYe7NRLCTa0Uc8ZbGZ4gOfWIRrJesYnOErj-b1pHEU1cE8OPuVDAzKf0aIXOh49G-9PgfbyNyc9sBEPMdZCN5A";
		}

		public async Task<LoginObject> GetLoginObject(User u)
		{
			Console.WriteLine(_userService);
			Task<LoginObject> loginObjectTask = new Task<LoginObject>(() =>
			{
				LoginObject loginObject = new LoginObject() { Hostname = _tenantData.Hostname, WebIntegrationID = _tenantData.WebIntegrationID };
				loginObject.JWTToken = GenerateToken(u);
				return loginObject;
			});
			loginObjectTask.Start();

			return await loginObjectTask;

		}
	}
}
