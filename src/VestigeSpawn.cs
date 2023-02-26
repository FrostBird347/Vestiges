using System;
using UnityEngine;

namespace Vestiges {
	public struct VestigeSpawn {
		public readonly string room;
		public readonly string region;
		public Color colour;
		public VestigeCoord spawn;
		public VestigeCoord target;
		public DateTime time;

		public VestigeSpawn(string _room, string _region, Color _colour, VestigeCoord _spawn, VestigeCoord _target, DateTime _time) {
			room = _room;
			region = _region;
			colour = _colour;
			spawn = _spawn;
			target = _target;
			time = _time;
		}
	}
}
