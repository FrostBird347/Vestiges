using System;
using UnityEngine;

namespace Vestiges {
	public struct VestigeSpawn {
		public readonly string room;
		public readonly string region;
		public Color colour;
		public WorldCoordinate spawn;
		public WorldCoordinate target;

		public VestigeSpawn(string _room, string _region, Color _colour, WorldCoordinate _spawn, WorldCoordinate _target) {
			room = _room;
			region = _region;
			colour = _colour;
			spawn = _spawn;
			target = _target;
		}
	}
}
