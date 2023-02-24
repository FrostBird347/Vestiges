using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RWCustom;
using UnityEngine;
using UnityEngine.Scripting;
using Random = UnityEngine.Random;

//Modified version of the vanilla fireflies
namespace Vestiges {
	public class Vestige : CosmeticInsect {
		private Vector2 dir;
		private Vector2 lastLastPos;
		public LightSource light;
		public Color col;
		public float sizeMult;
		public float sin;
		public Vector2 target;
		public bool noTarget;
		public float targetSwitchMult;
		public bool exists;
		BepInEx.Logging.ManualLogSource Logger;

		public void SetupLogger(BepInEx.Logging.ManualLogSource newLogger) {
			Logger = newLogger;
		}

		public Vestige(Room room, Vector2 pos, Vector2 targt, Color colour, int size) : base(room, pos, PluginEnums.Vestige) {
			lastLastPos = pos;
			this.pos = pos;
			sin = Random.value;
			col = colour;
			sizeMult = size / 2f;
			target = targt;
			noTarget = false;
			targetSwitchMult = 1f;
			exists = true;
			this.room = room;
			Logger = null;
		}

		public override void Reset(Vector2 resetPos) {
			base.Reset(resetPos);
			lastLastPos = resetPos;
		}

		public override void EmergeFromGround(Vector2 emergePos) {
			base.EmergeFromGround(emergePos);
			dir = new Vector2(0f, 1f);
		}

		public override void Update(bool eu) {
			if (room == null) {
				exists = false;
				Destroy();
				return;
			}
			vel *= 0.95f;
			vel.x = vel.x + dir.x * 0.3f;
			vel.y = vel.y + dir.y * 0.3f;

			if (Random.value > 0.95f * targetSwitchMult && !noTarget) {
				noTarget = true;
			} else if (Random.value > 0.75f && noTarget) {
				noTarget = false;
			}

			if (!noTarget) {
				dir = Vector2.Lerp(dir, Custom.DirVec(pos, target), Mathf.Pow(Random.value, 2f) * 0.65f);
			} else {
				dir = Vector2.Lerp(dir, Custom.DegToVec(Random.value * 360f) * Mathf.Pow(Random.value, 0.75f), 0.4f).normalized;
			}

			if (wantToBurrow) {
				dir = Vector2.Lerp(dir, new Vector2(0f, -1f), 0.1f);
			}

			float num = TileScore(room.GetTilePosition(pos));
			IntVector2 intVector = new IntVector2(0, 0);
			for (int i = 0; i < 4; i++) {
				if (!room.GetTile(room.GetTilePosition(pos) + Custom.fourDirections[i]).Solid && TileScore(room.GetTilePosition(pos) + Custom.fourDirections[i] * 3) > num) {
					num = TileScore(room.GetTilePosition(pos) + Custom.fourDirections[i] * 3);
					intVector = Custom.fourDirections[i];
				}
			}
			vel += intVector.ToVector2() * 0.4f;

			if (room.PointSubmerged(pos)) {
				vel *= 0.95f;
				targetSwitchMult = 0.78f;
			} else {
				targetSwitchMult = 1;
			}
			sin += 1f / Mathf.Lerp(20f, 80f, Random.value);

			if (room.Darkness(pos) > 0f) {
				if (light == null) {
					light = new LightSource(pos, false, col, this);
					light.noGameplayImpact = ModManager.MMF;
					room.AddObject(light);
				}
				light.setPos = new Vector2?(pos);
				light.setAlpha = new float?(0.15f - 0.1f * Mathf.Sin(sin * 3.14159274f * 2f)) * sizeMult;
				light.setRad = new float?(60f + 20f * Mathf.Sin(sin * 3.14159274f * 2f)) * sizeMult;
			} else if (light != null) {
				light.Destroy();
				light = null;
			}

			lastLastPos = lastPos;
			base.Update(eu);

			if (!room.BeingViewed) {
				exists = false;
				Destroy();
			}
		}

		private float TileScore(IntVector2 tile) {
			if (!room.readyForAI || !room.IsPositionInsideBoundries(tile)) {
				return 0f;
			}
			return Random.value / (float)Math.Abs(room.aimap.getAItile(tile).floorAltitude - 4) / (float)Math.Abs(room.aimap.getAItile(tile).terrainProximity - 4);
		}

		public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) {
			sLeaser.sprites = new FSprite[1];

			sLeaser.sprites[0] = new FSprite("pixel", true);
			sLeaser.sprites[0].scaleX = 2f * sizeMult;
			sLeaser.sprites[0].anchorY = 0f;
			sLeaser.sprites[0].color = this.col;

			this.AddToContainer(sLeaser, rCam, null);
		}

		public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
			sLeaser.sprites[0].x = Mathf.Lerp(this.lastPos.x, this.pos.x, timeStacker) - camPos.x;
			sLeaser.sprites[0].y = Mathf.Lerp(this.lastPos.y, this.pos.y, timeStacker) - camPos.y;
			sLeaser.sprites[0].rotation = Custom.AimFromOneVectorToAnother(Vector2.Lerp(this.lastLastPos, this.lastPos, timeStacker), Vector2.Lerp(this.lastPos, this.pos, timeStacker));
			sLeaser.sprites[0].scaleY = Mathf.Max(2f * sizeMult, (2f * sizeMult) + 0.55f * Vector2.Distance(Vector2.Lerp(this.lastLastPos, this.lastPos, timeStacker), Vector2.Lerp(this.lastPos, this.pos, timeStacker)));
			base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
		}

		public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) {
		}

		public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner) {
			base.AddToContainer(sLeaser, rCam, newContatiner);
		}

	}
}