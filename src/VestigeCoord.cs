using System;
namespace Vestiges {
	public struct VestigeCoord {
		public int x;
		public int y;

		public VestigeCoord(int _x, int _y, bool isTarget = false) {
			x = _x;
			y = _y;

			if (isTarget && y < 0) {
				y = 0;
			}
		}


		public VestigeCoord(WorldCoordinate wCoord, bool isTarget = false) {
			x = wCoord.x;
			y = wCoord.y;

			if (isTarget && y < 0) {
				y = 0;
			}
		}
	}
}
