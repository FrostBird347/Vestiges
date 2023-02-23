using BepInEx;
using System.Security.Permissions;
using System;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

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

		public void OnEnable() {
			// Add hooks here
			On.RainWorld.OnModsInit += Init;

			On.Player.Jump += SpawnFlyAtJump;
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

		private void SpawnFlyAtJump(On.Player.orig_Jump orig, Player self) {
			orig(self);

			Vector2 spawnPos = self.mainBodyChunk.pos + new Vector2(0f, (self.mainBodyChunk.rad * 2));
			new Vestige(self.room, spawnPos, new Color(0.25f, 0.75f, 1f, 0.25f));
		}
	}
}