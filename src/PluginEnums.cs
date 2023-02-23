using System;
namespace Vestiges {
	public class PluginEnums {
		public static CosmeticInsect.Type Vestige;

		public static void RegisterEnums() {
			Vestige = new CosmeticInsect.Type("Vestige", true);
		}

		public static void UnregisterEnums() {
			if (Vestige != null) {
				Vestige.Unregister();
				Vestige = null;
			}
		}

	}
}
