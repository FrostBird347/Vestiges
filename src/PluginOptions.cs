using System;
using System.Security.Policy;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace Vestiges {

	public class PluginOptions : OptionInterface {
		private readonly ManualLogSource Logger;

		public readonly Configurable<string> UploadID;
		public readonly Configurable<string> DownloadID;
		private UIelement[] UIArrPlayerOptions;

		public PluginOptions(Plugin pluginInstance, ManualLogSource logSource) {
			Logger = logSource;
			UploadID = config.Bind("UploadID", "1FAIpQLSdkBHGRNMbJQGJ0A89CJfDrA98uy1DBL3VQuys9s91i41P1JA");
			DownloadID = config.Bind("DownloadID", "1mUk-KQp7Kv4U-ODamQwb7DUWNewvyXLucVu72bVqFZU");
		}

		public override void Initialize() {
			OpTab opTab = new OpTab(this, "Options");
			this.Tabs = new[]
			{
				opTab
			};

			UIArrPlayerOptions = new UIelement[]
			{
				new OpLabel(10f, 550f, "Options", true),
				new OpLabel(10f, 520f, "Example float"),
				new OpTextBox(UploadID, new Vector2(150f,520f), 100f),
				new OpLabel(10f, 490f, "Example bool"),
				new OpTextBox(DownloadID, new Vector2(200f,490f), 100f) { description = "Hover description" }
			};
			opTab.AddItems(UIArrPlayerOptions);
		}
	}
}