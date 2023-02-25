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

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Vestiges {

	[BepInPlugin("frostbird347.vestiges", "Vestiges", "0.1.0")]
	public sealed class Plugin : BaseUnityPlugin {
		bool init;
		private PluginOptions Options = null;
		bool configWorking = false;

		Dictionary<string, Dictionary<string, List<VestigeSpawn>>> vestigeData;
		List<Vestige> activeVestigeList;
		List<VestigeSpawnQueue> vestigeSpawnQueue;
		List<WorldCoordinate> lastVestigeSpawns;
		bool isStory;

		private static readonly HttpClient httpClient = new HttpClient();
		bool isDownloaded;

		public void OnEnable() {
			On.RainWorld.OnModsInit += Init;

			On.Player.NewRoom += SpawnVestiges;
			On.Player.Update += UpdateFly;
			On.Player.Die += OnDeath;
			On.Player.Grabbed += OnGrabDeath;
			On.RainWorldGame.GoToDeathScreen += ResetQueue;
		}

		private void Init(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
			orig(self);

			if (!init) {
				init = true;

				vestigeData = new Dictionary<string, Dictionary<string, List<VestigeSpawn>>>();
				activeVestigeList = new List<Vestige>();
				vestigeSpawnQueue = new List<VestigeSpawnQueue>();
				lastVestigeSpawns = new List<WorldCoordinate>();
				isDownloaded = false;

				try {
					Options = new PluginOptions(this, Logger);
					MachineConnector.SetRegisteredOI("frostbird347.vestiges", Options);
					configWorking = true;
				} catch (Exception err) {
					Logger.LogError(err);
					configWorking = false;
				}

				if (configWorking) {
					DownloadVestiges();
				}
			}
		}

		private void SpawnVestiges(On.Player.orig_NewRoom orig, Player self, Room newRoom) {
			orig(self, newRoom);

			string roomName = newRoom.abstractRoom.name;
			string regionName = newRoom.world.region.name;

			if (!self.dead && vestigeData.ContainsKey(regionName) && vestigeData[regionName].ContainsKey(roomName)) {
				for (int i = 0; i < vestigeData[regionName][roomName].Count; i++) {

					VestigeSpawn spawnInfo = vestigeData[regionName][roomName][i];
					Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), spawnInfo.spawn, spawnInfo.target, spawnInfo.colour, 1);
					newRoom.AddObject(newBug);
					activeVestigeList.Add(newBug);
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
			if (isStory && vestigeSpawnQueue.Count != 0) {
				int queueIndex = Random.Range(0, vestigeSpawnQueue.Count);
				bool skip = false;

				if (!lastVestigeSpawns.Contains(vestigeSpawnQueue[queueIndex].safeCoord)) {

					if (!vestigeData.ContainsKey(vestigeSpawnQueue[queueIndex].region)) {
						vestigeData.Add(vestigeSpawnQueue[queueIndex].region, new Dictionary<string, List<VestigeSpawn>>());
					}
					if (!vestigeData[vestigeSpawnQueue[queueIndex].region].ContainsKey(vestigeSpawnQueue[queueIndex].room)) {
						vestigeData[vestigeSpawnQueue[queueIndex].region].Add(vestigeSpawnQueue[queueIndex].room, new List<VestigeSpawn>());
					}

					VestigeSpawn newSpawn = new VestigeSpawn(vestigeSpawnQueue[queueIndex].room, vestigeSpawnQueue[queueIndex].region, vestigeSpawnQueue[queueIndex].colour, new VestigeCoord(vestigeSpawnQueue[queueIndex].coord), new VestigeCoord(vestigeSpawnQueue[queueIndex].safeCoord));
					vestigeData[vestigeSpawnQueue[queueIndex].region][vestigeSpawnQueue[queueIndex].room].Add(newSpawn);

					lastVestigeSpawns.Add(vestigeSpawnQueue[queueIndex].safeCoord);

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

		private void ResetQueue(On.RainWorldGame.orig_GoToDeathScreen orig, RainWorldGame self) {
			orig(self);

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
		}

		private void UploadVestige(VestigeSpawn newVest) {
			Logger.LogDebug("UploadVestige");
			Logger.LogDebug(newVest.colour);
			Logger.LogDebug(newVest.room);
			Logger.LogDebug(newVest.region);
			Logger.LogDebug(newVest.spawn);
			Logger.LogDebug(newVest.target);

			Dictionary<string, string> encodedSpawnData = new Dictionary<string, string>();
			encodedSpawnData.Add("entry.46667845", newVest.room);
			encodedSpawnData.Add("entry.799920119", newVest.region);
			encodedSpawnData.Add("entry.2120884595", newVest.colour.r.ToString());
			encodedSpawnData.Add("entry.559370072", newVest.colour.g.ToString());
			encodedSpawnData.Add("entry.1818183584", newVest.colour.b.ToString());
			encodedSpawnData.Add("entry.685257973", newVest.spawn.x.ToString());
			encodedSpawnData.Add("entry.622593087", newVest.spawn.y.ToString());
			encodedSpawnData.Add("entry.1964557942", newVest.target.x.ToString());
			encodedSpawnData.Add("entry.787154321", newVest.target.y.ToString());

			httpClient.PostAsync("https://docs.google.com/forms/u/0/d/e/" + Options.UploadID.Value + "/formResponse", new FormUrlEncodedContent(encodedSpawnData));
		}

		private async void DownloadVestiges() {
			//ClearVestiges();

			string rawDataset = await httpClient.GetStringAsync("https://docs.google.com/spreadsheet/ccc?key=" + Options.DownloadID.Value + "&output=csv");
			if (rawDataset == null || rawDataset == "") {
				Logger.LogError("[DownloadVestiges] rawDataset is either null or empty!");
				isDownloaded = false;
			}

			string[] rawRows = rawDataset.Split('\n');
			if (rawRows.Length <= 0 || rawRows[0] != "Timestamp,room,region,colour.r,colour.g,colour.b,spawn.x,spawn.y,target.x,target.y\r") {
				isDownloaded = false;
			}

			int validEntries = 0;
			for (int r = 1; r < rawRows.Length; r++) {
				//[Timestamp, room, region, colour.r, colour.g, colour.b, spawn.x, spawn.y, target.x, target.y]
				//[0        , 1   , 2     , 3       , 4       , 5       , 6      , 7      , 8       , 9       ]
				string[] currentValues = rawRows[r].Trim('\r').Split(',');

				if (currentValues.Length == 10) {
					validEntries++;

					if (!vestigeData.ContainsKey(currentValues[2])) {
						vestigeData.Add(currentValues[2], new Dictionary<string, List<VestigeSpawn>>());
					}
					if (!vestigeData[currentValues[2]].ContainsKey(currentValues[1])) {
						vestigeData[currentValues[2]].Add(currentValues[1], new List<VestigeSpawn>());
					}

					Color currentColor = new Color(float.Parse(currentValues[3]), float.Parse(currentValues[4]), float.Parse(currentValues[5]));
					VestigeCoord currentSpawn = new VestigeCoord(int.Parse(currentValues[6]), int.Parse(currentValues[7]));
					VestigeCoord currentTarget = new VestigeCoord(int.Parse(currentValues[8]), int.Parse(currentValues[9]));

					VestigeSpawn currentVestige = new VestigeSpawn(currentValues[2], currentValues[1], currentColor, currentSpawn, currentTarget);
					vestigeData[currentValues[2]][currentValues[1]].Add(currentVestige);

				} else {
					Logger.LogError("[DownloadVestiges] skipped entry on row " + r + " due to invalid formatting!");
				}
			}
			Logger.LogInfo(validEntries + "/" + (rawRows.Length - 1) + " Vestiges were loaded");

			isDownloaded = true;
		}

	}

}
