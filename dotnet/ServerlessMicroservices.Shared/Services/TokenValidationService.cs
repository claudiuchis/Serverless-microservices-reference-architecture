﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace ServerlessMicroservices.Shared.Services
{
    /*
     * This is a GWT token validator. It is used by the B2cValidationAttribute filter to handle security cross-cutting concerns. 
     * 
     * Because filters seem to be not fully baked in Azure Functions yet, I opted to use an APIM policy to verify the GWT token prior 
     * to hitting the API.
     */
    public class TokenValidationService : ITokenValidationService
    {
        const string AuthorizationHeaderName = "authorization";
        const string BearerScheme = "bearer";
        const string ScopeClainType = "scp";
        const string SigningKeyUseType = "sig";
        const string NameClaimType = "displayName";
        const string RoleClaimType = "role";

        private ISettingService _settingService;
        private ILoggerService _loggerService;
        private DiscoveryCache _discoveryCache;
        private string _audience;
        private string _scope;

        /// <summary>
        /// Configured by the function app settings. If false, validation is skipped.
        /// </summary>
        /// <returns></returns>
        public bool AuthEnabled { get; }

        public TokenValidationService(ISettingService settingService, ILoggerService loggerService)
        {
            _settingService = settingService;
            _loggerService = loggerService;

            _discoveryCache = new DiscoveryCache(settingService.GetAuthorityUrl(), policy: new DiscoveryPolicy
            {
                ValidateIssuerName = false,
                ValidateEndpoints = false
            });

            _audience = settingService.GetApiApplicationId();
            _scope = settingService.GetApiScopeName();
            AuthEnabled = settingService.EnableAuth();
        }

        public async Task<ClaimsPrincipal> AuthenticateRequest(HttpRequest request)
        {
            var token = ExtractBearerToken(request);
            if (token != null)
            {
                var principal = await ValidateJwt(token);
                return principal;
            }

            return null;
        }

        string ExtractBearerToken(HttpRequest request)
        {
            _loggerService.Log("Entering ExtractBearerToken");
            if (request.Headers.TryGetValue(AuthorizationHeaderName, out var authorization))
            {
                var header = authorization.FirstOrDefault()?.ToString();
                _loggerService.Log(header);
                if (header != null && header.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase))
                {
                    var token = header.Substring(BearerScheme.Length).Trim();
                    return token;
                }
            }

            return null;
        }

        async Task<ClaimsPrincipal> ValidateJwt(string token)
        {
            _loggerService.Log("Entering ValidateJwt");
            var validationParams = await GetValidationParameters();
            if (validationParams != null)
            {
                var handler = new JwtSecurityTokenHandler();
                handler.InboundClaimTypeMap.Clear();

                try
                {
                    var principal = handler.ValidateToken(token, validationParams, out _);
                    foreach (Claim claim in principal.Claims)
                    {
                        _loggerService.Log(claim.ToString());
                    }
                    if (principal.HasClaim(ScopeClainType, _scope))
                    {
                        return principal;
                    }
                }
                catch (Exception ex)
                {
                    _loggerService.Log(ex);
                }
            }

            _loggerService.Log("ValidateJwt - return null");
            return null;
        }

        async Task<TokenValidationParameters> GetValidationParameters()
        {
            var disco = await _discoveryCache.GetAsync();
            if (disco.IsError)
            {
                //_loggerService.LogError("Discovery error {0}", disco.Error);
                return null;
            }

            var keys = disco.KeySet.Keys
               .Where(x => x.Use == SigningKeyUseType)
               .Select(x =>
               {
                   return new RsaSecurityKey(new System.Security.Cryptography.RSAParameters
                   {
                       Exponent = Base64Url.Decode(x.E),
                       Modulus = Base64Url.Decode(x.N)
                   })
                   {
                       KeyId = x.Kid
                   };
               });

            return new TokenValidationParameters
            {
                ValidIssuer = disco.Issuer,
                ValidAudience = _audience,
                IssuerSigningKeys = keys,
                NameClaimType = NameClaimType,
                RoleClaimType = RoleClaimType
            };
        }
    }
}
