using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static SteamKit2.SteamClient;
using static SteamKit2.SteamFriends;
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

		private readonly Queue<SteamID> UsersToRequest = new Queue<SteamID>();
		private SteamID RequestedSteamID;
		private TaskCompletionSource<bool> IsRequestingUsers;

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
				Friends = Client.GetHandler<SteamFriends>(),
			};

			Manager.Subscribe<ConnectedCallback>(OnConnected);
			Manager.Subscribe<DisconnectedCallback>(OnDisconnected);
			Manager.Subscribe<LoggedOnCallback>(OnLoggedOn);
			Manager.Subscribe<LoggedOffCallback>(OnLoggedOff);
			Manager.Subscribe<UpdateMachineAuthCallback>(OnMachineAuth);
			Manager.Subscribe<PersonaStateCallback>(OnRequestUserInfo);

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
			Utils.WriteLine($"Connected to Steam! Logging in '{Settings.Username}'...");

			LogOn();
		}

		private void OnDisconnected(DisconnectedCallback callback)
		{
			// after recieving an AccountLogonDenied, we'll be disconnected from steam
			// so after we read an authcode from the user, we need to reconnect to begin the logon flow again

			Utils.WriteLine("Disconnected from Steam...");

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
				SentryFileHash = GetSentryHash(),
			});
		}

		private void OnLoggedOn(LoggedOnCallback callback)
		{
			if (callback.Result != EResult.OK)
			{
				Utils.WriteLine($"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}");
				Thread.Sleep(TimeSpan.FromSeconds(60));
				LogOn();
			}
			else
			{
				IsReady = true;
				Utils.WriteLine("Successfully logged on!");
			}
		}

		private void OnLoggedOff(LoggedOffCallback callback)
		{
			IsReady = false;
			Utils.WriteLine($"Logged off of Steam: {callback.Result}");
			LogOn();
		}

		private void OnMachineAuth(UpdateMachineAuthCallback callback)
		{
			Utils.WriteLine("Updating sentryfile...");

			// write out our sentry file
			// ideally we'd want to write to the filename specified in the callback
			// but then this sample would require more code to find the correct sentry file to read during logon
			// for the sake of simplicity, we'll just use "sentry.bin"

			int fileSize;
			byte[] sentryHash;
			using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
			{
				fs.Seek(callback.Offset, SeekOrigin.Begin);
				fs.Write(callback.Data, 0, callback.BytesToWrite);
				fileSize = (int) fs.Length;

				fs.Seek(0, SeekOrigin.Begin);
				using (var sha = SHA1.Create())
				{
					sentryHash = sha.ComputeHash(fs);
				}
			}

			// inform the steam servers that we're accepting this sentry file
			Handlers.User.SendMachineAuthResponse(new MachineAuthDetails
			{
				JobID = callback.JobID,
				FileName = callback.FileName,
				BytesWritten = callback.BytesToWrite,
				FileSize = fileSize,
				Offset = callback.Offset,
				Result = EResult.OK,
				LastError = 0,
				OneTimePassword = callback.OneTimePassword,
				SentryFileHash = sentryHash,
			});

			Utils.WriteLine("Sentryfile updated!");
		}

		public async Task RequestUserInfo(SteamID steamID)
		{
			if (Handlers.Friends.GetFriendPersonaName(steamID) == null)
			{
				await RequestUserInfo(new List<SteamID>() { steamID });
			}
		}

		public async Task RequestUserInfo(List<SteamID> steamIDs)
		{
			if(steamIDs.Count == 0)
			{
				return;
			}

			IsRequestingUsers = new TaskCompletionSource<bool>();
			steamIDs.ForEach(x => UsersToRequest.Enqueue(x));
			var next = UsersToRequest.Dequeue();
			Utils.WriteLine($"Requesting name for {next}");
			RequestedSteamID = next;
			Handlers.Friends.RequestFriendInfo(next, EClientPersonaStateFlag.PlayerName);

			await IsRequestingUsers.Task;
		}

		public void OnRequestUserInfo(PersonaStateCallback callback)
		{
			// Skip callbacks from stuff we don't care about
			if(callback.FriendID != RequestedSteamID)
			{
				return;
			}

			Utils.WriteLine($"Name found for      {callback.FriendID}");
			if (UsersToRequest.Count == 0)
			{
				//Utils.WriteLine("Queue empty");
				IsRequestingUsers.SetResult(true);
			}
			else
			{
				var next = UsersToRequest.Dequeue();
				Utils.WriteLine($"Requesting name for {next}");
				RequestedSteamID = next;
				Handlers.Friends.RequestFriendInfo(next, EClientPersonaStateFlag.PlayerName);
			}
		}
	}
}
