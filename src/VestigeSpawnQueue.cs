using System;
using UnityEngine;

namespace Vestiges {
	public struct VestigeSpawnQueue {
		public readonly string room;
		public readonly string region;
		public readonly WorldCoordinate coord;
		public readonly WorldCoordinate safeCoord;
		public readonly Color colour;

		public VestigeSpawnQueue(WorldCoordinate _coord, WorldCoordinate _safeCoord, Color _colour) {
			coord = _coord;
			room = _safeCoord.ResolveRoomName();
			region = _coord.SaveToString().Split('_')[0];

			if (_safeCoord.Valid && _safeCoord.ResolveRoomName() == _coord.ResolveRoomName()) {
				safeCoord = _safeCoord;
			} else {
				safeCoord = _coord;
			}

			colour = _colour;
		}
	}
}
