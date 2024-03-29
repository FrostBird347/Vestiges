﻿using System;
using System.Security.Policy;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace Vestiges {

	public class PluginOptions : OptionInterface {
		private readonly ManualLogSource Logger;

		public readonly Configurable<int> VestigeLimit;
		public readonly Configurable<int> LargeHours;
		public readonly Configurable<int> Lifespan;

		public readonly Configurable<bool> VestigeLights;
		public readonly Configurable<bool> StealthMode;
		public readonly Configurable<bool> InfiniteLifespan;

		public readonly Configurable<string> DownloadID;
		public readonly Configurable<string> UploadID;

		public readonly Configurable<string> EntryA;
		public readonly Configurable<string> EntryB;
		public readonly Configurable<string> EntryC;
		public readonly Configurable<string> EntryD;
		public readonly Configurable<string> EntryE;
		public readonly Configurable<string> EntryF;
		public readonly Configurable<string> EntryG;
		public readonly Configurable<string> EntryH;
		public readonly Configurable<string> EntryI;

		public readonly Configurable<string> ArchiveURL;

		private UIelement[] UIArrPlayerOptions;
		private UIelement[] UIArrPlayerOptionsTwo;

		public PluginOptions(Plugin pluginInstance, ManualLogSource logSource) {
			Logger = logSource;
			VestigeLimit = config.Bind("VestigeLimit", 50, new ConfigAcceptableRange<int>(1, 999999));
			LargeHours = config.Bind("LargeHours", 24, new ConfigAcceptableRange<int>(0, 720));
			Lifespan = config.Bind("Lifespan", 96, new ConfigAcceptableRange<int>(0, 720));

			VestigeLights = config.Bind("VestigeLights", true);
			StealthMode = config.Bind("StealthMode", false);
			InfiniteLifespan = config.Bind("InfiniteLifespan", false);

			DownloadID = config.Bind("DownloadID", "1mUk-KQp7Kv4U-ODamQwb7DUWNewvyXLucVu72bVqFZU");
			UploadID = config.Bind("UploadID", "1FAIpQLSdkBHGRNMbJQGJ0A89CJfDrA98uy1DBL3VQuys9s91i41P1JA");

			EntryA = config.Bind("EntryA", "46667845");
			EntryB = config.Bind("EntryB", "799920119");
			EntryC = config.Bind("EntryC", "2120884595");
			EntryD = config.Bind("EntryD", "559370072");
			EntryE = config.Bind("EntryE", "1818183584");
			EntryF = config.Bind("EntryF", "685257973");
			EntryG = config.Bind("EntryG", "622593087");
			EntryH = config.Bind("EntryH", "1964557942");
			EntryI = config.Bind("EntryI", "787154321");

			ArchiveURL = config.Bind("ArchiveURL", "https://raw.githubusercontent.com/FrostBird347/VestigeBackup/master/VestigeBackup.csv");

		}

		public override void Initialize() {
			OpSimpleButton RefreshVestiges = new OpSimpleButton(new Vector2(10f, 490f), new Vector2(125f, 10f), "Reload Vestiges") { description = "Clear and redownload all Vestiges" };

			//https://github.com/FrostBird347/Vestiges/issues/3
			//	RefreshVestiges.OnClick += Vestiges.Plugin.DownloadVestiges;
			RefreshVestiges.greyedOut = true;

			OpLabel VestigeStatus = new OpLabel(150F, 490f, "Failed to download Vestiges!");
			if (Vestiges.Plugin.isDownloading) VestigeStatus.text = "Vestiges are still downloading...";
			if (Vestiges.Plugin.isDownloaded) VestigeStatus.text = "Vestiges have been downloaded (" + Vestiges.Plugin.vestigeCount + " loaded)";

			OpTab opTab = new OpTab(this, "Options");
			OpTab opTabTwo = new OpTab(this, "Upload/Download Settings");
			this.Tabs = new[]
			{
				opTab,
				opTabTwo
			};

			UIArrPlayerOptions = new UIelement[]
			{
				new OpLabel(10f, 550f, "Options", true),

				new OpLabel(10f, 490f, "Vestige Limit"),
				new OpUpdown(VestigeLimit, new Vector2(90f, 490f), 75) { description = "Maximum number of Vestiges that can be placed in a single room" },
				new OpLabel(180f, 490f, "Large Vestige Timeout"),
				new OpUpdown(LargeHours, new Vector2(310f, 490f), 75) { description = "How many hours Vestiges will be twice as large for" },
				new OpLabel(400f, 490f, "Vestige Timeout"),
				new OpUpdown(Lifespan, new Vector2(500f, 490f), 75) { description = "How many hours Vestiges exist for" },

				new OpLabel(10f, 460f, "Vestige Lights"),
				new OpCheckBox(VestigeLights, 90f, 460f) { description = "Vestiges will produce a small amount of light" },
				new OpLabel(130f, 460f, "Stealth Mode"),
				new OpCheckBox(StealthMode, 210f, 460f) { description = "Prevents any new vestiges from being uploaded. They will still appear locally until the cache is reset." },
				new OpLabel(250f, 460f, "Remove Timeout"),
				new OpCheckBox(InfiniteLifespan, 350f, 460f) { description = "Remove Vestige timeout and download historical Vistages from a seperate online backup. This option is disabled by default for a reason, you have been warned!" }
			};
			opTab.AddItems(UIArrPlayerOptions);

			UIArrPlayerOptionsTwo = new UIelement[]
			{
				new OpLabel(10f, 550f, "Upload/Downlload Options", true),
				new OpLabel(290f, 550f, "(Only change these if you know what you are doing!)"),

				RefreshVestiges,
				VestigeStatus,

				new OpLabel(10f, 430f, "Download ID"),
				new OpTextBox(DownloadID, new Vector2(200f,430f), 400f) { description = "" },
				new OpLabel(10f, 400f, "Upload ID"),
				new OpTextBox(UploadID, new Vector2(200f,400f), 400f) { description = "" },
				new OpLabel(10f, 370f, "entry1 ID"),
				new OpTextBox(EntryA, new Vector2(200f,370f), 400f) { description = "Room" },
				new OpLabel(10f, 340f, "entry2 ID"),
				new OpTextBox(EntryB, new Vector2(200f,340f), 400f) { description = "Region" },
				new OpLabel(10f, 310f, "entry3 ID"),
				new OpTextBox(EntryC, new Vector2(200f,310f), 400f) { description = "Red" },
				new OpLabel(10f, 280f, "entry4 ID"),
				new OpTextBox(EntryD, new Vector2(200f,280f), 400f) { description = "Green" },
				new OpLabel(10f, 250f, "entry5 ID"),
				new OpTextBox(EntryE, new Vector2(200f,250f), 400f) { description = "Blue" },
				new OpLabel(10f, 220f, "entry6 ID"),
				new OpTextBox(EntryF, new Vector2(200f,220f), 400f) { description = "Spawn X" },
				new OpLabel(10f, 190f, "entry7 ID"),
				new OpTextBox(EntryG, new Vector2(200f,190f), 400f) { description = "Spawn Y" },
				new OpLabel(10f, 160f, "entry8 ID"),
				new OpTextBox(EntryH, new Vector2(200f,160f), 400f) { description = "Target X" },
				new OpLabel(10f, 130f, "entry9 ID"),
				new OpTextBox(EntryI, new Vector2(200f,130f), 400f) { description = "Target Y" },

				new OpLabel(10f, 100f, "Archive URL"),
				//Unfortunately the size of the text box determines the string's size limit, so to work around that issue it's set to be ridiculously large
				//There probably is some other form of input for long text... but I don't really have the time to figure that out right now
				new OpTextBox(ArchiveURL, new Vector2(200f, 100f), 4000f) { description = "Where historical Vestiges are downloaded" }
			};
			opTabTwo.AddItems(UIArrPlayerOptionsTwo);
		}
	}
}