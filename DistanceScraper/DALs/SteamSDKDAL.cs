using SteamKit2;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static SteamKit2.SteamClient;
using static SteamKit2.SteamUser;

namespace DistanceScraper
{
	public class BaseScraper
	{
		protected uint AppID { get; set; }
		protected Handlers Handlers { get; set; }
		private SteamClient Client;
		private CallbackManager Manager;
		private bool IsReady;

		public BaseScraper(uint appID)
		{
			AppID = appID;
		}

		public void Init()
		{
			Client = new SteamClient();
			Manager = new CallbackManager(Client);

			Handlers = new Handlers()
			{
				UserStats = Client.GetHandler<SteamUserStats>(),
				User = Client.GetHandler<SteamUser>(),
			};

			Manager.Subscribe<ConnectedCallback>(OnConnected);
			Manager.Subscribe<DisconnectedCallback>(OnDisconnected);
			Manager.Subscribe<LoggedOnCallback>(OnLoggedOn);
			Manager.Subscribe<LoggedOffCallback>(OnLoggedOff);

			Client.Connect();
			new Thread(() => {
				while (true)
				{
					Manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
				}
			}).Start();
			
			// Wait for login
			while(!IsReady)
			{
				Thread.Sleep(TimeSpan.FromSeconds(5));
			}
		}

		private void OnConnected(ConnectedCallback callback)
		{
			Utils.WriteLine("Init", $"Connected to Steam! Logging in '{Settings.Username}'...");

			LogOn();
		}

		private void OnDisconnected(DisconnectedCallback callback)
		{
			// after recieving an AccountLogonDenied, we'll be disconnected from steam
			// so after we read an authcode from the user, we need to reconnect to begin the logon flow again

			Utils.WriteLine("Init", "Disconnected from Steam...");

			Thread.Sleep(TimeSpan.FromSeconds(5));

			Client.Connect();
		}

		private byte[] GetSentryHash()
		{
			byte[] sentryHash = null;
			if (File.Exists("sentry.bin"))
			{
				// if we have a saved sentry file, read and sha-1 hash it
				var sentryFile = File.ReadAllBytes("sentry.bin");
				sentryHash = CryptoHelper.SHAHash(sentryFile);
			}

			return sentryHash;
		}

		private void LogOn()
		{
			Handlers.User.LogOn(new LogOnDetails
			{
				Username = Settings.Username,
				Password = Settings.Password,
				AuthCode = null,
				TwoFactorCode = null,
			});
		}

		private void OnLoggedOn(LoggedOnCallback callback)
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

		private void OnLoggedOff(LoggedOffCallback callback)
		{
			IsReady = false;
			Utils.WriteLine("Init", $"Logged off of Steam: {callback.Result}");
			LogOn();
		}
	}
}
