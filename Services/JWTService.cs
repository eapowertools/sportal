﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Formatters.Binary;
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
				Stream openFileStream = File.OpenRead("tenantData.tdf");
				BinaryFormatter deserializer = new BinaryFormatter();
				_tenantData = (TenantData)deserializer.Deserialize(openFileStream);
				openFileStream.Close();
			}
			else
			{
				_tenantData = new TenantData();
				_tenantData.WebIntegrationID = "";
				_tenantData.Hostname = "";
				_tenantData.Certificate = GenerateCertificate();

				SaveTenantData();
			}

		}

		private void SaveTenantData()
		{
			Stream SaveFileStream = File.Create("tenantData.tdf");
			BinaryFormatter serializer = new BinaryFormatter();
			serializer.Serialize(SaveFileStream, _tenantData);
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

				X509Certificate2 certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
				certificate.FriendlyName = CertificateName;

				return certificate;
			}
		}

		public string GenerateToken(User user)
		{
			//private key temp
			SecureString pass = new SecureString();
			string password = "";
			for (int i = 0; i < password.Length; i++)
			{
				pass.AppendChar(password[i]);
			}
			X509Certificate2 cert = new X509Certificate2("/Users/eps/Documents/Qlik_Projects/QCS_SaaS_JWT_Prototype/certs/cert.pfx", pass);


			var mySecret = "";


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
