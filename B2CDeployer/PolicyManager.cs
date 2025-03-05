using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

using B2CPolicyManager.Models;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace B2CPolicyManager
{
	public class PolicyManager
	{
		public AuthenticationHelper AuthenticationHelper { get; }

		public PolicyManager( AuthenticationHelper AuthenticationHelper )
		{
			this.AuthenticationHelper = AuthenticationHelper;
		}

		HttpHelper HttpHelper 
			=> new HttpHelper( AuthenticationHelper );

		public async Task Deploy( ILogger Logger, IEnumerable<String> PolicyFileNames )
		{
			HttpResponseMessage response;

			Logger.LogInformation( "Deploying B2C resources..." );

			string token = await AuthenticationHelper.GetTokenForUserAsync();
			if( token != null )
			{

				var unorderedPolicies = new List<Policy>();
				var orderedPolicies = new List<Policy>();

				foreach( string fileName in PolicyFileNames )
				{
					string xml = File.ReadAllText(fileName);

					// Get policy id and base policy id
					XDocument policyFile = XDocument.Parse( xml );
					var qualify = ( String name ) => $"{{{policyFile.Root.GetDefaultNamespace()}}}{name}";
					unorderedPolicies.Add(
						new Policy
						{
							Text = xml,
							Id = policyFile.Root.Attribute( "PolicyId" ).Value,
							Base = policyFile.Root.Element( qualify( "BasePolicy" ) )?.Element( qualify( "PolicyId" ) )?.Value
						}
					);
				}

				while( unorderedPolicies.Count != 0 )
				{
					foreach( var policy in unorderedPolicies.ToArray().Where( p => !unorderedPolicies.Exists( pBase => pBase.Id.Equals( p.Base, StringComparison.OrdinalIgnoreCase ) ) ) )
					{
						orderedPolicies.Add( policy );
						unorderedPolicies.Remove( policy );
					}
				}

				foreach( var policy in orderedPolicies )
				{
					response = await HttpHelper.HttpPutIDAsync( Constants.TrustFrameworkPolicyByIDUriPUT, policy.Id, policy.Text );
					if( response.IsSuccessStatusCode == false )
					{
						string errContent = await response.Content.ReadAsStringAsync();
						uploadPolicyResponseError errorMsg = JsonConvert.DeserializeObject<uploadPolicyResponseError>(errContent);
						Logger.LogError( "{0}; CorrelationId: {1}", errorMsg.error.message, errorMsg.error.innerError.correlationId );
					}
					else
					{
						Logger.LogInformation( "Successfully updated policy {0}", policy.Id );
					}
				}

			}

			Logger.LogInformation( "B2C resources deployed." );

		}

		public async Task<List<String>> GetPoliciesAsync( ILogger Logger )
		{
			List<String> result;
			HttpResponseMessage response;

			response = await HttpHelper.HttpGetAsync( Constants.TrustFrameworkPolicesUri );
			string content = await response.Content.ReadAsStringAsync();

			if( response.IsSuccessStatusCode == true )
			{
				result = JsonConvert.DeserializeObject<PolicyList>( content )
					.Value
					.Select( v => v.Id )
					.ToList()
				;

				Logger.LogInformation( "Successfully fetched policy list." );
			}
			else
			{
				result = new();
				Logger.LogError( "Failed to fetch policy list. {0}", content );
			}

			return result;
		}

		public async Task<List<App>> GetAppRegistrationsAsync( ILogger Logger )
		{
			List<App> result;
			HttpResponseMessage response;
			Logger.LogInformation( "Getting AAD B2C app registrations." );

			response = await HttpHelper.HttpGetAsync( "https://graph.microsoft.com/beta/applications" );
			string content = await response.Content.ReadAsStringAsync();

			if( response.IsSuccessStatusCode == true )
			{
				result = JsonConvert.DeserializeObject<AppList>( content ).value
					.Where( app => app.signInAudience == "AzureADandPersonalMicrosoftAccount" )
					.OrderBy( app => app.displayName )
					.ToList()
				;

			}
			else
			{
				result = new();
				Logger.LogError( "Failed to fetch app registrations." );
			}

			return result;
		}

		public async Task DeletePolicyAsync( ILogger Logger, String PolicyName )
		{
			HttpResponseMessage response;
			
			Logger.LogInformation( "Deleting policy {0}", PolicyName );
			response = await HttpHelper.HttpDeleteIDAsync( Constants.TrustFrameworkPolicesUri, Constants.TrustFrameworkPolicyByIDUri, PolicyName.ToUpper() );
			string content = await response.Content.ReadAsStringAsync();

			if( response.IsSuccessStatusCode == true )
			{
				Logger.LogInformation( "Successfully deleted policy {0}", PolicyName );
			}
			else
			{
				Logger.LogInformation( "Failed to delete policy {0}: {1}", PolicyName, content );
			}
		}

	}
}
