using BepInEx;
using System.Security.Permissions;
using System;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;
using System.Linq;
using static RoomCamera;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Globalization;
using System.Threading.Tasks;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Vestiges {

	[BepInPlugin("frostbird347.vestiges", "Vestiges", "0.10.3")]
	public sealed class Plugin : BaseUnityPlugin {
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
		private DateTime lastDev;

		bool isStory;

		private static readonly HttpClient httpClient = new HttpClient();
		public static bool isDownloading;
		public static bool isDownloaded;
		public static int vestigeCount;
		private DateTime nextDownload;
		int lastLifespan;

		public void OnEnable() {
			On.RainWorld.OnModsInit += Init;

			On.Player.NewRoom += SpawnVestiges;
			On.Player.Update += UpdateFly;
			On.Player.Die += OnDeath;
			On.Player.Grabbed += OnGrabDeath;
			On.RainWorldGame.ctor += StartCycle;
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
				lastDev = DateTime.Now.AddYears(-1);

				isStory = false;

				isDownloading = false;
				isDownloaded = false;
				vestigeCount = 0;
				nextDownload = DateTime.Now.AddYears(-1);
				lastLifespan = -1;

				try {
					Options = new PluginOptions(this, Logger);
					MachineConnector.SetRegisteredOI("frostbird347.vestiges", Options);
					configWorking = true;
				} catch (Exception err) {
					Logger.LogError(err);
					configWorking = false;
				}

				if (configWorking) {
					ClearVestiges();
					lastLifespan = Options.Lifespan.Value;
					Task.Run(() => DownloadVestiges(true));
				} else {
					Logger.LogFatal("Config failed to load, this mod has somewhat disabled itself for safety!");

					On.Player.NewRoom -= SpawnVestiges;
					On.Player.Update -= UpdateFly;
					On.Player.Die -= OnDeath;
					On.Player.Grabbed -= OnGrabDeath;
					On.RainWorldGame.ctor -= StartCycle;
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

						Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), spawnInfo.spawn, spawnInfo.target, spawnInfo.colour, currentSize, Options.VestigeLights.Value);
						newRoom.AddObject(newBug);
						activeVestigeList.Add(newBug);
					}
				}
				if (!self.dead) {
					for (int i = 0; i < localvestigeData.Count; i++) {
						if (localvestigeData[i].region == regionName && localvestigeData[i].room == roomName) {

							int currentSize = 1;
							if ((DateTime.UtcNow - localvestigeData[i].time).TotalHours <= Options.LargeHours.Value) {
								currentSize = 2;
							}

							Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), localvestigeData[i].spawn, localvestigeData[i].target, localvestigeData[i].colour, currentSize, Options.VestigeLights.Value);
							newRoom.AddObject(newBug);
							activeVestigeList.Add(newBug);
						}
					}
				}
			}
		}

		private void UpdateFly(On.Player.orig_Update orig, Player self, bool eu) {
			orig(self, eu);

			if (self.IsJollyPlayer || !self.isSlugpup) {
				for (int i = activeVestigeList.Count - 1; i >= 0; i--) {
					if (activeVestigeList[i] != null && activeVestigeList[i].exists) {
						activeVestigeList[i].Update(eu);
					} else {
						activeVestigeList[i] = null;
						activeVestigeList.RemoveAt(i);
					}
				}

				if (self.room != null) {
					isStory = self.room.world.game.IsStorySession;
					AddNewVestige(self);
				}

				if (self.room != null && self.room.world.game.devToolsActive) {
					lastDev = DateTime.Now.AddMinutes(5);
				}

				if (self.lowerBodyFramesOnGround > 0 && !self.dead && !self.Stunned && self.grabbedBy.Count == 0) {
					backupTargets.Remove(self.playerState.playerNumber);
					backupTargets.Add(self.playerState.playerNumber, self.coord);
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

			if (isStory && (!self.isSlugpup || self.IsJollyPlayer) && !self.isNPC && (!localDeathTimes.ContainsKey(self.playerState.playerNumber) || (DateTime.Now - localDeathTimes[self.playerState.playerNumber]).TotalSeconds >= 10)) {
				localDeathTimes.Remove(self.playerState.playerNumber);
				localDeathTimes.Add(self.playerState.playerNumber, DateTime.Now);

				WorldCoordinate safePos = self.coord;
				if (self.karmaFlowerGrowPos.HasValue && self.karmaFlowerGrowPos.Value.Valid && self.coord.room == self.karmaFlowerGrowPos.Value.room) {
					safePos = self.karmaFlowerGrowPos.Value;
				} else if (backupTargets.ContainsKey(self.playerState.playerNumber) && backupTargets[self.playerState.playerNumber].Valid && backupTargets[self.playerState.playerNumber].room == self.coord.room) {
					safePos = backupTargets[self.playerState.playerNumber];
				}

				VestigeSpawnQueue newSpawn = new VestigeSpawnQueue(self.coord, safePos, self.ShortCutColor());
				vestigeSpawnQueue.Add(newSpawn);

				if (self.room != null) {
					AddNewVestige(self);
				}

			}

		}

		private void AddNewVestige(Player self) {
			if (isStory && vestigeUploadLimiter <= 150) vestigeUploadLimiter++;
			if (isStory && vestigeSpawnQueue.Count != 0 && vestigeUploadLimiter >= 150) {
				int queueIndex = Random.Range(0, vestigeSpawnQueue.Count);
				bool skip = false;
				vestigeUploadLimiter = 0;

				if (!lastVestigeSpawns.Contains(vestigeSpawnQueue[queueIndex].safeCoord)) {


					VestigeSpawn newSpawn = new VestigeSpawn(vestigeSpawnQueue[queueIndex].room, vestigeSpawnQueue[queueIndex].region, vestigeSpawnQueue[queueIndex].colour, new VestigeCoord(vestigeSpawnQueue[queueIndex].coord), new VestigeCoord(vestigeSpawnQueue[queueIndex].safeCoord), DateTime.UtcNow); ;
					localvestigeData.Add(newSpawn);

					lastVestigeSpawns.Add(vestigeSpawnQueue[queueIndex].safeCoord);
					vestigeCount++;

					if (DateTime.Compare(DateTime.Now, lastDev) > 0) {
						UploadVestige(newSpawn);
					} else {
						Logger.LogWarning("Sorry but to slightly lower the amount of vestiges being mass spawned, devtools disables uploading for a while.");
						Logger.LogWarning("While I do expect people to easily get around this, I hope that it will slightly lower the rate of new vestiges being mass spawned in single rooms to a rate where I won't need to lower their lifetime.");
						Logger.LogWarning("I will likely add a way to disable this once the vestige creation rate stabilizes (or remove it completely), especially since you can now lower the Vestige lifespan in the config yourself");
					}

					if (self.room != null && self.room.abstractRoom.name == vestigeSpawnQueue[queueIndex].room) {
						Vestige newBug = new Vestige(self.room, new Vector2(0, 0), newSpawn.spawn, newSpawn.target, newSpawn.colour, 2, Options.VestigeLights.Value);
						self.room.AddObject(newBug);
						activeVestigeList.Add(newBug);
					}
				}

				if (!skip) {
					vestigeSpawnQueue.RemoveAt(queueIndex);
				}
			}
		}

		private void StartCycle(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager) {
			orig(self, manager);

			activeRooms.Clear();
			if (lastLifespan != Options.Lifespan.Value) {
				Logger.LogDebug("Vestige lifespan has been changed, clearing and redownloading vestiges...");
				ClearVestiges();
				lastLifespan = Options.Lifespan.Value;
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
			Logger.LogDebug("Attempting to upload vestige... [" + newVest.room + ":" + newVest.region + ":(" + newVest.colour.r.ToString() + "," + newVest.colour.g.ToString() + "," + newVest.colour.b.ToString() + "):(" + newVest.spawn.x.ToString() + "," + newVest.spawn.y.ToString() + "):(" + newVest.target.x.ToString() + "," + newVest.target.y.ToString() + ")]");

			Dictionary<string, string> encodedSpawnData = new Dictionary<string, string>();
			encodedSpawnData.Add("entry." + Options.EntryA.Value, newVest.room);
			encodedSpawnData.Add("entry." + Options.EntryB.Value, newVest.region);
			encodedSpawnData.Add("entry." + Options.EntryC.Value, newVest.colour.r.ToString());
			encodedSpawnData.Add("entry." + Options.EntryD.Value, newVest.colour.g.ToString());
			encodedSpawnData.Add("entry." + Options.EntryE.Value, newVest.colour.b.ToString());
			encodedSpawnData.Add("entry." + Options.EntryF.Value, newVest.spawn.x.ToString());
			encodedSpawnData.Add("entry." + Options.EntryG.Value, newVest.spawn.y.ToString());
			encodedSpawnData.Add("entry." + Options.EntryH.Value, newVest.target.x.ToString());
			encodedSpawnData.Add("entry." + Options.EntryI.Value, newVest.target.y.ToString());

			httpClient.PostAsync("https://docs.google.com/forms/u/0/d/e/" + Options.UploadID.Value + "/formResponse", new FormUrlEncodedContent(encodedSpawnData));
		}

		private async void DownloadVestiges(bool firstRun) {
			if (!isDownloading && (firstRun || DateTime.Compare(DateTime.Now, nextDownload) > 0)) {
				Logger.LogDebug("Downloading Vestiges...");

				if (firstRun) {
					isDownloading = true;
				}

				string rawDataset = "";
				try {
					string downloadingDataset = await httpClient.GetStringAsync("https://docs.google.com/spreadsheet/ccc?key=" + Options.DownloadID.Value + "&output=csv");
					rawDataset = downloadingDataset;
				} catch (Exception err) {
					Logger.LogError("Download failed: " + err.Message);
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					return;
				}
				Logger.LogDebug("Loading Vestiges...");

				if (rawDataset == null || rawDataset == "") {
					Logger.LogError("rawDataset is either null or empty!");
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					return;
				}

				string[] rawRows = rawDataset.Split('\n');
				if (rawRows.Length <= 0 || !rawRows[0].Trim('\r').StartsWith("Timestamp,room,region,colour.r,colour.g,colour.b,spawn.x,spawn.y,target.x,target.y")) {
					Logger.LogError("rawDataset is not formatted correclty!");
					if (firstRun) {
						isDownloaded = false;
					}
					isDownloading = false;
					return;
				}

				int validEntries = 0;
				int totalEntries = rawRows.Length - 1;
				int newEntries = 0;
				for (int r = 1; r < rawRows.Length; r++) {
					//[Timestamp, room, region, colour.r, colour.g, colour.b, spawn.x, spawn.y, target.x, target.y]
					//[0        , 1   , 2     , 3       , 4       , 5       , 6      , 7      , 8       , 9       ]
					string[] currentValues = rawRows[r].Trim('\r').Split(',');
					if (currentValues == new string[] { "", "", "", "", "", "", "", "", "", "" }) {
						totalEntries--;
					} else if (currentValues.Length >= 10) {
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

							VestigeSpawn currentVestige = new VestigeSpawn(currentValues[2], currentValues[1], currentColor, currentSpawn, currentTarget, currentTimestamp);

							if ((DateTime.UtcNow - currentVestige.time).TotalHours <= Options.Lifespan.Value) {
								vestigeData[currentValues[2]][currentValues[1]].Add(currentVestige);
								vestigeCount++;
							}

							rawDownloads.Add(rawRows[r].Trim('\r'));
							newEntries++;
						}

					} else {
						Logger.LogError("skipped entry on row " + r + " due to invalid formatting!");
					}
				}
				vestigeCount -= localvestigeData.Count;
				Logger.LogDebug(validEntries + "/" + (totalEntries) + " Vestiges were downloaded (" + newEntries + " new, " + localvestigeData.Count + " (local) removed and " + vestigeCount + " loaded)");
				localvestigeData.Clear();

				isDownloaded = true;
				isDownloading = false;
				nextDownload = DateTime.Now.AddMinutes(30);
			} else if (isDownloading) {
				Logger.LogWarning("Skipped download attempt: Vestiges are still being downloaded!");
			} else {
				Logger.LogDebug("Skipped download attempt: it has been less than half an hour!");
			}
		}

		private void ClearVestiges() {
			Logger.LogDebug("Clearing all saved Vestiges...");
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

				Logger.LogDebug("Cleared all Vestiges");
			} else {
				Logger.LogWarning("Did not clear: Vestiges are still being downloaded!");
			}
		}

	}

}
