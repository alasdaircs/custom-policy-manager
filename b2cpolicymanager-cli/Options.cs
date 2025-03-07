using System;
using System.Diagnostics;

using CommandLine;

using Microsoft.Extensions.Logging;

namespace b2cpolicymanager_cli
{
    internal class OptionsBase
    {
		[Option( 't', "tenant", Required = true, HelpText = "The tenant id - either domain name or guid of the tenant" )]
		public String TenantId { get; set; } = "";

		[Option( 'a', "appid", Required = true, HelpText = "The application id - GUID of App Registration in Entra ID" )]
		public Guid AppId { get; set; }

		[Option( 's', "appsecret", Required = true, HelpText = "The application secret from the App Registration in Entra ID - DO NOT STORE THIS" )]
		public String AppSecret { get; set; } = "";

		[Verb( "list", HelpText = "List the policies in the tenant" )]
		public class ListOptions :OptionsBase
		{
		}

		public class FolderOptions :OptionsBase
		{
			[Option( 'f', "folder", Default = "", Required = false, HelpText = "The path to the folder for the policy files. Relative or absolute." )]
			public String Folder { get; set; } = "";
		}

		[Verb( "get", HelpText = "Get the policies from the tenant" )]
		public class GetOptions :FolderOptions
		{
			[Option( 'p', "policies", Required = false, HelpText = "The list of policy names to get - omit to get all" )]
			public IEnumerable<String> PolicyNames { get; set; } = [];
		}

		[Verb( "deploy", HelpText = "Deploy the policies to the tenant" )]
		public class DeployOptions :FolderOptions
		{
			[Option( 'p', "policies", Required = true, HelpText = "The list of policy names to deploy. The files must be in the --folder and be named <PolicyName>.xml Wildcards are allowed." )]
			public IEnumerable<String> PolicyNames { get; set; } = [];
		}

	}
}
