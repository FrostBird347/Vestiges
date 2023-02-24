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
		List<Vestige> vestigeList = new List<Vestige>();

		public void OnEnable() {
			// Add hooks here
			On.RainWorld.OnModsInit += Init;

			On.Player.MovementUpdate += SpawnFly;
			On.Player.Update += UpdateFly;
		}

		public void OnDisable() {
			Logger.LogInfo("Disabled plugin!");
			//Vestige
		}

		private void Init(On.RainWorld.orig_OnModsInit orig, RainWorld self) {
			orig(self);

			if (!init) {
				init = true;

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

		private void SpawnFly(On.Player.orig_MovementUpdate orig, Player self, bool eu) {
			orig(self, eu);
			if (self.FoodInStomach > 0 && Random.value > 0.75f) {
				Vector2 spawnPos = self.mainBodyChunk.pos + new Vector2(0f, (self.mainBodyChunk.rad * 2));
				Vestige newBug = new Vestige(self.room, spawnPos, new Color(Random.value, Random.value, Random.value), 1);
				//newBug.SetupLogger(Logger);
				self.room.AddObject(newBug);
				vestigeList.Add(newBug);
			}
		}

		private void UpdateFly(On.Player.orig_Update orig, Player self, bool eu) {
			orig(self, eu);

			for (int i = 0; i < vestigeList.Count; i++) {
				if (vestigeList[i] != null && vestigeList[i].exists) {
					vestigeList[i].Update(eu);
				} else {
					vestigeList[i] = null;
					vestigeList.RemoveAt(i);
					i--;
				}
			}
		}

	}
}