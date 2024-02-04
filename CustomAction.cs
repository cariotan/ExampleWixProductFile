using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CredentialManagement;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using Referral.Shared;
using ReferralSelection;

namespace PdfScribeInstallCustomAction
{
	/// <summary>
	/// Lotsa notes from here:
	/// http://stackoverflow.com/questions/835624/how-do-i-pass-msiexec-properties-to-a-wix-c-sharp-custom-action
	/// </summary>
	public class CustomActions
	{
		[CustomAction]
		public static ActionResult CheckIfPrinterNotInstalled(Session session)
		{
			ActionResult resultCode;
			SessionLogWriterTraceListener installTraceListener = new SessionLogWriterTraceListener(session);
			PdfScribeInstaller installer = new PdfScribeInstaller();
			installer.AddTraceListener(installTraceListener);
			try
			{
				if(installer.IsPdfScribePrinterInstalled())
					resultCode = ActionResult.Success;
				else
					resultCode = ActionResult.Failure;
			}
			finally
			{
				if(installTraceListener != null)
					installTraceListener.Dispose();
			}

			return resultCode;
		}

		[CustomAction]
		public static ActionResult InstallVisualCpp(Session session)
		{
			if(IsVisualCppInstalled() is false)
			{
				var process = Process.Start("VC_redist.x64.exe", "/q /norestart");

				for(int i = 0; i < 30; i++)
				{
					if(IsVisualCppInstalled())
					{
						break;
					}

					Thread.Sleep(1000);
				}
			}

			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult OpenProgram(Session session)
		{
			string outputCommand = session.CustomActionData["OutputCommand"];
			Process.Start(outputCommand);

			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult CreateSqliteDb(Session session)
		{
			GetConnectionStringOrCreateDb.GetOrCreate();

			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult UpdatingRegistry(Session session)
		{
			var roamingFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var referralRoamingPath = Path.Combine(roamingFolderPath, "ReferralSelection");

			//if(Directory.Exists(referralRoamingPath) is false)
			//{
			//	Directory.CreateDirectory(referralRoamingPath);
			//}

			var localFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var referralLocalPath = Path.Combine(localFolderPath, "ReferralSelection");

			if(Directory.Exists(referralLocalPath) is false)
			{
				Directory.CreateDirectory(referralLocalPath);
			}

			var databaseLocalFilePath = Path.Combine(referralLocalPath, "Database");
			var databaseRoamingFilePath = Path.Combine(referralRoamingPath, "Database");

			//if(File.Exists(databaseLocalFilePath) && File.Exists(databaseRoamingFilePath) is false)
			//{
			//	File.Copy(databaseLocalFilePath, databaseRoamingFilePath);
			//}

			if(File.Exists(databaseRoamingFilePath) && File.Exists(databaseLocalFilePath) is false)
			{
				File.Copy(databaseRoamingFilePath, databaseLocalFilePath);
			}

			Assembly assembly = Assembly.GetExecutingAssembly();
			Version version = assembly?.GetName()?.Version;

			RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

			key = key.OpenSubKey("Software", true);

			if(key == null)
			{
				return ActionResult.Success;
			}

			var programName = "eOrdering Updater";

			key = key.CreateSubKey(programName);

			key.SetValue("Version", version);

			key.Close();

			return ActionResult.Success;
		}

		[CustomAction]
		public static ActionResult InstallPdfScribePrinter(Session session)
		{
			ActionResult printerInstalled;

			String driverSourceDirectory = session.CustomActionData["DriverSourceDirectory"];
			String outputCommand = session.CustomActionData["OutputCommand"];
			String outputCommandArguments = session.CustomActionData["OutputCommandArguments"];

			SessionLogWriterTraceListener installTraceListener = new SessionLogWriterTraceListener(session);
			installTraceListener.TraceOutputOptions = TraceOptions.DateTime;

			PdfScribeInstaller installer = new PdfScribeInstaller();
			installer.AddTraceListener(installTraceListener);
			try
			{
				if(installer.InstallPdfScribePrinter(driverSourceDirectory, outputCommand, outputCommandArguments))
				{
					printerInstalled = ActionResult.Success;
				}
				else
				{
					printerInstalled = ActionResult.Failure;
				}

				installTraceListener.CloseAndWriteLog();
			}
			finally
			{
				if(installTraceListener != null)
					installTraceListener.Dispose();

			}

			return printerInstalled;
		}

		private static bool IsVisualCppInstalled()
		{
			return ReferralSelection.IsVisualCppInstalled.Check();
		}

		[CustomAction]
		public static ActionResult UninstallPdfScribePrinter(Session session)
		{
			ActionResult printerUninstalled;

			SessionLogWriterTraceListener installTraceListener = new SessionLogWriterTraceListener(session);
			installTraceListener.TraceOutputOptions = TraceOptions.DateTime;

			PdfScribeInstaller installer = new PdfScribeInstaller();
			installer.AddTraceListener(installTraceListener);
			try
			{
				if(installer.UninstallPdfScribePrinter())
					printerUninstalled = ActionResult.Success;
				else
					printerUninstalled = ActionResult.Failure;
				installTraceListener.CloseAndWriteLog();
			}
			finally
			{
				if(installTraceListener != null)
					installTraceListener.Dispose();
			}
			return printerUninstalled;
		}

		[CustomAction]
		public static ActionResult CleanUp(Session session)
		{
			// Delete ReferralSelection in Local Folder.
			var localFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var referralLocalPath = Path.Combine(localFolderPath, "ReferralSelection");

			if(Directory.Exists(referralLocalPath))
			{
				Directory.Delete(referralLocalPath, true);
			}

			// Delete ReferralSelection in Roaming Folder.
			var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var referral = Path.Combine(roaming, "ReferralSelection");

			if(Directory.Exists(referral))
			{
				Directory.Delete(referral, true);
			}

			// Delete User.Config Folder.
			string companyName = "";

			Assembly assembly = Assembly.GetExecutingAssembly();

			object[] attributes = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);

			if(attributes.Length > 0)
			{
				AssemblyCompanyAttribute companyAttribute = (AssemblyCompanyAttribute)attributes[0];
				companyName = companyAttribute.Company;
				if(string.IsNullOrEmpty(companyName) is false)
				{
					var companyFolder = Path.Combine(localFolderPath, companyName);

					if(Directory.Exists(companyFolder))
					{
						Directory.Delete(companyFolder, true);
					}
				}
			}

			// Delete eOrdering Updater Registry Key.
			RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

			key = key.OpenSubKey("Software", true);

			if(key == null)
			{
				return ActionResult.Success;
			}

			var programName = "eOrdering Updater";

			if(key != null)
			{
				key.DeleteSubKeyTree(programName, false);
				key.Close();
			}

			return ActionResult.Success;
		}
	}
}
