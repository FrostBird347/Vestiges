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
		ConditionalWeakTable<Player, string> deathDict;
		List<FailedDeathData> failedDeathCoords;

		public void OnEnable() {
			// Add hooks here
			On.RainWorld.OnModsInit += Init;

			On.Player.NewRoom += SpawnVestiges;
			On.Player.Update += UpdateFly;
			On.Player.Die += OnDeath;
			On.Player.Grabbed += OnGrabDeath;
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
				deathDict = new ConditionalWeakTable<Player, string>();
				failedDeathCoords = new List<FailedDeathData>();

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
					Vestige newBug = new Vestige(newRoom, spawnInfo.spawn, spawnInfo.target, spawnInfo.colour, 1);
					newRoom.AddObject(newBug);
					activeVestigeList.Add(newBug);
				}
			}
		}

		private void UpdateFly(On.Player.orig_Update orig, Player self, bool eu) {
			orig(self, eu);

			for (int i = 0; i < activeVestigeList.Count; i++) {
				if (activeVestigeList[i] != null && activeVestigeList[i].exists) {
					activeVestigeList[i].Update(eu);
				} else {
					activeVestigeList[i] = null;
					activeVestigeList.RemoveAt(i);
					i--;
				}
			}
		}

		private void OnDeath(On.Player.orig_Die orig, Player self) {
			Logger.LogInfo("OnDeath");
			AddNewVestige(self, true);
			orig(self);
		}

		private void OnGrabDeath(On.Player.orig_Grabbed orig, Player self, Creature.Grasp grasp) {
			orig(self, grasp);
			Logger.LogInfo("Grabbed");
			Logger.LogInfo(grasp.pacifying);
			if (grasp.grabber.Template.IsLizard) {
				AddNewVestige(self, false);
			}
		}

		private void AddNewVestige(Player self, bool actuallyDead) {
			string lastTimeRaw;
			Logger.LogDebug("A");
			Logger.LogDebug(self);
			Logger.LogDebug(self.coord.Valid);
			Logger.LogDebug(self.lastCoord.Valid);
			Logger.LogDebug(self.room == null);
			if ((self.room == null) && failedDeathCoords.Count == 0) {
				Logger.LogDebug("bb");
				FailedDeathData newFail = new FailedDeathData(self.coord, self.karmaFlowerGrowPos.Value, self.ShortCutColor());
				Logger.LogDebug("cc");
				failedDeathCoords.Add(newFail);
				Logger.LogDebug(newFail.region);
				Logger.LogDebug("dd");
			} else if (self.room.world.game.IsStorySession) {
				Logger.LogDebug("b");
				if (!deathDict.TryGetValue(self, out lastTimeRaw) || ((DateTime.Now.ToFileTime() - long.Parse(lastTimeRaw)) / 10000000) > 15) {
					Logger.LogDebug("c");
					deathDict.Remove(self);
					deathDict.Add(self, DateTime.Now.ToFileTime().ToString());
					Logger.LogDebug("d");

					string roomName = self.room.abstractRoom.name;
					string regionName = self.room.world.region.name;
					Logger.LogDebug("e");

					if (!vestigeData.ContainsKey(regionName)) {
						vestigeData.Add(regionName, new Dictionary<string, List<VestigeSpawn>>());
					}
					Logger.LogDebug("f");
					if (!vestigeData[regionName].ContainsKey(roomName)) {
						vestigeData[regionName].Add(roomName, new List<VestigeSpawn>());
					}
					Logger.LogDebug("g");

					Vector2 safePos = self.mainBodyChunk.pos;
					Logger.LogDebug("h");
					if (self.karmaFlowerGrowPos.HasValue && self.karmaFlowerGrowPos.Value.Valid && self.room.abstractRoom.index == self.karmaFlowerGrowPos.Value.room) {
						safePos = self.room.MiddleOfTile(self.karmaFlowerGrowPos.Value.x, self.karmaFlowerGrowPos.Value.y);
					}
					Logger.LogDebug("i");
					VestigeSpawn newSpawn = new VestigeSpawn(roomName, regionName, self.ShortCutColor(), self.mainBodyChunk.pos, safePos);

					Logger.LogDebug("j");
					vestigeData[regionName][roomName].Add(newSpawn);

					Logger.LogDebug("l");
					Vestige newBug = new Vestige(self.room, newSpawn.spawn, newSpawn.target, newSpawn.colour, 2);
					Logger.LogDebug("m");
					self.room.AddObject(newBug);
					Logger.LogDebug("n");
					activeVestigeList.Add(newBug);
					Logger.LogDebug("o");

					Logger.LogDebug("Added new vestige at " + regionName + ":" + roomName + newSpawn.spawn + " targeting " + newSpawn.target);
				} else {
					Logger.LogDebug("Skipped adding additional vestige: the same player triggered this less than 15 seconds ago! (or they are already fully dead)");
				}
				if (actuallyDead) {
					Logger.LogDebug("p");
					deathDict.Remove(self);
					Logger.LogDebug("q");
					deathDict.Add(self, (DateTime.Now.ToFileTime() + ((long)10000000 * 3600)).ToString());
					Logger.LogDebug("r");
				}

			}
		}

	}

}
