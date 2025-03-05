﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using B2CPolicyManager;
using B2CPolicyManager.Models;

using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.Logging;

using Newtonsoft.Json;

#pragma warning disable VSTHRD100 //  Avoid "async void" methods, because any exceptions not handled by the method will crash the process. Event Handlers are an exception.

namespace B2CPolicyManagerUI
{
	public partial class B2CPolicyManager :Form
	{
		List<String> _policyList = new();
		List<App> _appRegistrationList = new();

		ILogger Logger { get; }

		AuthenticationHelper AuthenticationHelper 
			=> new( tenantTxt.Text, new Guid( txtAppId.Text ) );

		PolicyManager PolicyManager
			=> new( AuthenticationHelper );

		public B2CPolicyManager()
		{
			InitializeComponent();
			Logger = new TextBoxLogger( HTTPResponse );
		}

		private App SelectedAppRegistration
			=> _appRegistrationList
				.Where(
					app => app.displayName == lstAppRegistrations.SelectedItem?.ToString()
				)
				.FirstOrDefault()
			;

		private String SelectedAppRegistrationId
			=> SelectedAppRegistration?.appId;

		private async Task UpdatePolicyListAsync( Boolean refresh )
		{
			lstPolicies.Items.Clear();
			if( refresh )
			{
				_policyList = await PolicyManager.GetPoliciesAsync( Logger );
				if( _policyList.Count == 0 )
				{
					Logger.LogWarning( "There are no custom policy files available in the tenant." );
				}
			}

			if( _policyList.Count != 0 )
			{
				var policyList = _policyList
					.Where(
						policyId =>
							!showRPs.Checked
							||
							(
								!policyId.Contains( "BASE" )
								&&
								!policyId.Contains( "EXTENSIONS" )
								&&
								!policyId.Contains( "LOCALIZATON" )
							)
					)
					.Select(
						policyId => policyId.ToLower()
					)
					.Where(
						policyId =>
							String.IsNullOrEmpty( txtPoliciesFilter.Text )
							||
							policyId.Contains( txtPoliciesFilter.Text.ToLower() )
					)
					.OrderBy( x => x )
					.ToList()
				;

				foreach( string policyId in policyList )
				{
					lstPolicies.Items.Add( policyId );
				}
			}

		}

		private async Task UpdateAppRegistrationsAsync( Boolean refresh )
		{
			if( refresh )
			{
				_appRegistrationList = await PolicyManager.GetAppRegistrationsAsync( Logger );
				if( _appRegistrationList.Count == 0 )
				{
					Logger.LogWarning( "There are no app registrations available in the tenant." );
				}
			}

			lstAppRegistrations.Items.Clear();
			foreach( var app in _appRegistrationList )
			{
				lstAppRegistrations.Items.Add( app.displayName );
			}

			UpdateRedirectUrls();
		}

		private void btnSelectPolicyFolder_Click( object sender, EventArgs e )
		{
			var fbd = new FolderBrowserDialog();
			if( policyFolderLbl.Text != null )
			{
				fbd.SelectedPath = policyFolderLbl.Text;
			}
			if( fbd.ShowDialog() == DialogResult.OK )
			{
				Properties.Settings.Default.Folder = fbd.SelectedPath;
				Properties.Settings.Default.Save();
				policyFolderLbl.Text = fbd.SelectedPath;
				btnRefrshFileList_Click( sender, e );
			}
		}

		private async void btnLogin_Click( object sender, EventArgs e )
		{
			if( btnLogin.Text == "Login" )
			{
				string token = await AuthenticationHelper.GetTokenForUserAsync();
				if( token != null )
				{
					btnLogin.Text = "Logout";
					Logger.LogInformation( "Logged in, getting policies and app registrations." );
					await UpdatePolicyListAsync( true );
					await UpdateAppRegistrationsAsync( true );
				}
			}
			else
			{
				btnLogin.Text = "Login";
				await AuthenticationHelper.ClearCacheAsync();
				lstPolicies.Items.Clear();
			}
		}

		private async void btnListPolicies_Click( object sender, EventArgs e )
		{
			string token = await AuthenticationHelper.GetTokenForUserAsync();
			if( token != null )
			{
				await UpdatePolicyListAsync( true );
			}
		}

		private async void btnDeleteSelected_Click( object sender, EventArgs e )
		{

			if( lstPolicies.SelectedItem != null )
			{
				string token = await AuthenticationHelper.GetTokenForUserAsync();
				if( token != null )
				{
					await PolicyManager.DeletePolicyAsync( Logger, lstPolicies.SelectedItem.ToString() );
					await UpdatePolicyListAsync( true );
				}
			}
		}

		private async void btnUpdateSelectedPolices_Click( object sender, EventArgs e )
		{

			if( policyFolderLbl.Text != "No Folder selected." )
			{
				var selectedPolicies = lstPolicyFiles.CheckedItems
					.Cast<String>()
					.Select( policy => Path.Join( policyFolderLbl.Text, policy ) )
					.ToList();
				await PolicyManager.Deploy( Logger, selectedPolicies );
				await UpdatePolicyListAsync( true );
			}

		}

		private void btnRefrshFileList_Click( object sender, EventArgs e )
		{
			if( policyFolderLbl.Text != "No Folder selected." )
			{
				lstPolicyFiles.Items.Clear();
				var fileEntries = Directory.EnumerateFiles( policyFolderLbl.Text )
					.Select(
						p => Path.GetFileName( p )
					)
					.Where(
						fn =>
							Path.GetExtension(fn).Equals( ".xml", StringComparison.CurrentCultureIgnoreCase )
							&&
							fn.ContainsEx(txtPolicyFileFilter.Text)
					)
				;
				foreach( string file in fileEntries )
				{
					lstPolicyFiles.Items.Add( file );
				}
			}
		}

		private void btnCopyRunNow_Click( object sender, EventArgs e )
		{
			if( txtRunNow.Text != "" )
			{
				Clipboard.SetText( txtRunNow.Text );
			}
		}

		private void tenantTxt_TextChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.TenantId = tenantTxt.Text;
			Properties.Settings.Default.Save();
			UpdateRunNow();
		}

		private void lstPolicies_SelectedIndexChanged( object sender, EventArgs e )
		{
			UpdateRunNow();
		}

		private void UpdateRunNow()
		{
			String runNow = "";
			var domain = "login.microsoftonline.com";

			if( lstPolicies.SelectedItem != null )
			{
				var idtokens = new List<String> { "id_token" };
				var scopes = new List<String> { "openid" };
				var policy = lstPolicies.SelectedItem.ToString();

				if( lstAppRegistrations.SelectedItem != null )
				{
					var regex = new Regex(@"\w*");
					var match = regex.Match(tenantTxt.Text);
					if( match.Success )
					{
						domain = $"{match.Value}.b2clogin.com";
					}

					if( getAccessToken.Checked )
					{
						foreach( var scope in b2cResource.Text.Split( ' ' ) )
						{
							scopes.Add( scope.Trim() );
						}
						idtokens.Add( "token" );
					}

				}

				var scopesString = Uri.EscapeDataString( String.Join( " ", scopes ).Trim() );
				var idtokensString = Uri.EscapeDataString( String.Join( " ", idtokens ).Trim() );
				var replyUrlString = Uri.EscapeDataString( txtReplyUrl.SelectedItem?.ToString() ?? "" );
				runNow = $"https://{domain}/{SelectedAppRegistrationId}/oauth2/v2.0/authorize?p={policy}&client_id={SelectedAppRegistrationId}&nonce=defaultNonce&redirect_uri={replyUrlString}&scope={scopesString}&response_type={idtokensString}&prompt=login&disable_cache=true";
			}

			txtRunNow.Text = runNow;
		}

		private void B2CPolicyManager_Load( object sender, EventArgs e )
		{
			this.lstAppRegistrations.SelectedItem = Properties.Settings.Default.B2CAppId;
			this.tenantTxt.Text = Properties.Settings.Default.TenantId;
			this.txtAppId.Text = Properties.Settings.Default.V2AppId;
			this.txtReplyUrl.SelectedItem = Properties.Settings.Default.ReplyUrl;
			this.policyFolderLbl.Text = Properties.Settings.Default.Folder;
			this.showRPs.Checked = Properties.Settings.Default.ShowRPs;
			this.getAccessToken.Checked = Properties.Settings.Default.GetAccessToken;
			this.b2cResource.Text = Properties.Settings.Default.Resource;

			btnRefrshFileList_Click( sender, e );
		}

		private void txtAppId_TextChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.V2AppId = txtAppId.Text;
			Properties.Settings.Default.Save();
		}

		private void btnClearLog_Click( object sender, EventArgs e )
		{
			HTTPResponse.Text = "";
		}

		private void btnOpenFolderInVSCode_Click( object sender, EventArgs e )
		{
			if( this.policyFolderLbl.Text != "No Folder selected." )
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = string.Format("code"),
					WindowStyle = ProcessWindowStyle.Hidden,
					Arguments = string.Format("\"{0}\"", policyFolderLbl.Text)
				};
				Process.Start( startInfo );
			}
		}

		private void RunCommand( String cmdline )
		{
			var startInfo = new ProcessStartInfo("cmd", "/c " + cmdline)
			{
				WindowStyle = ProcessWindowStyle.Hidden
			};
			Process.Start( startInfo );
		}

		private void btnOpenInChrome_Click( object sender, EventArgs e )
		{
			RunCommand( $"start chrome --incognito --new-window \"{txtRunNow.Text}\"" );
		}

		private void btnOpenInEdge_Click( object sender, EventArgs e )
		{
			RunCommand( $"start msedge.exe \"{txtRunNow.Text}\" -inPrivate" );
		}

		private void btnSelectAllPolicies_Click( object sender, EventArgs e )
		{
			int elementCount = lstPolicyFiles.Items.Count;
			for( int i = 0; i < elementCount; i++ )
			{
				lstPolicyFiles.SetItemChecked( i, true );
			}
		}

		private void btnDeselectAllPolicies_Click( object sender, EventArgs e )
		{
			int elementCount = lstPolicyFiles.Items.Count;
			for( int i = 0; i < elementCount; i++ )
			{
				lstPolicyFiles.SetItemChecked( i, false );
			}
		}

		private void btnSamlSP_Click( object sender, EventArgs e )
		{
			Regex regex = new Regex(@"\w*");
			Match match = regex.Match(tenantTxt.Text);
			if( lstPolicies.SelectedItem != null )
			{
				string url = "https://b2csamlrp.azurewebsites.net/SP/autoinitiate?" + "tenant=" + match.Value + "&policy=" + lstPolicies.SelectedItem.ToString();
				string command = String.Format("start chrome --incognito --new-window \"{0}\"", url);
				ProcessStartInfo startInfo = new ProcessStartInfo("cmd", "/c " + command)
				{
					WindowStyle = ProcessWindowStyle.Hidden
				};
				Process.Start( startInfo );
			}

		}

		private async void showRPs_CheckedChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.ShowRPs = showRPs.Checked;
			Properties.Settings.Default.Save();
			await UpdatePolicyListAsync( false );
		}

		private void getAccessToken_CheckedChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.GetAccessToken = getAccessToken.Checked;
			Properties.Settings.Default.Save();
			UpdateRunNow();
		}

		private void b2cResource_TextChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.Resource = b2cResource.Text;
			Properties.Settings.Default.Save();
			UpdateRunNow();
		}

		private async void policyListFilter_TextChanged( object sender, EventArgs e )
		{
			await UpdatePolicyListAsync( false );
		}

		private void txtPolicyFileFilter_TextChanged( object sender, EventArgs e )
		{
			btnRefrshFileList_Click( sender, e );
		}

		private void lstAppRegistrations_SelectedIndexChanged( object sender, EventArgs e )
		{
			Properties.Settings.Default.B2CAppId = SelectedAppRegistrationId;
			Properties.Settings.Default.Save();

			UpdateRedirectUrls();
			UpdateRunNow();
		}

		private void UpdateRedirectUrls()
		{
			txtReplyUrl.Items.Clear();
			txtReplyUrl.Text = "";

			foreach( string url in SelectedAppRegistration?.web.redirectUris ?? new() )
			{
				txtReplyUrl.Items.Add( url );
			}
		}

		private void txtReplyUrl_SelectedIndexChanged( object sender, EventArgs e )
		{
			UpdateRunNow();

			Properties.Settings.Default.ReplyUrl = txtReplyUrl.SelectedItem.ToString();
			Properties.Settings.Default.Save();
		}


	}
}
