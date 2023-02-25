using System;
namespace Vestiges {
	public struct VestigeCoord {
		public int x;
		public int y;

		public VestigeCoord(int _x, int _y) {
			x = _x;
			y = _y;
		}


		public VestigeCoord(WorldCoordinate wCoord) {
			x = wCoord.x;
			y = wCoord.y;
		}
	}
}
