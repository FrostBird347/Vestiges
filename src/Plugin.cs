﻿using BepInEx;
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

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Vestiges {

	[BepInPlugin("frostbird347.vestiges", "Vestiges", "0.9.2")]
	public sealed class Plugin : BaseUnityPlugin {
		bool init;
		private PluginOptions Options = null;
		bool configWorking = false;

		Dictionary<string, Dictionary<string, List<VestigeSpawn>>> vestigeData;
		List<string> rawDownloads;
		List<VestigeSpawn> localvestigeData;
		List<Vestige> activeVestigeList;
		string lastRoomName;
		List<VestigeSpawnQueue> vestigeSpawnQueue;
		int vestigeUploadLimiter;
		List<WorldCoordinate> lastVestigeSpawns;
		bool isStory;

		private static readonly HttpClient httpClient = new HttpClient();
		public static bool isDownloaded;
		public static int vestigeCount;

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
				init = true;

				vestigeData = new Dictionary<string, Dictionary<string, List<VestigeSpawn>>>();
				rawDownloads = new List<string>();
				localvestigeData = new List<VestigeSpawn>();
				activeVestigeList = new List<Vestige>();
				lastRoomName = "_";
				vestigeSpawnQueue = new List<VestigeSpawnQueue>();
				vestigeUploadLimiter = 150;
				lastVestigeSpawns = new List<WorldCoordinate>();
				isDownloaded = false;
				vestigeCount = 0;

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
					DownloadVestiges(true);
				} else {
					Logger.LogFatal("Config failed to load, this mod has somewhat disabled itself for safety!");

					On.Player.NewRoom -= SpawnVestiges;
					On.Player.Update -= UpdateFly;
					On.Player.Die -= OnDeath;
					On.Player.Grabbed -= OnGrabDeath;
					On.RainWorldGame.ctor -= StartCycle;
				}
			}
		}

		private void SpawnVestiges(On.Player.orig_NewRoom orig, Player self, Room newRoom) {
			orig(self, newRoom);

			string roomName = newRoom.abstractRoom.name;
			string regionName = roomName.Split('_')[0];

			if (roomName != lastRoomName) {
				lastRoomName = roomName;

				if (!self.dead && vestigeData.ContainsKey(regionName) && vestigeData[regionName].ContainsKey(roomName)) {
					for (int i = 0; i < vestigeData[regionName][roomName].Count && i < Options.VestigeLimit.Value; i++) {

						VestigeSpawn spawnInfo = vestigeData[regionName][roomName][i];

						int currentSize = 1;
						if ((DateTime.UtcNow - spawnInfo.time).TotalHours <= 24) {
							currentSize = 2;
						}

						Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), spawnInfo.spawn, spawnInfo.target, spawnInfo.colour, currentSize);
						newRoom.AddObject(newBug);
						activeVestigeList.Add(newBug);
					}
				}
				if (!self.dead) {
					for (int i = 0; i < localvestigeData.Count; i++) {
						if (localvestigeData[i].region == regionName && localvestigeData[i].room == roomName) {

							//Don't need to worry about the time because 24 hours will pretty much never pass between the start and end of a cycle
							Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), localvestigeData[i].spawn, localvestigeData[i].target, localvestigeData[i].colour, 2);
							newRoom.AddObject(newBug);
							activeVestigeList.Add(newBug);
						}
					}
				}
			}
		}

		private void UpdateFly(On.Player.orig_Update orig, Player self, bool eu) {
			orig(self, eu);

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
			if (self.room == null && actuallyDead && isStory) {

				WorldCoordinate safePos = self.coord;
				if (self.karmaFlowerGrowPos.HasValue && self.karmaFlowerGrowPos.Value.Valid && self.coord.room == self.karmaFlowerGrowPos.Value.room) {
					safePos = self.karmaFlowerGrowPos.Value;
				}

				VestigeSpawnQueue newSpawn = new VestigeSpawnQueue(self.coord, safePos, self.ShortCutColor());
				vestigeSpawnQueue.Add(newSpawn);

			} else if (self.room != null && self.room.world.game.IsStorySession) {

				WorldCoordinate safePos = self.coord;
				if (self.karmaFlowerGrowPos.HasValue && self.karmaFlowerGrowPos.Value.Valid && self.coord.room == self.karmaFlowerGrowPos.Value.room) {
					safePos = self.karmaFlowerGrowPos.Value;
				}

				VestigeSpawnQueue newSpawn = new VestigeSpawnQueue(self.coord, safePos, self.ShortCutColor());
				vestigeSpawnQueue.Add(newSpawn);

				AddNewVestige(self);
			}

		}

		private void AddNewVestige(Player self) {
			if (isStory && vestigeUploadLimiter <= 150) vestigeUploadLimiter++;
			if (isStory && vestigeSpawnQueue.Count != 0 && vestigeUploadLimiter >= 150) {
				int queueIndex = Random.Range(0, vestigeSpawnQueue.Count);
				bool skip = false;
				vestigeUploadLimiter = 0;

				if (!lastVestigeSpawns.Contains(vestigeSpawnQueue[queueIndex].safeCoord)) {


					VestigeSpawn newSpawn = new VestigeSpawn(vestigeSpawnQueue[queueIndex].room, vestigeSpawnQueue[queueIndex].region, vestigeSpawnQueue[queueIndex].colour, new VestigeCoord(vestigeSpawnQueue[queueIndex].coord), new VestigeCoord(vestigeSpawnQueue[queueIndex].safeCoord), DateTime.UtcNow);
					localvestigeData.Add(newSpawn);

					lastVestigeSpawns.Add(vestigeSpawnQueue[queueIndex].safeCoord);
					vestigeCount++;

					UploadVestige(newSpawn);

					if (self.room != null && self.room.abstractRoom.name == vestigeSpawnQueue[queueIndex].room) {
						Vestige newBug = new Vestige(self.room, new Vector2(0, 0), newSpawn.spawn, newSpawn.target, newSpawn.colour, 2);
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

			DownloadVestiges(false);
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

			lastRoomName = "_";
		}

		private void UploadVestige(VestigeSpawn newVest) {
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
			Logger.LogDebug("Downloading Vestiges...");

			string rawDataset = await httpClient.GetStringAsync("https://docs.google.com/spreadsheet/ccc?key=" + Options.DownloadID.Value + "&output=csv");
			if (rawDataset == null || rawDataset == "") {
				Logger.LogError("rawDataset is either null or empty!");
				if (firstRun) {
					isDownloaded = false;
				}
				return;
			}

			string[] rawRows = rawDataset.Split('\n');
			if (rawRows.Length <= 0 || !rawRows[0].Trim('\r').StartsWith("Timestamp,room,region,colour.r,colour.g,colour.b,spawn.x,spawn.y,target.x,target.y")) {
				Logger.LogError("rawDataset is not formatted correclty!");
				if (firstRun) {
					isDownloaded = false;
				}
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
						vestigeData[currentValues[2]][currentValues[1]].Add(currentVestige);
						vestigeCount++;

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
		}

		private void ClearVestiges() {
			Logger.LogDebug("Clearing all saved Vestiges...");

			lastRoomName = "_";
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
			vestigeCount = 0;

			Logger.LogDebug("Cleared all Vestiges");
		}

	}

}
