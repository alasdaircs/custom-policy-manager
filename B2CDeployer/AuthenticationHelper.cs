using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Identity.Client;

namespace B2CPolicyManager
{
	public class AuthenticationHelper
	{
		static readonly string[] Scopes = ["User.Read"];

		IPublicClientApplication _app { get; }


		public AuthenticationHelper( string TenantId, Guid AppId )
		{
			var authority = $"https://login.microsoftonline.com/{TenantId}/v2.0";
			_app = PublicClientApplicationBuilder.Create( AppId.ToString( "D" ) )
				.WithAuthority( authority )
				// .WithDefaultRedirectUri()
				.WithRedirectUri( "http://localhost" )
				.Build();
			TokenCacheHelper.EnableSerialization( _app.UserTokenCache );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>Token for user.</returns>
		public async Task ClearCacheAsync()
		{
			var accounts = (await _app.GetAccountsAsync()).ToList();
			foreach( var account in accounts )
			{
				await _app.RemoveAsync( account );
			}
		}

		public async Task<string> GetTokenForUserAsync( CancellationToken cancellation = default )
		{
			AuthenticationResult authResult;

			var accounts = (await _app.GetAccountsAsync()).ToList();
			string tokenForUser = null;
			try
			{
				authResult = await _app.AcquireTokenSilent( Scopes, accounts.FirstOrDefault() )
					.ExecuteAsync( cancellation )
					.ConfigureAwait( false );

				tokenForUser = authResult.AccessToken;
			}

			catch( MsalUiRequiredException )
			{
				try
				{
					authResult = await _app.AcquireTokenInteractive( Scopes )
						.WithAccount( accounts.FirstOrDefault() )
						.WithPrompt( Prompt.NoPrompt )
						.ExecuteAsync( cancellation )
						.ConfigureAwait( false );

					tokenForUser = authResult.AccessToken;
				}
				catch
				{
					return tokenForUser;
				}
				//}
			}

			return tokenForUser;
		}

		public async Task AddHeadersAsync( HttpRequestMessage requestMessage )
		{
			string tokenForUser = await GetTokenForUserAsync();
			try
			{
				requestMessage.Headers.Authorization = new AuthenticationHeaderValue( "bearer", tokenForUser );
			}
			catch( Exception ex )
			{
				Debug.WriteLine( "Could not add headers to HttpRequestMessage: " + ex.Message );
			}
		}

	}
}