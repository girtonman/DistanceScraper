using SteamKit2;
using SteamKit2.Authentication;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SteamKit2.SteamClient;
using static SteamKit2.SteamUser;

namespace DistanceScraper
{
	public static class SteamSDKDAL
	{
		public static SteamUserStats UserStats { get; set; }
		public static SteamUser User { get; set; }
		private static SteamClient Client;
		private static CallbackManager Manager;
		private static bool IsReady;

		public static void Init()
		{
			// Initialize steam client
			Client = new SteamClient();
			Manager = new CallbackManager(Client);

			// Initialize handlers for interfaces
			UserStats = Client.GetHandler<SteamUserStats>();
			User = Client.GetHandler<SteamUser>();

			// Register callbacks
			Manager.Subscribe<ConnectedCallback>(OnConnected);
			Manager.Subscribe<DisconnectedCallback>(OnDisconnected);
			Manager.Subscribe<LoggedOnCallback>(OnLoggedOn);
			Manager.Subscribe<LoggedOffCallback>(OnLoggedOff);

			Client.Connect();
			new Thread(() =>
			{
				while (true)
				{
					// TODO: Figure out if swallowing these exceptions is bad
					// NOTE: Adding this try catch might be what solves mysterious scraper deaths
					// since callbacks would not be processed if this thread ever died since it 
					// doesn't get started up again
					try
					{
						Manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
					}
					catch (Exception e)
					{
						Utils.WriteLine("Callback Loop", $"Swallowing unexpected exception during RunWaitCallbacks: {e.Message}");
					}
				}
			}).Start();

			// Wait for login
			while (!IsReady)
			{
				Thread.Sleep(TimeSpan.FromSeconds(5));
			}
		}

		private static void OnConnected(ConnectedCallback callback)
		{
			Utils.WriteLine("Init", $"Connected to Steam! Logging in '{Settings.Username}'...");

			LogOn();
		}

		private static void OnDisconnected(DisconnectedCallback callback)
		{
			// after recieving an AccountLogonDenied, we'll be disconnected from steam
			// so after we read an authcode from the user, we need to reconnect to begin the logon flow again

			Utils.WriteLine("Init", "Disconnected from Steam...");

			Thread.Sleep(TimeSpan.FromSeconds(5));

			Client.Connect();
		}

		private async static void LogOn()
		{
			var authSession = await Client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
			{
				Username = Settings.Username,
				Password = Settings.Password,
				IsPersistentSession = false,
				Authenticator = new NoSteamGuardAuthenticator(),
			});

			var pollResponse = await authSession.PollingWaitForResultAsync();

			User.LogOn(new LogOnDetails
			{
				Username = pollResponse.AccountName,
				AccessToken = pollResponse.RefreshToken,
				ShouldRememberPassword = false,
			});
		}

		private static void OnLoggedOn(LoggedOnCallback callback)
		{
			if (callback.Result != EResult.OK)
			{
				Utils.WriteLine("Init", $"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");
				Thread.Sleep(TimeSpan.FromSeconds(60));
				LogOn();
			}
			else
			{
				IsReady = true;
				Utils.WriteLine("Init", "Successfully logged on!");
			}
		}

		private static void OnLoggedOff(LoggedOffCallback callback)
		{
			IsReady = false;
			Utils.WriteLine("Init", $"Logged off of Steam: {callback.Result}");
			LogOn();
		}

		// Husk of an implementation for a steam guard authenticator
		// Accounts for this scraper should NOT have steam guard enabled
		private class NoSteamGuardAuthenticator : IAuthenticator
		{
			public NoSteamGuardAuthenticator() {}

			public Task<bool> AcceptDeviceConfirmationAsync()
			{
				Utils.WriteLine("Init", "This account has steamguard enabled. It must be disabled for the scraper to work");
				throw new NotImplementedException();
			}

			public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
			{
				Utils.WriteLine("Init", "This account has steamguard enabled. It must be disabled for the scraper to work");
				throw new NotImplementedException();
			}

			public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
			{
				Utils.WriteLine("Init", "This account has steamguard enabled. It must be disabled for the scraper to work");
				throw new NotImplementedException();
			}
		}
	}
}
