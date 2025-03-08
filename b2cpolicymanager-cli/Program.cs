using System.Text.Json;

using B2CPolicyManager;

using b2cpolicymanager_cli;

using CommandLine;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using static b2cpolicymanager_cli.Options;

Options? options = null;

// Resolve command line options that are environment references (starting with "env:")
var expandedArgs
	= args.Select(
		arg =>
			arg.StartsWith( "env:", StringComparison.CurrentCultureIgnoreCase )
				? Environment.GetEnvironmentVariable( arg[  4 ..] ) ?? ""
				: arg
	);

var parserResult = Parser.Default.ParseArguments<ListOptions, GetOptions, DeployOptions>( expandedArgs )
	.WithParsed<Options>(
		opts => options = opts
	);

using ILoggerFactory loggerFactory
	= LoggerFactory.Create(
		builder => builder
			.AddConsole(
				opts =>
				{
					opts.FormatterName = "CustomFormatter";
				}
			)
			.AddConsoleFormatter<CustomFormatter, CustomFormatterOptions>(
				opts =>
				{
					opts.TimestampFormat = "HH:mm:ss.fff";
				}
			)
			.SetMinimumLevel(
				options?.Verbose ?? true
					? LogLevel.Trace
					: LogLevel.Information
			)
	);

ILogger logger = loggerFactory.CreateLogger<Program>();

if( options == null )
{
	logger.LogCritical( "Invalid command line options" );
	Environment.Exit( 1 );
}

logger.LogDebug(
	"Options: {options}",
	JsonSerializer.Serialize( 
		options, 
		new JsonSerializerOptions
		{
			WriteIndented = true 
		} 
	) 
);

var authHelper = new ConfidentialAuthenticationHelper(
	options.TenantId,
	options.AppId,
	options.AppSecret
);

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

