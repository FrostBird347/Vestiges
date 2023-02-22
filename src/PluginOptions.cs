using System;
using System.Security.Policy;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace RENAME_ME {

	public class PluginOptions : OptionInterface {
		private readonly ManualLogSource Logger;

		public readonly Configurable<float> exampleFloat;
		public readonly Configurable<bool> exampleBool = new Configurable<bool>(false);
		private UIelement[] UIArrPlayerOptions;

		public PluginOptions(Plugin pluginInstance, ManualLogSource logSource) {
			Logger = logSource;
			exampleFloat = config.Bind("exampleFloat", 1f, new ConfigAcceptableRange<float>(0f, 100f));
			exampleBool = config.Bind("exampleBool", false, (ConfigurableInfo)null);
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
				new OpUpdown(exampleFloat, new Vector2(150f,520f), 100f, 1),
				new OpLabel(10f, 490f, "Example bool"),
				new OpCheckBox(exampleBool, new Vector2(200f,490f)) { description = "Hover description" }
			};
			opTab.AddItems(UIArrPlayerOptions);
		}
	}
}