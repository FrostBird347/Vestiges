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

		public void OnEnable() {
			// Add hooks here
			On.RainWorld.OnModsInit += Init;

			On.Player.NewRoom += SpawnVestiges;
			On.Player.Update += UpdateFly;
			On.Player.Die += OnDeath;
			On.Player.Grabbed += OnGrabDeath;
			On.RainWorldGame.GoToDeathScreen += ResetQueue;
			//On.Player.PermaDie;
		}

		public void OnDisable() {
			Logger.LogInfo("Disabled plugin!");
			//Vestige
		}

		private void Init(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
			orig(self);

			if (!init) {
				init = true;

				vestigeData = new Dictionary<string, Dictionary<string, List<VestigeSpawn>>>();
				activeVestigeList = new List<Vestige>();
				vestigeSpawnQueue = new List<VestigeSpawnQueue>();
				lastVestigeSpawns = new List<WorldCoordinate>();

				DownloadVestiges();

				try {
					Options = new PluginOptions(this, Logger);
					MachineConnector.SetRegisteredOI("frostbird347.vestiges", Options);
					configWorking = true;
				} catch (Exception err) {
					Logger.LogError(err);
					configWorking = false;
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
					Vestige newBug = new Vestige(newRoom, new Vector2(0, 0), spawnInfo.spawn, spawnInfo.target, spawnInfo.colour, 1, Logger);
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
			Logger.LogInfo("OnDeath");
			QueueNewVestige(self, true);
			orig(self);
		}

		private void OnGrabDeath(On.Player.orig_Grabbed orig, Player self, Creature.Grasp grasp) {
			orig(self, grasp);
			Logger.LogInfo("Grabbed");
			Logger.LogInfo(grasp.pacifying);
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
				Logger.LogDebug("CheckNewVestige");
				Logger.LogDebug(vestigeSpawnQueue.Count);
				if (!lastVestigeSpawns.Contains(vestigeSpawnQueue[queueIndex].safeCoord)) {
					Logger.LogDebug("AddNewVestige");
					Logger.LogDebug(vestigeSpawnQueue[queueIndex].room);
					Logger.LogDebug(vestigeSpawnQueue[queueIndex].region);

					if (!vestigeData.ContainsKey(vestigeSpawnQueue[queueIndex].region)) {
						vestigeData.Add(vestigeSpawnQueue[queueIndex].region, new Dictionary<string, List<VestigeSpawn>>());
					}

					if (!vestigeData[vestigeSpawnQueue[queueIndex].region].ContainsKey(vestigeSpawnQueue[queueIndex].room)) {
						vestigeData[vestigeSpawnQueue[queueIndex].region].Add(vestigeSpawnQueue[queueIndex].room, new List<VestigeSpawn>());
					}

					VestigeSpawn newSpawn = new VestigeSpawn(vestigeSpawnQueue[queueIndex].room, vestigeSpawnQueue[queueIndex].region, vestigeSpawnQueue[queueIndex].colour, vestigeSpawnQueue[queueIndex].coord, vestigeSpawnQueue[queueIndex].safeCoord);
					vestigeData[vestigeSpawnQueue[queueIndex].region][vestigeSpawnQueue[queueIndex].room].Add(newSpawn);

					lastVestigeSpawns.Add(vestigeSpawnQueue[queueIndex].safeCoord);

					UploadVestige(newSpawn);

					if (self.room != null && self.room.abstractRoom.name == vestigeSpawnQueue[queueIndex].room) {
						Logger.LogDebug("SpawnNewVestige");
						Vestige newBug = new Vestige(self.room, new Vector2(0, 0), newSpawn.spawn, newSpawn.target, newSpawn.colour, 2, Logger);
						self.room.AddObject(newBug);
						activeVestigeList.Add(newBug);
					}
				}

				if (!skip) {
					vestigeSpawnQueue.RemoveAt(queueIndex);
				}
			} else if (!isStory && lastVestigeSpawns.Count != 0) {
				Logger.LogDebug("ClearLastVestigeSpawns");
				Logger.LogDebug(lastVestigeSpawns.Count);
				lastVestigeSpawns.Clear();
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
		}

		private void DownloadVestiges() { }

	}

}
