using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace B2CPolicyManager
{
	class HttpHelper
	{
		public AuthenticationHelper AuthenticationHelper { get; }

		public HttpHelper( AuthenticationHelper AuthenticationHelper )
		{
			this.AuthenticationHelper = AuthenticationHelper;
		}	

		public async Task<HttpResponseMessage> HttpGetAsync( string uri )
		{
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
			await AuthenticationHelper.AddHeadersAsync( request );
			HttpClient httpClient = new HttpClient();
			Task<HttpResponseMessage> response = httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			return await response;
		}

		public async Task<HttpResponseMessage> HttpPutIDAsync( string uri, string id, string xml )
		{
			string uriWithID = String.Format(uri, id);
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uriWithID);
			await AuthenticationHelper.AddHeadersAsync( request );
			request.Content = new StringContent( xml, Encoding.UTF8, "application/xml" );
			HttpClient httpClient = new HttpClient();
			Task<HttpResponseMessage> response = httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			return await response;
		}

		public async Task<HttpResponseMessage> HttpDeleteIDAsync( string policyUri, string policyByIdURI, string id )
		{
			string uriWithID = String.Format(policyByIdURI, id);
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, uriWithID);
			await AuthenticationHelper.AddHeadersAsync( request );
			HttpClient httpClient = new HttpClient();
			Task<HttpResponseMessage> response = httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			return await response;
		}

	}
}
