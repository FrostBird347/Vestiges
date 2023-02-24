using System;
using UnityEngine;

namespace Vestiges {
	public struct VestigeSpawn {
		public readonly string room;
		public readonly string region;
		public Color colour;
		public Vector2 spawn;
		public Vector2 target;

		public VestigeSpawn(string _room, string _region, Color _colour, Vector2 _spawn, Vector2 _target) {
			room = _room;
			region = _region;
			colour = _colour;
			spawn = _spawn;
			target = _target;
		}
	}
}
