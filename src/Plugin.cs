using BepInEx;
using System.Security.Permissions;
using System;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Net.Http;
using System.Globalization;
using System.Threading.Tasks;
using Menu.Remix.MixedUI;
using System.Linq;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Vestiges {

	[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
	public sealed class Plugin : BaseUnityPlugin {
		public const string PLUGIN_GUID = "frostbird347.vestiges";
		public const string PLUGIN_NAME = "Vestiges";
		public const string PLUGIN_VERSION = "1.0.2";
		
		bool init;
		private PluginOptions Options = null;
		bool configWorking = false;

		Dictionary<string, Dictionary<string, List<VestigeSpawn>>> vestigeData;
		List<string> rawDownloads;
		List<VestigeSpawn> localvestigeData;
		Dictionary<int, DateTime> localDeathTimes;

		List<Vestige> activeVestigeList;
		public static List<Room> activeRooms;
		Dictionary<int, WorldCoordinate> backupTargets;

		List<VestigeSpawnQueue> vestigeSpawnQueue;
		int vestigeUploadLimiter;
		List<WorldCoordinate> lastVestigeSpawns;
		public static Dictionary<Player, int> lastKarmas;
		private bool mustCheckKarma;
		private bool justReachedEndScreen;
		private DateTime lastDev;

		private static readonly HttpClient httpClient = new HttpClient();
		public static bool isDownloading;
		public static bool isDownloaded;
		public static int vestigeCount;
		public static int vestigeCountKarma;
		private DateTime nextDownload;
		int lastLifespan;
		bool lastInfiniteLifespan;

		public void OnEnable() {
			On.RainWorld.OnModsInit += Init;

			On.Player.NewRoom += SpawnVestiges;
			On.RainWorldGame.Update += UpdateGame;
			On.KarmaFlower.BitByPlayer += LockKarma;
			On.Player.Die += OnDeath;
			On.Player.Grabbed += OnGrabDeath;
			On.RainWorldGame.ctor += StartCycle;
			On.SaveState.SessionEnded += ReachEndScreen;
			On.RainWorldGame.ShutDownProcess += EndCycle;
		}

		private void Init(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
			orig(self);

			if (!init) {
				Logger.LogDebug("Init");

				init = true;

				vestigeData = new Dictionary<string, Dictionary<string, List<VestigeSpawn>>>();
				rawDownloads = new List<string>();
				localvestigeData = new List<VestigeSpawn>();
				localDeathTimes = new Dictionary<int, DateTime>();

				activeVestigeList = new List<Vestige>();
				activeRooms = new List<Room>();
				backupTargets = new Dictionary<int, WorldCoordinate>();

				vestigeSpawnQueue = new List<VestigeSpawnQueue>();
				vestigeUploadLimiter = 150;
				lastVestigeSpawns = new List<WorldCoordinate>();
				lastKarmas = new Dictionary<Player, int>();
				mustCheckKarma = false;
				justReachedEndScreen = false;
				lastDev = DateTime.Now.AddYears(-1);

				isDownloading = false;
				isDownloaded = false;
				vestigeCount = 0;
				vestigeCountKarma = 0;
				nextDownload = DateTime.Now.AddYears(-1);
				lastLifespan = -1;
				lastInfiniteLifespan = false;

				try {
					Options = new PluginOptions(this, Logger);
					MachineConnector.SetRegisteredOI("frostbird347.vestiges", Options);
					configWorking = true;

					//Ensure the config is loaded now, so we don't process vestiges incorrectly at startup
					Options._LoadConfigFile();
				} catch (Exception err) {
					Logger.LogError(err);
					configWorking = false;
				}

				if (configWorking) {
					ClearVestiges();
					lastLifespan = Options.Lifespan.Value;
					lastInfiniteLifespan = Options.InfiniteLifespan.Value;
					Task.Run(() => DownloadVestiges(true));
				} else {
					Logger.LogFatal("Config failed to load, this mod has somewhat disabled itself for safety!");

					On.Player.NewRoom -= SpawnVestiges;
					On.RainWorldGame.Update -= UpdateGame;
					On.KarmaFlower.BitByPlayer -= LockKarma;
					On.Player.Die -= OnDeath;
					On.Player.Grabbed -= OnGrabDeath;
					On.RainWorldGame.ctor -= StartCycle;
					On.SaveState.SessionEnded -= ReachEndScreen;
					On.RainWorldGame.ShutDownProcess -= EndCycle;
				}

				Logger.LogDebug("Init done");
			}
		}

		private void SpawnVestiges(On.Player.orig_NewRoom orig, Player self, Room newRoom) {
			orig(self, newRoom);

			string roomName = newRoom.abstractRoom.name;
			string regionName = roomName.Split('_')[0];

			for (int i = activeRooms.Count - 1; i >= 0; i--) {
				if (activeRooms[i] == null || !activeRooms[i].BeingViewed) {
					activeRooms.RemoveAt(i);
				}
			}

			if (!activeRooms.Contains(newRoom) && newRoom.BeingViewed) {
				activeRooms.Add(newRoom);

				if (!self.dead && vestigeData.ContainsKey(regionName) && vestigeData[regionName].ContainsKey(roomName)) {
					for (int i = 0; i < vestigeData[regionName][roomName].Count && i < Options.VestigeLimit.Value; i++) {

						VestigeSpawn spawnInfo = vestigeData[regionName][roomName][i];

						int currentSize = 1;
						if ((DateTime.UtcNow - spawnInfo.time).TotalHours <= Options.LargeHours.Value) {
							currentSize = 2;
						}

						Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), spawnInfo.spawn, spawnInfo.target, Options.ShouldOverrideColours.Value ? Options.OverridenColour.Value : spawnInfo.colour, currentSize, Options.VestigeLights.Value == PluginOptions.LightSetting.All || (Options.VestigeLights.Value == PluginOptions.LightSetting.KarmaOnly && spawnInfo.karma), Options.Karma.Value && spawnInfo.karma);
						newRoom.AddObject(newBug);
						activeVestigeList.Add(newBug);
						newBug.SetupLogger(Logger);
					}
				}
				if (!self.dead) {
					for (int i = 0; i < localvestigeData.Count; i++) {
						if (localvestigeData[i].region == regionName && localvestigeData[i].room == roomName) {

							int currentSize = 1;
							if ((DateTime.UtcNow - localvestigeData[i].time).TotalHours <= Options.LargeHours.Value) {
								currentSize = 2;
							}

							//Always set karma to false for local vestiges
							Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), localvestigeData[i].spawn, localvestigeData[i].target, Options.ShouldOverrideColours.Value ? Options.OverridenColour.Value : localvestigeData[i].colour, currentSize, Options.VestigeLights.Value == PluginOptions.LightSetting.All, false);
							newRoom.AddObject(newBug);
							activeVestigeList.Add(newBug);
						}
					}
				}
			}
		}

		private void UpdateGame(On.RainWorldGame.orig_Update orig, RainWorldGame self) {
			orig(self);
			
			for (int i = activeVestigeList.Count - 1; i >= 0; i--) {
				if (activeVestigeList[i] == null || !activeVestigeList[i].exists) {
					activeVestigeList[i] = null;
					activeVestigeList.RemoveAt(i);
				}
			}

			foreach (AbstractCreature abstractPlayer in self.Players) {
				if (abstractPlayer?.realizedCreature != null && abstractPlayer?.realizedCreature is Player) {
					Player player = abstractPlayer.realizedCreature as Player;

					if (mustCheckKarma && self.session is StoryGameSession) {
						mustCheckKarma = false;
						if ((self.session as StoryGameSession).saveState.deathPersistentSaveData.reinforcedKarma)
							lastKarmas[player] = int.MaxValue;
					}

					if (player.room != null) {
						if (self.devToolsActive)
							lastDev = DateTime.Now.AddMinutes(5);
						AddNewVestige(player);
					}

					if (player.lowerBodyFramesOnGround > 0 && !player.dead && !player.Stunned && player.grabbedBy.Count == 0) {
						backupTargets.Remove(player.playerState.playerNumber);
						backupTargets.Add(player.playerState.playerNumber, player.coord);
					}

					if (!self.GameOverModeActive && lastKarmas.ContainsKey(player) && lastKarmas[player] <= self.clock && self.session is StoryGameSession) {
						lastKarmas.Remove(player);
						Logger.LogDebug("There are now " + lastKarmas.Count + " slugcats with temporary karma!");
						if (Options.Karma.Value && lastKarmas.Count == 0) {
							DeathPersistentSaveData saveData = (self.session as StoryGameSession).saveState.deathPersistentSaveData;
							saveData.reinforcedKarma = false;
						
							TriggerKarmaAnim(player, Logger);
						}
					}
				}
			}
		}

		//If you are writing another mod that locks/reinforces the player's karma and want to ensure compatibility, you just need to run something like Vestiges.Plugin.lastKarmas[player] = int.MaxValue; since the dictionary is public and static
		//Just make sure that single instruction is isolated in it's own class where you can catch any errors as the class is loaded, otherwise you will have vestiges as a hard dependency when it really doesn't need to be one
		//Also, previous versions of the mod don't have this dictionary so checking ModManager.ActiveMods.Exists((ModManager.Mod mod) => mod.id == "frostbird347.vestiges"); prior to instantiating might not be enough, just wrap it in a try/catch to be 100% safe
		//And here's some keyword spam for anyone searching within this file/project: karma lock karma reinforcement karma reinforce karma reinforced karma locked karma flower karmaFlowerKarmaLockKarmaLockedKarmaReinforcedKarma karma, karma... Karma.
		private void LockKarma(On.KarmaFlower.orig_BitByPlayer orig, KarmaFlower self, Creature.Grasp grasp, bool eu) {
			orig(self, grasp, eu);
			if (Options.Karma.Value && grasp.grabber is Player && self.BitesLeft < 1) {
				Logger.LogDebug("Karma flower was consumed, setting the player's karma timer to int.MaxValue...");
				if (lastKarmas.Count > 0 && !lastKarmas.Values.Any(value => value == int.MaxValue))
					TriggerKarmaAnim(grasp.grabber as Player, Logger);
				lastKarmas[grasp.grabber as Player] = int.MaxValue;
			}
		}

		public static void TriggerKarmaAnim(Player player, BepInEx.Logging.ManualLogSource Logger = null) {
			Logger?.LogDebug("Karma animation triggered.");
			for (int i = 0; i < player.room.game.cameras.Length; i++) {
					RoomCamera cam = player.room.game.cameras[i];
					if (cam.hud != null) {
						if (cam.followAbstractCreature == player.abstractCreature || ModManager.CoopAvailable)
							cam.hud.karmaMeter.reinforceAnimation = 0;
					}
			}
		}

		private void OnDeath(On.Player.orig_Die orig, Player self) {
			QueueNewVestige(self, true);
			orig(self);
		}

		private void OnGrabDeath(On.Player.orig_Grabbed orig, Player self, Creature.Grasp grasp) {
			orig(self, grasp);
			if (grasp.grabber.Template.IsLizard) {
				QueueNewVestige(self, false);
			}
		}

		private void QueueNewVestige(Player self, bool actuallyDead) {
			if (self.room.world.game.IsStorySession && (!self.isSlugpup || self.IsJollyPlayer) && !self.isNPC && (!localDeathTimes.ContainsKey(self.playerState.playerNumber) || (DateTime.Now - localDeathTimes[self.playerState.playerNumber]).TotalSeconds >= 10)) {
				localDeathTimes.Remove(self.playerState.playerNumber);
				localDeathTimes.Add(self.playerState.playerNumber, DateTime.Now);

				WorldCoordinate safePos = self.coord;
				if (self.karmaFlowerGrowPos.HasValue && self.karmaFlowerGrowPos.Value.Valid && self.coord.room == self.karmaFlowerGrowPos.Value.room) {
					safePos = self.karmaFlowerGrowPos.Value;
				} else if (backupTargets.ContainsKey(self.playerState.playerNumber) && backupTargets[self.playerState.playerNumber].Valid && backupTargets[self.playerState.playerNumber].room == self.coord.room) {
					safePos = backupTargets[self.playerState.playerNumber];
				}

				//Don't let temporary reinforced karma work instead of the first karma flower
				bool karma = self.KarmaIsReinforced
					&& lastKarmas.Values.Any(value => value == int.MaxValue)
					&& self.grasps.Any(grasp => grasp?.grabbed is KarmaFlower);
				VestigeSpawnQueue newSpawn = new VestigeSpawnQueue(self.coord, safePos, self.ShortCutColor(), karma);
				vestigeSpawnQueue.Add(newSpawn);

				if (self.room != null) {
					AddNewVestige(self);
				}
			}
		}

		private void AddNewVestige(Player self) {
			if (self.room.world.game.IsStorySession && vestigeUploadLimiter <= 150) vestigeUploadLimiter++;
			if (self.room.world.game.IsStorySession && vestigeSpawnQueue.Count != 0 && vestigeUploadLimiter >= 150) {
				int queueIndex = Random.Range(0, vestigeSpawnQueue.Count);
				bool skip = false;
				vestigeUploadLimiter = 0;

				if (!lastVestigeSpawns.Contains(vestigeSpawnQueue[queueIndex].safeCoord)) {

					VestigeSpawn newSpawn = new VestigeSpawn(vestigeSpawnQueue[queueIndex].room, vestigeSpawnQueue[queueIndex].region, vestigeSpawnQueue[queueIndex].colour, new VestigeCoord(vestigeSpawnQueue[queueIndex].coord), new VestigeCoord(vestigeSpawnQueue[queueIndex].safeCoord), DateTime.UtcNow, vestigeSpawnQueue[queueIndex].karma);
					localvestigeData.Add(newSpawn);

					lastVestigeSpawns.Add(vestigeSpawnQueue[queueIndex].safeCoord);
					vestigeCount++;
					if (vestigeSpawnQueue[queueIndex].karma) vestigeCountKarma++;

					bool devCheck = DateTime.Compare(DateTime.Now, lastDev) > 0;

					if (!Options.StealthMode.Value && devCheck) {
						UploadVestige(newSpawn);
					} else if (!devCheck) {
						Logger.LogWarning("Sorry but to slightly lower the amount of vestiges being mass spawned, devtools disables uploading for a while.");
						Logger.LogWarning("While I do expect people to easily get around this, I hope that it will slightly lower the rate of new vestiges being mass spawned in single rooms to a rate where I won't need to lower their lifetime.");
						Logger.LogWarning("I will likely add a way to disable this once the vestige creation rate stabilizes (or remove it completely), especially since you can now lower the vestige lifespan in the config yourself");
					} else {
						Logger.LogWarning("Skipping upload, stealth mode is active!");
					}

					if (self.room != null && self.room.abstractRoom.name == vestigeSpawnQueue[queueIndex].room) {
						Vestige newBug = new Vestige(self.room, new Vector2(0, 0), newSpawn.spawn, newSpawn.target, Options.ShouldOverrideColours.Value ? Options.OverridenColour.Value : newSpawn.colour, 2, Options.VestigeLights.Value == PluginOptions.LightSetting.All || (Options.VestigeLights.Value == PluginOptions.LightSetting.KarmaOnly && newSpawn.karma), false);

						if (Options.StealthMode.Value) {
							newBug.col = new Color(1 - newBug.col.r, 1 - newBug.col.g, 1 - newBug.col.b);
							Logger.LogWarning("Inverted vestige colour because stealth mode is enabled!");
						}

						self.room.AddObject(newBug);
						activeVestigeList.Add(newBug);
					}
				}

				if (!skip) {
					vestigeSpawnQueue.RemoveAt(queueIndex);
				}
			}
		}

		private void ReachEndScreen(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished) {
			justReachedEndScreen = true;
			if (Options.Karma.Value && survived && lastKarmas.Count > 0 && !lastKarmas.Values.Any(value => value == int.MaxValue) && game.IsStorySession) {
				Logger.LogDebug("Removing temporary karma...");
				self.deathPersistentSaveData.reinforcedKarma = false;
			}
			orig(self, game, survived, newMalnourished);
		}

		private void EndCycle(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self) {
			if (Options.Karma.Value && !justReachedEndScreen && self.IsStorySession && self.clock < 40 * 30 && lastKarmas.Count > 0 && !lastKarmas.Values.Any(value => value == int.MaxValue) && self.GetStorySession?.saveState?.deathPersistentSaveData?.reinforcedKarma == true) {
				Logger.LogDebug("Removing temporary karma...");
				self.GetStorySession.saveState.deathPersistentSaveData.reinforcedKarma = false;
				self.GetStorySession.saveState.progression.SaveDeathPersistentDataOfCurrentState(false, false);
			}
			orig(self);
			//Just to be absolutely certain that this doesn't somehow get called multiple times
			justReachedEndScreen = true;
		}

		private void StartCycle(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager) {
			orig(self, manager);

			mustCheckKarma = self.session is StoryGameSession;
			justReachedEndScreen = false;
			lastKarmas.Clear();
			activeRooms.Clear();
			if (lastLifespan != Options.Lifespan.Value || lastInfiniteLifespan != Options.InfiniteLifespan.Value) {
				Logger.LogDebug("Vestige lifespan has changed, clearing and redownloading vestiges...");
				ClearVestiges();
				lastLifespan = Options.Lifespan.Value;
				lastInfiniteLifespan = Options.InfiniteLifespan.Value;
				Task.Run(() => DownloadVestiges(true));
			} else {
				localDeathTimes.Clear();
				backupTargets.Clear();
				Task.Run(() => DownloadVestiges(false));
			}
		}

		private void ResetQueue() {
			Logger.LogDebug("PreResetQueue: " + lastVestigeSpawns.Count);
			for (int li = lastVestigeSpawns.Count - 1; li >= 0; li--) {
				bool found = false;
				for (int ni = 0; ni < vestigeSpawnQueue.Count; ni++) {
					if (lastVestigeSpawns[li] == vestigeSpawnQueue[ni].safeCoord) {
						found = true;
					}
				}

				if (!found) {
					lastVestigeSpawns.RemoveAt(li);
				}
			}
			Logger.LogDebug("PostResetQueue: " + lastVestigeSpawns.Count);

			activeRooms.Clear();
		}

		private void UploadVestige(VestigeSpawn newVest) {
			Logger.LogDebug("Attempting to upload vestige... [" + newVest.room + ":" + newVest.region + ":(" + newVest.colour.r.ToString() + "," + newVest.colour.g.ToString() + "," + newVest.colour.b.ToString() + "):(" + newVest.spawn.x.ToString() + "," + newVest.spawn.y.ToString() + "):(" + newVest.target.x.ToString() + "," + newVest.target.y.ToString() + ")" + (newVest.karma ? ":K" : "") + "]");

			Dictionary<string, string> encodedSpawnData = new Dictionary<string, string>
			{
				{ "entry." + Options.EntryA.Value, newVest.room },
				{ "entry." + Options.EntryB.Value, newVest.region },
				{ "entry." + Options.EntryC.Value, newVest.colour.r.ToString() },
				{ "entry." + Options.EntryD.Value, newVest.colour.g.ToString() },
				{ "entry." + Options.EntryE.Value, newVest.colour.b.ToString() },
				{ "entry." + Options.EntryF.Value, newVest.spawn.x.ToString() },
				{ "entry." + Options.EntryG.Value, newVest.spawn.y.ToString() },
				{ "entry." + Options.EntryH.Value, newVest.target.x.ToString() },
				{ "entry." + Options.EntryI.Value, newVest.target.y.ToString() },
				{ "entry." + Options.EntryJ.Value, newVest.karma ? "Y" : "" }
			};

			httpClient.PostAsync("https://docs.google.com/forms/u/0/d/e/" + Options.UploadID.Value + "/formResponse", new FormUrlEncodedContent(encodedSpawnData));
		}

		private async void DownloadVestiges(bool firstRun) {
			if (!isDownloading && (firstRun || DateTime.Compare(DateTime.Now, nextDownload) > 0)) {
				Logger.LogDebug("Downloading vestiges...");

				isDownloading = true;
				Options.RefreshStatusAndButton();

				string rawDataset;
				try {
					rawDataset = await httpClient.GetStringAsync("https://docs.google.com/spreadsheet/ccc?key=" + Options.DownloadID.Value + "&output=csv");
				} catch (Exception err) {
					Logger.LogError("Download failed: " + err.Message);
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					Options.RefreshStatusAndButton();
					return;
				}
				Logger.LogDebug("Loading vestiges...");

				if (rawDataset == null || rawDataset == "") {
					Logger.LogError("rawDataset is either null or empty!");
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					Options.RefreshStatusAndButton();
					return;
				}

				string[] rawRows = rawDataset.Split('\n');
				if (rawRows.Length <= 0 || !rawRows[0].Trim('\r').StartsWith("Timestamp,room,region,colour.r,colour.g,colour.b,spawn.x,spawn.y,target.x,target.y,karma")) {
					Logger.LogError("rawDataset is not formatted correclty!");
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					Options.RefreshStatusAndButton();
					return;
				}

				ParseRawVestiges(rawRows);

				if (firstRun && Options.InfiniteLifespan.Value) {
					Logger.LogDebug("Downloading historical vestiges...");

					try {
						rawDataset = await httpClient.GetStringAsync(Options.ArchiveURL.Value);
					} catch (Exception err) {
						Logger.LogError("Download failed: " + err.Message);
						isDownloaded = false;
						isDownloading = false;
						Options.RefreshStatusAndButton();
						return;
					}
					Logger.LogDebug("Loading historical vestiges... (this will take a LONG time to process)");

					if (rawDataset == null || rawDataset == "") {
						Logger.LogError("rawDataset is either null or empty!");
						isDownloaded = false;
						isDownloading = false;
						Options.RefreshStatusAndButton();
						return;
					}

					rawRows = rawDataset.Split('\n');
					if (rawRows.Length <= 0 || !rawRows[0].Trim('\r').StartsWith("Timestamp,room,region,colour.r,colour.g,colour.b,spawn.x,spawn.y,target.x,target.y,karma")) {
						Logger.LogError("rawDataset is not formatted correclty!");
						isDownloaded = false;
						isDownloading = false;
						Options.RefreshStatusAndButton();
						return;
					}

					ParseRawVestiges(rawRows, true);
				}

				isDownloaded = true;
				isDownloading = false;
				nextDownload = DateTime.Now.AddMinutes(30);
			} else if (isDownloading) {
				Logger.LogWarning("Skipped download attempt: vestiges are still being downloaded!");
			} else {
				Logger.LogDebug("Skipped download attempt: it has been less than half an hour!");
			}
			Options.RefreshStatusAndButton();
		}

		private void ParseRawVestiges(string[] rawRows, bool printProgress = false) {
			int validEntries = 0;
			int totalEntries = rawRows.Length - 1;
			int newEntries = 0;
			for (int r = 1; r < rawRows.Length; r++) {
				if (printProgress && r % 10000 == 0) {
					Logger.LogDebug("Processed " + ((float)(r * 100) / rawRows.Length) + "%");
				}

				//[Timestamp, room, region, colour.r, colour.g, colour.b, spawn.x, spawn.y, target.x, target.y, karma]
				//[0        , 1   , 2     , 3       , 4       , 5       , 6      , 7      , 8       , 9       , 10   ]
				string[] currentValues = rawRows[r].Trim('\r').Split(',');
				if (rawRows[r].Trim('\r', ',', ' ') == "") {
					totalEntries--;
				} else if (currentValues.Length >= 11) {
					validEntries++;

					if (!vestigeData.ContainsKey(currentValues[2])) {
						vestigeData.Add(currentValues[2], new Dictionary<string, List<VestigeSpawn>>());
					}
					if (!vestigeData[currentValues[2]].ContainsKey(currentValues[1])) {
						vestigeData[currentValues[2]].Add(currentValues[1], new List<VestigeSpawn>());
					}

					Color currentColor = new Color(float.Parse(currentValues[3], NumberStyles.Float), float.Parse(currentValues[4], NumberStyles.Float), float.Parse(currentValues[5], NumberStyles.Float));
					VestigeCoord currentSpawn = new VestigeCoord(int.Parse(currentValues[6], NumberStyles.Integer | NumberStyles.AllowExponent), int.Parse(currentValues[7], NumberStyles.Integer | NumberStyles.AllowExponent));
					VestigeCoord currentTarget = new VestigeCoord(int.Parse(currentValues[8], NumberStyles.Integer | NumberStyles.AllowExponent), int.Parse(currentValues[9], NumberStyles.Integer | NumberStyles.AllowExponent));
					DateTime currentTimestamp = DateTime.SpecifyKind(DateTime.ParseExact(currentValues[0], "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Utc);

					if (!rawDownloads.Contains(rawRows[r].Trim('\r'))) {

						VestigeSpawn currentVestige = new VestigeSpawn(currentValues[2], currentValues[1], currentColor, currentSpawn, currentTarget, currentTimestamp, currentValues[10] == "Y");

						if (Options.InfiniteLifespan.Value || (DateTime.UtcNow - currentVestige.time).TotalHours <= Options.Lifespan.Value) {
							vestigeData[currentValues[2]][currentValues[1]].Add(currentVestige);
							vestigeCount++;
							if (currentVestige.karma) vestigeCountKarma++;
						}

						rawDownloads.Add(rawRows[r].Trim('\r'));
						newEntries++;
					}

				} else {
					Logger.LogError("skipped entry on row " + r + " due to invalid formatting!");
				}
			}
			vestigeCount -= localvestigeData.Count;
			vestigeCountKarma -= localvestigeData.FindAll(vestige => vestige.karma).Count;
			Logger.LogDebug(validEntries + "/" + totalEntries + " vestiges were downloaded (" + newEntries + " new, " + localvestigeData.Count + " (local) removed and " + vestigeCount + " loaded)");
			localvestigeData.Clear();
		}

		private void ClearVestiges() {
			Logger.LogDebug("Clearing all saved vestiges...");
			if (!isDownloading) {

				activeRooms.Clear();
				for (int i = activeVestigeList.Count - 1; i >= 0; i--) {
					if (!activeVestigeList[i].exists) {
						activeVestigeList.RemoveAt(i);
					}
				}
				ResetQueue();
				lastVestigeSpawns.Clear();

				//Might not be nessecary, but just incase this should help avoid memory leaks
				foreach (string currentRegion in vestigeData.Keys) {
					foreach (string currentRoom in vestigeData[currentRegion].Keys) {
						vestigeData[currentRegion][currentRoom].Clear();
					}
					vestigeData[currentRegion].Clear();
				}
				vestigeData.Clear();
				rawDownloads.Clear();
				localvestigeData.Clear();
				localDeathTimes.Clear();
				backupTargets.Clear();
				vestigeCount = 0;
				vestigeCountKarma = 0;
				isDownloaded = false;

				Logger.LogDebug("Cleared all vestiges");
			} else {
				Logger.LogWarning("Did not clear: vestiges are still being downloaded!");
			}
		}

		public void OnReloadButton(UIfocusable button) {
			button.greyedOut = true;
			ClearVestiges();
			Task.Run(() => DownloadVestiges(true));
		}
	}
}