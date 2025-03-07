using B2CPolicyManager;

using b2cpolicymanager_cli;

using CommandLine;

using Microsoft.Extensions.Logging;

using static b2cpolicymanager_cli.OptionsBase;

using ILoggerFactory loggerFactory = LoggerFactory.Create(
	builder => builder.AddConsole()
);
ILogger logger = loggerFactory.CreateLogger<Program>();

OptionsBase? options = null;
var parserResult = Parser.Default.ParseArguments<ListOptions, GetOptions, DeployOptions>( args )
	.WithParsed<OptionsBase>(
		opts => options = opts
	);

if( options == null )
{
	logger.LogError( "Invalid command line options" );
	Environment.Exit( 1 );
}

var authHelper = new ConfidentialAuthenticationHelper(
	options.TenantId,
	options.AppId,
	options.AppSecret
);

var token = await authHelper.GetTokenAsync();

// logger.LogInformation( "Token: {token}", token );

var policyManager = new PolicyManager( authHelper );

await parserResult
	.MapResult(
		async ( ListOptions opts ) =>
		{
			foreach( var policy in await policyManager.GetPoliciesAsync( logger ) )
			{
				logger.LogInformation( "Policy: {policy}", policy );
			}
		},
		async ( GetOptions opts ) =>
		{
			var policies = opts.PolicyNames.Any() 
				? opts.PolicyNames
				: await policyManager.GetPoliciesAsync( logger );

			await foreach( var policy in policyManager.GetPolicyDefinitionsAsync( logger, policies ) )
			{
				var path = Path.Join( opts.Folder, Path.ChangeExtension( policy.Id, ".xml" ) );

				await File.WriteAllTextAsync( path, policy.Text );
			}
		},
		async ( DeployOptions opts ) =>
		{
			var fileNames = opts.PolicyNames
				.SelectMany(
					policyName => Directory.EnumerateFiles( opts.Folder, Path.ChangeExtension( policyName, ".xml" ) )
				);

			await policyManager.DeployPoliciesAsync( logger, fileNames );
		},
		errs =>
		{
			logger.LogError( "Invalid command line options" );
			Environment.Exit( 1 );

			// never gets here, but the compiler doesn't know that
			return Task.CompletedTask;
		}
	)
;

