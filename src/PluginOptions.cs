using System;
using System.Security.Policy;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace Vestiges {

	public class PluginOptions : OptionInterface {
		private readonly ManualLogSource Logger;

		public readonly Configurable<int> VestigeLimit;
		public readonly Configurable<string> UploadID;
		public readonly Configurable<string> DownloadID;

		public readonly Configurable<string> EntryA;
		public readonly Configurable<string> EntryB;
		public readonly Configurable<string> EntryC;
		public readonly Configurable<string> EntryD;
		public readonly Configurable<string> EntryE;
		public readonly Configurable<string> EntryF;
		public readonly Configurable<string> EntryG;
		public readonly Configurable<string> EntryH;
		public readonly Configurable<string> EntryI;

		private UIelement[] UIArrPlayerOptions;

		public PluginOptions(Plugin pluginInstance, ManualLogSource logSource) {
			Logger = logSource;
			VestigeLimit = config.Bind("VestigeLimit", 50, new ConfigAcceptableRange<int>(1, 5000));
			UploadID = config.Bind("UploadID", "1FAIpQLSdkBHGRNMbJQGJ0A89CJfDrA98uy1DBL3VQuys9s91i41P1JA");
			DownloadID = config.Bind("DownloadID", "1mUk-KQp7Kv4U-ODamQwb7DUWNewvyXLucVu72bVqFZU");

			EntryA = config.Bind("EntryA", "46667845");
			EntryB = config.Bind("EntryB", "799920119");
			EntryC = config.Bind("EntryC", "2120884595");
			EntryD = config.Bind("EntryD", "559370072");
			EntryE = config.Bind("EntryE", "1818183584");
			EntryF = config.Bind("EntryF", "685257973");
			EntryG = config.Bind("EntryG", "622593087");
			EntryH = config.Bind("EntryH", "1964557942");
			EntryI = config.Bind("EntryI", "787154321");

		}

		public override void Initialize() {
			OpSimpleButton RefreshVestiges = new OpSimpleButton(new Vector2(10f, 460f), new Vector2(125f, 10f), "Reload Vestiges") { description = "Clear and redownload all Vestiges" };

			//https://github.com/FrostBird347/Vestiges/issues/3
			//	RefreshVestiges.OnClick += Vestiges.Plugin.DownloadVestiges;
			RefreshVestiges.greyedOut = true;

			OpLabel VestigeStatus = new OpLabel(150F, 460f, "Failed to download Vestiges!");
			if (Vestiges.Plugin.isDownloaded) VestigeStatus.text = "Vestiges have been downloaded";

			OpTab opTab = new OpTab(this, "Options");
			this.Tabs = new[]
			{
				opTab
			};

			UIArrPlayerOptions = new UIelement[]
			{
				new OpLabel(10f, 550f, "Options", true),
				new OpLabel(10f, 490f, "Vestige Limit"),
				new OpUpdown(VestigeLimit, new Vector2(110f, 490f), 75) { description = "Maximum number of Vestiges that can be placed in a single room" },
				RefreshVestiges,
				VestigeStatus,

				new OpLabel(10f, 400f, "Upload ID"),
				new OpTextBox(UploadID, new Vector2(200f,400f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 370f, "Download ID"),
				new OpTextBox(DownloadID, new Vector2(200f,370f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 340f, "entry1 ID"),
				new OpTextBox(EntryA, new Vector2(200f,340f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 310f, "entry2 ID"),
				new OpTextBox(EntryB, new Vector2(200f,310f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 280f, "entry3 ID"),
				new OpTextBox(EntryC, new Vector2(200f,280f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 250f, "entry4 ID"),
				new OpTextBox(EntryD, new Vector2(200f,250f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 220f, "entry5 ID"),
				new OpTextBox(EntryE, new Vector2(200f,220f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 190f, "entry6 ID"),
				new OpTextBox(EntryF, new Vector2(200f,190f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 160f, "entry7 ID"),
				new OpTextBox(EntryG, new Vector2(200f,160f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 130f, "entry8 ID"),
				new OpTextBox(EntryH, new Vector2(200f,130f), 400f) { description = "Only change this if you know what you are doing!" },
				new OpLabel(10f, 100f, "entry9 ID"),
				new OpTextBox(EntryI, new Vector2(200f,100f), 400f) { description = "Only change this if you know what you are doing!" }
			};
			opTab.AddItems(UIArrPlayerOptions);
		}
	}
}